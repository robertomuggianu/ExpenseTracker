using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Presentation.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
namespace ExpenseTracker.Presentation.ViewModels;
public partial class DashboardViewModel(ReportingService reportingService, IAppNavigationService navigation,
    AccountService accountService) : ViewModelBase
{
    [ObservableProperty] public partial string SelectedCurrency { get; set; } = "EUR";
    private bool refreshPending;
    private DateRange displayedRange;
    private int periodVersion;
    [ObservableProperty] public partial string? BalanceWarning { get; set; }
    [RelayCommand] private Task OpenCategoryAsync(CategoryBreakdownItem item)
    {
        var range = displayedRange;
        return navigation.GoToTransactionsAsync(new(range.FromUtc, range.ToUtcExclusive,
            Type: IsIncomeSelected ? ExpenseTracker.Domain.TransactionType.Income : ExpenseTracker.Domain.TransactionType.Expense,
            CategoryId: item.CategoryId, Currency: ReportCurrency));
    }
    public ObservableCollection<CategoryBreakdownItem> CategoryBreakdown { get; } = [];
    private IReadOnlyList<ExpenseGroup> expenseGroups = [];
    private IReadOnlyList<ExpenseGroup> incomeGroups = [];
    [ObservableProperty] public partial bool IsIncomeSelected { get; set; }
    public bool IsExpenseSelected => !IsIncomeSelected;
    public decimal ChartTotal => IsIncomeSelected ? Income : Expense;
    public string ChartLabel => IsIncomeSelected ? "Entrate del periodo" : "Spese del periodo";
    public string EmptyChartText => IsIncomeSelected ? "Nessuna entrata in questo periodo" : "Nessuna spesa in questo periodo";
    [RelayCommand] private void SelectExpenses() { IsIncomeSelected = false; UpdateChart(); }
    [RelayCommand] private void SelectIncome() { IsIncomeSelected = true; UpdateChart(); }
    partial void OnIsIncomeSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsExpenseSelected));
        OnPropertyChanged(nameof(ChartTotal));
        OnPropertyChanged(nameof(ChartLabel));
        OnPropertyChanged(nameof(EmptyChartText));
    }
    private void UpdateChart()
    {
        var groups = IsIncomeSelected ? incomeGroups : expenseGroups;
        ChartSeries = groups.Select(item => (ISeries)new PieSeries<decimal>
        {
            Name = item.Name, Values = [item.Total], InnerRadius = 75, MaxRadialColumnWidth = 42,
            Fill = new SolidColorPaint(SKColor.Parse(item.Color)),
            Stroke = new SolidColorPaint(SKColor.Parse("#26342F"), 3)
        }).ToArray();
        HasChartData = groups.Count > 0;
        CategoryBreakdown.Clear();
        foreach (var item in groups)
            CategoryBreakdown.Add(new CategoryBreakdownItem(item.Name, item.Color, $"category_{item.Icon}.png", item.Total,
                ChartTotal == 0 ? "0%" : (item.Total / ChartTotal).ToString("P0", CultureInfo.GetCultureInfo("it-IT")), ReportCurrency, item.CategoryId));
        OnPropertyChanged(nameof(ChartTotal));
    }
    [ObservableProperty] public partial DateTime SelectedMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public IReadOnlyList<PeriodOption> Periods { get; } =
    [new(DateRangePreset.CurrentWeek, "Settimana"), new(DateRangePreset.CurrentMonth, "Mese"),
     new(DateRangePreset.CurrentYear, "Anno"), new(DateRangePreset.Custom, "Periodo")];
    [ObservableProperty] public partial PeriodOption SelectedPeriod { get; set; } = new(DateRangePreset.CurrentMonth, "Mese");
    [ObservableProperty] public partial DateTime CustomFrom { get; set; } = DateTime.Today.AddDays(-30);
    [ObservableProperty] public partial DateTime CustomTo { get; set; } = DateTime.Today;
    public bool IsCustomPeriod => SelectedPeriod.Value == DateRangePreset.Custom;
    public bool CanStepPeriod => !IsCustomPeriod;
    private DateRange GetRange() => DateRangeFactory.Create(SelectedPeriod.Value, SelectedMonth, CustomFrom, CustomTo);
    public string MonthLabel
    {
        get
        {
            if (IsCustomPeriod) return $"{CustomFrom:dd/MM/yyyy} – {CustomTo:dd/MM/yyyy}";
            if (SelectedPeriod.Value == DateRangePreset.CurrentYear) return SelectedMonth.Year.ToString();
            if (SelectedPeriod.Value == DateRangePreset.CurrentWeek)
            {
                var range = GetRange();
                return $"{range.FromUtc.ToLocalTime():dd/MM/yyyy} – {range.ToUtcExclusive.ToLocalTime().AddDays(-1):dd/MM/yyyy}";
            }
            return SelectedMonth.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("it-IT"));
        }
    }
    private void RefreshPeriod()
    {
        OnPropertyChanged(nameof(MonthLabel));
        ++periodVersion; refreshPending = true;
        _ = LoadAsync();
    }
    partial void OnSelectedPeriodChanged(PeriodOption value)
    {
        OnPropertyChanged(nameof(IsCustomPeriod)); OnPropertyChanged(nameof(CanStepPeriod));
        SelectedMonth = DateTime.Today;
        RefreshPeriod();
    }
    partial void OnCustomFromChanged(DateTime value) { if (IsCustomPeriod) RefreshPeriod(); }
    partial void OnCustomToChanged(DateTime value) { if (IsCustomPeriod) RefreshPeriod(); }
    [ObservableProperty] public partial string? BalanceCurrency { get; set; } = "EUR";
    [ObservableProperty] public partial string? ReportCurrency { get; set; } = "EUR";
    [ObservableProperty] public partial decimal TotalBalance { get; set; }
    [ObservableProperty] public partial decimal Income { get; set; }
    [ObservableProperty] public partial decimal Expense { get; set; }
    [ObservableProperty] public partial decimal Difference { get; set; }
    [ObservableProperty] public partial string ComparisonText { get; set; } = string.Empty;
    [ObservableProperty] public partial IEnumerable<ISeries> ChartSeries { get; set; } = [];
    [ObservableProperty] public partial bool HasChartData { get; set; }
    public bool HasNoChartData => !HasChartData;
    [RelayCommand] public Task LoadAsync() => RunAsync(async () =>
    {
        var accounts = await accountService.GetAccountsAsync();
        // The primary active account determines the Home currency; other currencies stay separate.
        SelectedCurrency = accounts.Where(a => a.Account.IsActive)
            .OrderByDescending(a => a.Account.IsPrimary).ThenBy(a => a.Account.CreatedAtUtc).ThenBy(a => a.Account.Id)
            .Select(a => a.Account.Currency).FirstOrDefault() ?? "EUR";
        do { refreshPending = false; await LoadCoreAsync(); } while (refreshPending);
    });
    private async Task LoadCoreAsync()
    {
        var month = SelectedMonth; var currency = SelectedCurrency; var version = periodVersion;
        DateRange range;
        try { range = GetRange(); }
        catch (ExpenseTracker.Domain.DomainValidationException)
        {
            Income = Expense = Difference = 0; expenseGroups = []; incomeGroups = [];
            ComparisonText = ""; UpdateChart(); throw;
        }
        var summary = await reportingService.GetDashboardAsync(range, currency: currency);
        if (version != periodVersion || month != SelectedMonth || currency != SelectedCurrency) { refreshPending = true; return; }
        if (SelectedPeriod.Value == DateRangePreset.CurrentMonth)
        {
            var comparison = MonthlyPeriods.Comparison(month, DateTime.Today);
            var current = await reportingService.GetAnalyticsAsync(comparison.Current, currency: currency);
            var previous = await reportingService.GetAnalyticsAsync(comparison.Previous, currency: currency);
            if (version != periodVersion || month != SelectedMonth || currency != SelectedCurrency) { refreshPending = true; return; }
            var delta = current.Totals.Expense - previous.Totals.Expense;
            var period = comparison.Partial ? "allo stesso punto del mese scorso" : "al mese precedente";
            ComparisonText = current.ReportCurrency is not null && current.ReportCurrency == previous.ReportCurrency
                ? delta == 0 ? $"Spese uguali rispetto {period}."
                  : $"Spese: {MoneyFormatter.Format(Math.Abs(delta), current.ReportCurrency)} in {(delta > 0 ? "più" : "meno")} rispetto {period}."
                : "Confronto non disponibile tra valute diverse.";
        }
        else ComparisonText = "";
        displayedRange = range;
        BalanceWarning = summary.BalanceWarning;
        BalanceCurrency = summary.BalanceCurrency;
        ReportCurrency = summary.ReportCurrency;
        TotalBalance = summary.ActiveAccountsBalance;
        Income = summary.Totals.Income;
        Expense = summary.Totals.Expense;
        Difference = summary.Totals.Difference;
        expenseGroups = summary.ExpensesByCategory;
        incomeGroups = summary.IncomeByCategory;
        UpdateChart();
    }
    private Task StepPeriodAsync(int direction)
    {
        if (IsCustomPeriod) return Task.CompletedTask;
        SelectedMonth = SelectedPeriod.Value switch
        {
            DateRangePreset.CurrentWeek => SelectedMonth.AddDays(7 * direction),
            DateRangePreset.CurrentYear => SelectedMonth.AddYears(direction),
            _ => SelectedMonth.AddMonths(direction)
        };
        refreshPending = true; return LoadAsync();
    }
    [RelayCommand] private Task PreviousMonthAsync() => StepPeriodAsync(-1);
    [RelayCommand] private Task NextMonthAsync() => StepPeriodAsync(1);
    [RelayCommand] private Task CurrentMonthAsync() { SelectedMonth = DateTime.Today; refreshPending = true; return LoadAsync(); }
    [RelayCommand] private Task NewTransactionAsync() => navigation.GoToTransactionEditorAsync(returnToHome: true);
    partial void OnSelectedMonthChanged(DateTime value) => OnPropertyChanged(nameof(MonthLabel));
    partial void OnHasChartDataChanged(bool value) => OnPropertyChanged(nameof(HasNoChartData));
}

public sealed record CategoryBreakdownItem(string Name, string Color, string IconSource, decimal Total, string Percentage, string? Currency, Guid CategoryId = default);
