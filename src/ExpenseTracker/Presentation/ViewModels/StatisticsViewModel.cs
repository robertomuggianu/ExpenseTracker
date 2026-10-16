using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using ExpenseTracker.Presentation.Services;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class StatisticsViewModel(ReportingService reportingService, AccountService accountService, IAppNavigationService navigation) : ViewModelBase
{
    public ObservableCollection<string> Currencies { get; } = [];
    [ObservableProperty] public partial string SelectedCurrency { get; set; } = "EUR";
    [ObservableProperty] public partial DateTime ReferenceDate { get; set; } = DateTime.Today;
    public bool IsReferencePeriod => SelectedPeriod.Value is DateRangePreset.SelectedMonth or DateRangePreset.SelectedYear;
    public IEnumerable<ISeries> ChartSeries => ExpenseSeries;
    public decimal ChartTotal => Expense;
    public string ChartLabel => "Spese del periodo";
    partial void OnExpenseSeriesChanged(IEnumerable<ISeries> value) => OnPropertyChanged(nameof(ChartSeries));
    partial void OnExpenseChanged(decimal value) => OnPropertyChanged(nameof(ChartTotal));
    partial void OnSelectedCurrencyChanged(string value) => RequestRefresh();
    partial void OnReferenceDateChanged(DateTime value) { if (IsReferencePeriod) RequestRefresh(); }
    private DateRange displayedRange;
    private Guid? displayedAccount;
    [RelayCommand] private Task OpenCategoryAsync(CategoryBreakdownItem item) => navigation.GoToTransactionsAsync(
        new(displayedRange.FromUtc, displayedRange.ToUtcExclusive, displayedAccount, ExpenseTracker.Domain.TransactionType.Expense, item.CategoryId, Currency: ReportCurrency));
    [RelayCommand] private Task OpenChartAsync(IEnumerable<ChartPoint> points)
    {
        var series = points.FirstOrDefault()?.Context.Series;
        var index = ExpenseSeries.ToList().FindIndex(s => ReferenceEquals(s, series));
        return index >= 0 && index < CategoryBreakdown.Count ? OpenCategoryAsync(CategoryBreakdown[index]) : Task.CompletedTask;
    }
    private readonly Dictionary<Guid, string> accountCurrencies = [];
    private bool loadingAccounts;
    private int filterVersion;
    public ObservableCollection<CategoryBreakdownItem> CategoryBreakdown { get; } = [];
    [ObservableProperty] public partial bool HasChartData { get; set; }
    public bool HasNoChartData => !HasChartData;
    partial void OnHasChartDataChanged(bool value) => OnPropertyChanged(nameof(HasNoChartData));

    public IReadOnlyList<PeriodOption> Periods { get; } = PeriodOption.All;

    public ObservableCollection<AccountFilterOption> Accounts { get; } = [];

    [ObservableProperty]
    public partial string? ReportCurrency { get; set; } = "EUR";

    [ObservableProperty]
    public partial PeriodOption SelectedPeriod { get; set; } = PeriodOption.All[1];

    [ObservableProperty]
    public partial DateTime CustomFrom { get; set; } = DateTime.Today.AddMonths(-1);

    [ObservableProperty]
    public partial DateTime CustomTo { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial AccountFilterOption? SelectedAccount { get; set; }

    public bool IsCustomPeriod => SelectedPeriod.Value == DateRangePreset.Custom;
    partial void OnSelectedPeriodChanged(PeriodOption value)
    {
        OnPropertyChanged(nameof(IsCustomPeriod));
        OnPropertyChanged(nameof(IsReferencePeriod));
        RequestRefresh();
    }
    partial void OnSelectedAccountChanged(AccountFilterOption? value)
    {
        if (value?.Id is Guid id && accountCurrencies.TryGetValue(id, out var currency)) SelectedCurrency = currency;
        RequestRefresh();
    }
    partial void OnCustomFromChanged(DateTime value) { if (IsCustomPeriod) RequestRefresh(); }
    partial void OnCustomToChanged(DateTime value) { if (IsCustomPeriod) RequestRefresh(); }

    private void RequestRefresh()
    {
        filterVersion++;
        if (!loadingAccounts) _ = ApplyFiltersAsync();
    }

    [ObservableProperty]
    public partial decimal Income { get; set; }

    [ObservableProperty]
    public partial decimal Expense { get; set; }

    [ObservableProperty]
    public partial decimal Difference { get; set; }

    [ObservableProperty]
    public partial IEnumerable<ISeries> TrendSeries { get; set; } = [];

    [ObservableProperty]
    public partial IEnumerable<Axis> XAxes { get; set; } = [new Axis()];

    [ObservableProperty]
    public partial IEnumerable<Axis> YAxes { get; set; } = [];

    [ObservableProperty]
    public partial string TrendCaption { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoTrendData))]
    public partial bool HasTrendData { get; set; }

    public bool HasNoTrendData => !HasTrendData;

    [ObservableProperty]
    public partial IEnumerable<ISeries> ExpenseSeries { get; set; } = [];

    [RelayCommand]
    public Task LoadAsync() => RunAsync(async () =>
    {
        loadingAccounts = true;
        try
        {
            var accountId = SelectedAccount?.Id;
            Accounts.Clear();
            Accounts.Add(new AccountFilterOption(null, "Tutti i conti"));
            var accountItems = await accountService.GetAccountsAsync();
            accountCurrencies.Clear(); foreach (var account in accountItems) accountCurrencies[account.Account.Id] = account.Account.Currency;
            Currencies.Clear(); foreach (var code in accountItems.Select(a => a.Account.Currency).Distinct()) Currencies.Add(code);
            if (!Currencies.Contains(SelectedCurrency)) SelectedCurrency = Currencies.FirstOrDefault() ?? "EUR";
            foreach (var account in accountItems)
            {
                Accounts.Add(new AccountFilterOption(account.Account.Id, account.Account.Name));
            }

            SelectedAccount = Accounts.FirstOrDefault(item => item.Id == accountId) ?? Accounts[0];
        }
        finally { loadingAccounts = false; }
        await LoadLatestReportAsync();
    });

    [RelayCommand]
    private Task ApplyFiltersAsync() => RunAsync(LoadLatestReportAsync);

    private async Task LoadLatestReportAsync()
    {
        int version;
        do
        {
            version = filterVersion;
            await LoadReportCoreAsync();
        } while (version != filterVersion);
    }

    private async Task LoadReportCoreAsync()
    {
        var period = SelectedPeriod.Value;
        if (IsCustomPeriod && CustomFrom.Date > CustomTo.Date)
        {
            TrendSeries = []; ExpenseSeries = []; CategoryBreakdown.Clear(); HasChartData = false;
            HasTrendData = false; TrendCaption = string.Empty;
            Income = Expense = Difference = 0;
            throw new ExpenseTracker.Domain.DomainValidationException("La data iniziale deve precedere quella finale.");
        }
        var version = filterVersion;
        var range = DateRangeFactory.Create(period, IsReferencePeriod ? ReferenceDate : DateTime.Now, CustomFrom, CustomTo);
        var accountId = SelectedAccount?.Id;
        var summary = await reportingService.GetAnalyticsAsync(range, accountId, currency: SelectedCurrency);
        if (version != filterVersion) return;
        displayedRange = range; displayedAccount = accountId;
        ReportCurrency = summary.ReportCurrency;
        Income = summary.Totals.Income;
        Expense = summary.Totals.Expense;
        Difference = summary.Totals.Difference;
        var from = range.FromUtc.ToLocalTime().Date;
        var to = range.ToUtcExclusive.ToLocalTime().Date;
        var monthly = (to - from).TotalDays > 62;
        var weekly = !monthly && (to - from).TotalDays > 14;
        DateTime BucketDate(DateTime date) => monthly ? new DateTime(date.Year, date.Month, 1)
            : weekly ? from.AddDays(((date.Date - from).Days / 7) * 7) : date.Date;
        var values = summary.Trend.GroupBy(t => BucketDate(t.Date))
            .ToDictionary(g => g.Key, g => new TrendPoint(g.Key, g.Sum(t => t.Income), g.Sum(t => t.Expense)));
        var buckets = new List<TrendPoint>();
        for (var date = BucketDate(from); date < to; date = monthly ? date.AddMonths(1) : date.AddDays(weekly ? 7 : 1))
            buckets.Add(values.GetValueOrDefault(date) ?? new TrendPoint(date, 0, 0));
        var culture = System.Globalization.CultureInfo.GetCultureInfo("it-IT");
        var shortPeriod = (to - from).TotalDays <= 7;
        var labels = buckets.Select(t => monthly ? t.Date.ToString(from.Year == to.AddDays(-1).Year ? "MMM" : "MMM yy", culture)
            : weekly ? $"{t.Date:dd}–{(t.Date.AddDays(6) < to ? t.Date.AddDays(6) : to.AddDays(-1)):dd}"
            : t.Date.ToString(shortPeriod ? "ddd" : "dd/MM", culture)).ToArray();
        TrendCaption = $"Totali {(monthly ? "mensili" : weekly ? "settimanali" : "giornalieri")} · {from.ToString("dd MMM yyyy", culture)} – {to.AddDays(-1).ToString("dd MMM yyyy", culture)} · {ReportCurrency}";
        HasTrendData = buckets.Any(t => t.Income != 0 || t.Expense != 0);
        TrendSeries = [
            new ColumnSeries<decimal> { Name = "Entrate", Values = buckets.Select(t => t.Income).ToArray(),
                MaxBarWidth = 32, Padding = 3, Rx = 4, Ry = 4,
                YToolTipLabelFormatter = point => MoneyFormatter.Format(point.Model, ReportCurrency),
                Fill = new SolidColorPaint(SKColor.Parse("#80D9BE")) },
            new ColumnSeries<decimal> { Name = "Spese", Values = buckets.Select(t => t.Expense).ToArray(),
                MaxBarWidth = 32, Padding = 3, Rx = 4, Ry = 4,
                YToolTipLabelFormatter = point => MoneyFormatter.Format(point.Model, ReportCurrency),
                Fill = new SolidColorPaint(SKColor.Parse("#F4C66A")) }];
        XAxes = [new Axis { LabelsPaint = new SolidColorPaint(SKColor.Parse("#B2C4B8")), TextSize = 12,
            Labels = labels, MinStep = 1, ForceStepToMin = buckets.Count <= 7,
            MinLimit = -0.5, MaxLimit = buckets.Count - 0.5, LabelsRotation = 0,
            SeparatorsPaint = null }];
        YAxes = [new Axis { LabelsPaint = new SolidColorPaint(SKColor.Parse("#B2C4B8")), TextSize = 11,
            SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#3C5147")), MinLimit = 0,
            Labeler = value => value.ToString(value >= 1000000 ? "0,,.##' M'" : value >= 1000 ? "0,.##' k'" : "0.##", culture) }];
        ExpenseSeries = summary.ExpensesByCategory
            .Select(item => (ISeries)new PieSeries<decimal> { Name = item.Name, Values = [item.Total], InnerRadius = 75, MaxRadialColumnWidth = 42, Fill = new SolidColorPaint(SKColor.Parse(item.Color)), Stroke = new SolidColorPaint(SKColor.Parse("#26342F"), 3) })
            .ToArray();
        HasChartData = summary.ExpensesByCategory.Count > 0;
        CategoryBreakdown.Clear();
        foreach (var item in summary.ExpensesByCategory)
            CategoryBreakdown.Add(new CategoryBreakdownItem(item.Name, item.Color, $"category_{item.Icon}.png", item.Total,
                Expense == 0 ? "0%" : (item.Total / Expense).ToString("P0", System.Globalization.CultureInfo.GetCultureInfo("it-IT")), ReportCurrency, item.CategoryId));
    }
}
