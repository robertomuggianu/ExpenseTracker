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
    [ObservableProperty] public partial DateTime SelectedMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public string MonthLabel => SelectedMonth.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("it-IT"));
    [ObservableProperty] public partial string? BalanceCurrency { get; set; } = "EUR";
    [ObservableProperty] public partial string? ReportCurrency { get; set; } = "EUR";
    [ObservableProperty] public partial decimal TotalBalance { get; set; }
    [ObservableProperty] public partial decimal Income { get; set; }
    [ObservableProperty] public partial decimal Expense { get; set; }
    [ObservableProperty] public partial decimal Difference { get; set; }
    [ObservableProperty] public partial string ComparisonText { get; set; } = string.Empty;
    [ObservableProperty] public partial IEnumerable<ISeries> ExpenseSeries { get; set; } = [];
    [ObservableProperty] public partial bool HasExpenseData { get; set; }
    [ObservableProperty] public partial bool HasRecentTransactions { get; set; }
    public bool HasNoExpenseData => !HasExpenseData;
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
              : $"{MoneyFormatter.Format(Math.Abs(delta), current.ReportCurrency)} in {(delta > 0 ? "più" : "meno")} rispetto {period}."
            : "Confronto non disponibile tra valute diverse.";
        ExpenseSeries = summary.ExpensesByCategory.Select(item => (ISeries)new PieSeries<decimal>
        {
            Name = item.Name, Values = [item.Total], InnerRadius = 65,
            Fill = new SolidColorPaint(SKColor.Parse(item.Color)), Stroke = null
        }).ToArray();
        HasExpenseData = summary.ExpensesByCategory.Count > 0;
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
    partial void OnHasExpenseDataChanged(bool value) => OnPropertyChanged(nameof(HasNoExpenseData));
    partial void OnHasRecentTransactionsChanged(bool value) => OnPropertyChanged(nameof(HasNoRecentTransactions));
}
