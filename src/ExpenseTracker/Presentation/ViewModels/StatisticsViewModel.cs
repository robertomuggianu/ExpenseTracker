using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class StatisticsViewModel(ReportingService reportingService, AccountService accountService) : ViewModelBase
{
    public IReadOnlyList<PeriodOption> Periods { get; } = PeriodOption.All;

    public ObservableCollection<AccountFilterOption> Accounts { get; } = [];

    [ObservableProperty]
    public partial PeriodOption SelectedPeriod { get; set; } = PeriodOption.All[1];

    [ObservableProperty]
    public partial DateTime CustomFrom { get; set; } = DateTime.Today.AddMonths(-1);

    [ObservableProperty]
    public partial DateTime CustomTo { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial AccountFilterOption? SelectedAccount { get; set; }

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
    public partial IEnumerable<ISeries> ExpenseSeries { get; set; } = [];

    [RelayCommand]
    public Task LoadAsync() => RunAsync(async () =>
    {
        var accountId = SelectedAccount?.Id;
        Accounts.Clear();
        Accounts.Add(new AccountFilterOption(null, "Tutti i conti"));
        foreach (var account in await accountService.GetAccountsAsync())
        {
            Accounts.Add(new AccountFilterOption(account.Account.Id, account.Account.Name));
        }

        SelectedAccount = Accounts.FirstOrDefault(item => item.Id == accountId) ?? Accounts[0];
        await LoadReportCoreAsync();
    });

    [RelayCommand]
    private Task ApplyFiltersAsync() => RunAsync(LoadReportCoreAsync);

    private async Task LoadReportCoreAsync()
    {
        var range = DateRangeFactory.Create(SelectedPeriod.Value, DateTime.Now, CustomFrom, CustomTo);
        var summary = await reportingService.GetAnalyticsAsync(range, SelectedAccount?.Id);
        Income = summary.Totals.Income;
        Expense = summary.Totals.Expense;
        Difference = summary.Totals.Difference;
        TrendSeries =
        [
            new LineSeries<decimal> { Name = "Entrate", Values = summary.Trend.Select(item => item.Income).ToArray() },
            new LineSeries<decimal> { Name = "Uscite", Values = summary.Trend.Select(item => item.Expense).ToArray() }
        ];
        XAxes = [new Axis { Labels = summary.Trend.Select(item => item.Date.ToString("dd/MM")).ToArray() }];
        ExpenseSeries = summary.ExpensesByCategory
            .Select(item => (ISeries)new PieSeries<decimal> { Name = item.Name, Values = [item.Total] })
            .ToArray();
    }
}
