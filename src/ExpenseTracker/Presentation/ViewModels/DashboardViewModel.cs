using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Presentation.Services;
using LiveChartsCore;
using LiveChartsCore.Kernel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
namespace ExpenseTracker.Presentation.ViewModels;
public partial class DashboardViewModel(ReportingService reportingService, IAppNavigationService navigation,
    AccountService accountService) : ViewModelBase
{
    public ObservableCollection<string> Currencies { get; } = [];
    [ObservableProperty] public partial string SelectedCurrency { get; set; } = "EUR";
    private bool loadingCurrencies;
    private bool refreshPending;
    private DateRange displayedRange;
    [ObservableProperty] public partial string? BalanceWarning { get; set; }
    partial void OnSelectedCurrencyChanged(string value) { if (!loadingCurrencies) { refreshPending = true; _ = LoadAsync(); } }
    [RelayCommand] private Task OpenCategoryAsync(CategoryBreakdownItem item)
    {
        var range = displayedRange;
        return navigation.GoToTransactionsAsync(new(range.FromUtc, range.ToUtcExclusive,
            Type: IsIncomeSelected ? ExpenseTracker.Domain.TransactionType.Income : ExpenseTracker.Domain.TransactionType.Expense,
            CategoryId: item.CategoryId, Currency: ReportCurrency));
    }
    [RelayCommand] private Task OpenChartAsync(IEnumerable<ChartPoint> points)
    {
        var series = points.FirstOrDefault()?.Context.Series;
        var index = ChartSeries.ToList().FindIndex(s => ReferenceEquals(s, series));
        return index >= 0 && index < CategoryBreakdown.Count ? OpenCategoryAsync(CategoryBreakdown[index]) : Task.CompletedTask;
    }
    public ObservableCollection<CategoryBreakdownItem> CategoryBreakdown { get; } = [];
    private IReadOnlyList<ExpenseGroup> expenseGroups = [];
    private IReadOnlyList<ExpenseGroup> incomeGroups = [];
    [ObservableProperty] public partial bool IsIncomeSelected { get; set; }
    public bool IsExpenseSelected => !IsIncomeSelected;
    public decimal ChartTotal => IsIncomeSelected ? Income : Expense;
    public string ChartLabel => IsIncomeSelected ? "Entrate del mese" : "Spese del mese";
    public string EmptyChartText => IsIncomeSelected ? "Nessuna entrata in questo mese" : "Nessuna spesa in questo mese";
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
    public string MonthLabel => SelectedMonth.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("it-IT"));
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
        loadingCurrencies = true;
        try { var accounts = await accountService.GetAccountsAsync(); Currencies.Clear(); foreach (var code in accounts.Select(a => a.Account.Currency).Distinct()) Currencies.Add(code); if (!Currencies.Contains(SelectedCurrency)) SelectedCurrency = Currencies.FirstOrDefault() ?? "EUR"; }
        finally { loadingCurrencies = false; }
        do { refreshPending = false; await LoadCoreAsync(); } while (refreshPending);
    });
    private async Task LoadCoreAsync()
    {
        var month = SelectedMonth; var currency = SelectedCurrency;
        var range = MonthlyPeriods.FullMonth(month);
        var summary = await reportingService.GetDashboardAsync(range, currency: currency);
        if (month != SelectedMonth || currency != SelectedCurrency) { refreshPending = true; return; }
        displayedRange = range;
        BalanceWarning = summary.BalanceWarning;
        BalanceCurrency = summary.BalanceCurrency;
        ReportCurrency = summary.ReportCurrency;
        TotalBalance = summary.ActiveAccountsBalance;
        Income = summary.Totals.Income;
        Expense = summary.Totals.Expense;
        Difference = summary.Totals.Difference;
        var comparison = MonthlyPeriods.Comparison(month, DateTime.Today);
        var current = await reportingService.GetAnalyticsAsync(comparison.Current, currency: currency);
        var previous = await reportingService.GetAnalyticsAsync(comparison.Previous, currency: currency);
        if (month != SelectedMonth || currency != SelectedCurrency) { refreshPending = true; return; }
        var delta = current.Totals.Expense - previous.Totals.Expense;
        var period = comparison.Partial ? "allo stesso punto del mese scorso" : "al mese precedente";
        ComparisonText = current.ReportCurrency is not null && current.ReportCurrency == previous.ReportCurrency
            ? delta == 0 ? $"Spese uguali rispetto {period}."
              : $"Spese: {MoneyFormatter.Format(Math.Abs(delta), current.ReportCurrency)} in {(delta > 0 ? "più" : "meno")} rispetto {period}."
            : "Confronto non disponibile tra valute diverse.";
        expenseGroups = summary.ExpensesByCategory;
        incomeGroups = summary.IncomeByCategory;
        UpdateChart();
    }
    [RelayCommand] private Task PreviousMonthAsync() { SelectedMonth = SelectedMonth.AddMonths(-1); refreshPending = true; return LoadAsync(); }
    [RelayCommand] private Task NextMonthAsync() { SelectedMonth = SelectedMonth.AddMonths(1); refreshPending = true; return LoadAsync(); }
    [RelayCommand] private Task CurrentMonthAsync() { SelectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1); refreshPending = true; return LoadAsync(); }
    [RelayCommand] private Task NewTransactionAsync() => navigation.GoToTransactionEditorAsync();
    partial void OnSelectedMonthChanged(DateTime value) => OnPropertyChanged(nameof(MonthLabel));
    partial void OnHasChartDataChanged(bool value) => OnPropertyChanged(nameof(HasNoChartData));
}

public sealed record CategoryBreakdownItem(string Name, string Color, string IconSource, decimal Total, string Percentage, string? Currency, Guid CategoryId = default);
