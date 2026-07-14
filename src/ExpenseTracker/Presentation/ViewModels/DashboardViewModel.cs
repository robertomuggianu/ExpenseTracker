using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class DashboardViewModel(ReportingService reportingService) : ViewModelBase
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
        ExpenseSeries = summary.ExpensesByMacroCategory
            .Select(item => (ISeries)new PieSeries<decimal>
            {
                Name = item.Name,
                Values = [item.Total]
            })
            .ToArray();

        RecentTransactions.Clear();
        foreach (var item in summary.RecentTransactions)
        {
            RecentTransactions.Add(item);
        }
    });

    partial void OnSelectedPeriodChanged(PeriodOption value) => _ = LoadAsync();

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
