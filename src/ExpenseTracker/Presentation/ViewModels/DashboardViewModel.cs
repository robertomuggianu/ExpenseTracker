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
    ICsvExportService csvExportService, IFileShareService fileShareService) : ViewModelBase
{
    public ObservableCollection<TransactionListItem> RecentTransactions { get; } = [];
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
                ChartTotal == 0 ? "0%" : (item.Total / ChartTotal).ToString("P0", CultureInfo.GetCultureInfo("it-IT")), ReportCurrency));
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
    [ObservableProperty] public partial bool HasRecentTransactions { get; set; }
    public bool HasNoChartData => !HasChartData;
    public bool HasNoRecentTransactions => !HasRecentTransactions;
    [RelayCommand] public Task LoadAsync() => RunAsync(LoadCoreAsync);
    private async Task LoadCoreAsync()
    {
        var summary = await reportingService.GetDashboardAsync(MonthlyPeriods.FullMonth(SelectedMonth));
        BalanceCurrency = summary.BalanceCurrency;
        ReportCurrency = summary.ReportCurrency;
        TotalBalance = summary.ActiveAccountsBalance;
        Income = summary.Totals.Income;
        Expense = summary.Totals.Expense;
        Difference = summary.Totals.Difference;
        var comparison = MonthlyPeriods.Comparison(SelectedMonth, DateTime.Today);
        var current = await reportingService.GetAnalyticsAsync(comparison.Current);
        var previous = await reportingService.GetAnalyticsAsync(comparison.Previous);
        var delta = current.Totals.Expense - previous.Totals.Expense;
        var period = comparison.Partial ? "allo stesso punto del mese scorso" : "al mese precedente";
        ComparisonText = current.ReportCurrency is not null && current.ReportCurrency == previous.ReportCurrency
            ? delta == 0 ? $"Spese uguali rispetto {period}."
              : $"Spese: {MoneyFormatter.Format(Math.Abs(delta), current.ReportCurrency)} in {(delta > 0 ? "più" : "meno")} rispetto {period}."
            : "Confronto non disponibile tra valute diverse.";
        expenseGroups = summary.ExpensesByCategory;
        incomeGroups = summary.IncomeByCategory;
        UpdateChart();
        RecentTransactions.Clear();
        foreach (var item in summary.RecentTransactions) RecentTransactions.Add(item);
        HasRecentTransactions = RecentTransactions.Count > 0;
    }
    [RelayCommand] private Task PreviousMonthAsync() => RunAsync(async () => { SelectedMonth = SelectedMonth.AddMonths(-1); await LoadCoreAsync(); });
    [RelayCommand] private Task NextMonthAsync() => RunAsync(async () => { SelectedMonth = SelectedMonth.AddMonths(1); await LoadCoreAsync(); });
    [RelayCommand] private Task CurrentMonthAsync() => RunAsync(async () => { SelectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1); await LoadCoreAsync(); });
    [RelayCommand] private Task ExportMonthAsync() => RunAsync(async () =>
    {
        var range = MonthlyPeriods.FullMonth(SelectedMonth);
        await fileShareService.ShareAsync(await csvExportService.ExportAsync(new TransactionFilter(range.FromUtc, range.ToUtcExclusive)));
    });
    [RelayCommand] private Task NewTransactionAsync() => navigation.GoToTransactionEditorAsync();
    [RelayCommand] private Task ShowTransactionsAsync() => navigation.GoToTransactionsAsync();
    partial void OnSelectedMonthChanged(DateTime value) => OnPropertyChanged(nameof(MonthLabel));
    partial void OnHasChartDataChanged(bool value) => OnPropertyChanged(nameof(HasNoChartData));
    partial void OnHasRecentTransactionsChanged(bool value) => OnPropertyChanged(nameof(HasNoRecentTransactions));
}

public sealed record CategoryBreakdownItem(string Name, string Color, string IconSource, decimal Total, string Percentage, string? Currency);
