using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Presentation.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class DashboardViewModel(
    ReportingService reportingService,
    IAppNavigationService navigation) : ViewModelBase
{
    public IReadOnlyList<PeriodOption> Periods { get; } = PeriodOption.All;

    public ObservableCollection<TransactionListItem> RecentTransactions { get; } = [];

    [ObservableProperty]
    public partial PeriodOption SelectedPeriod { get; set; } = PeriodOption.All[1];

    [ObservableProperty]
    public partial DateTime CustomFrom { get; set; } = DateTime.Today.AddMonths(-1);

    [ObservableProperty]
    public partial DateTime CustomTo { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial decimal PrimaryBalance { get; set; }

    [ObservableProperty]
    public partial decimal TotalBalance { get; set; }

    [ObservableProperty]
    public partial decimal Income { get; set; }

    [ObservableProperty]
    public partial decimal Expense { get; set; }

    [ObservableProperty]
    public partial decimal Difference { get; set; }

    [ObservableProperty]
    public partial IEnumerable<ISeries> ExpenseSeries { get; set; } = [];

    [ObservableProperty]
    public partial bool IsCustomPeriod { get; set; }

    [ObservableProperty]
    public partial bool HasExpenseData { get; set; }

    [ObservableProperty]
    public partial bool HasRecentTransactions { get; set; }

    public bool HasNoExpenseData => !HasExpenseData;

    public bool HasNoRecentTransactions => !HasRecentTransactions;

    [RelayCommand]
    public Task LoadAsync() => RunAsync(async () =>
    {
        var range = DateRangeFactory.Create(SelectedPeriod.Value, DateTime.Now, CustomFrom, CustomTo);
        var summary = await reportingService.GetDashboardAsync(range);
        PrimaryBalance = summary.PrimaryAccountBalance;
        TotalBalance = summary.ActiveAccountsBalance;
        Income = summary.Totals.Income;
        Expense = summary.Totals.Expense;
        Difference = summary.Totals.Difference;
        ExpenseSeries = summary.ExpensesByCategory
            .Select(item => (ISeries)new PieSeries<decimal>
            {
                Name = item.Name,
                Values = [item.Total]
            })
            .ToArray();
        HasExpenseData = summary.ExpensesByCategory.Count > 0;

        RecentTransactions.Clear();
        foreach (var item in summary.RecentTransactions)
        {
            RecentTransactions.Add(item);
        }
        HasRecentTransactions = RecentTransactions.Count > 0;
    });

    [RelayCommand]
    private Task NewTransactionAsync() => navigation.GoToTransactionEditorAsync();

    [RelayCommand]
    private Task ShowTransactionsAsync() => navigation.GoToTransactionsAsync();

    partial void OnSelectedPeriodChanged(PeriodOption value)
    {
        IsCustomPeriod = value.Value == DateRangePreset.Custom;
        _ = LoadAsync();
    }

    partial void OnHasExpenseDataChanged(bool value) => OnPropertyChanged(nameof(HasNoExpenseData));

    partial void OnHasRecentTransactionsChanged(bool value) => OnPropertyChanged(nameof(HasNoRecentTransactions));

    partial void OnCustomFromChanged(DateTime value)
    {
        if (SelectedPeriod.Value == DateRangePreset.Custom)
        {
            _ = LoadAsync();
        }
    }

    partial void OnCustomToChanged(DateTime value)
    {
        if (SelectedPeriod.Value == DateRangePreset.Custom)
        {
            _ = LoadAsync();
        }
    }
}
