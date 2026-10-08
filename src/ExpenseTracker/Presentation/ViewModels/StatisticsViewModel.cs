using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class StatisticsViewModel(ReportingService reportingService, AccountService accountService) : ViewModelBase
{
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
    partial void OnSelectedPeriodChanged(PeriodOption value) => OnPropertyChanged(nameof(IsCustomPeriod));

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
        ReportCurrency = summary.ReportCurrency;
        Income = summary.Totals.Income;
        Expense = summary.Totals.Expense;
        Difference = summary.Totals.Difference;
        if (SelectedPeriod.Value == DateRangePreset.CurrentYear)
        {
            var monthly = Enumerable.Range(1, 12).Select(month => new
            {
                Date = new DateTime(DateTime.Today.Year, month, 1),
                Income = summary.Trend.Where(item => item.Date.Month == month).Sum(item => item.Income),
                Expense = summary.Trend.Where(item => item.Date.Month == month).Sum(item => item.Expense)
            }).ToArray();
            TrendSeries =
            [
                new ColumnSeries<decimal> { Name = "Entrate", Values = monthly.Select(item => item.Income).ToArray(), Fill = new SolidColorPaint(SKColor.Parse("#34D399")) },
                new ColumnSeries<decimal> { Name = "Spese", Values = monthly.Select(item => item.Expense).ToArray(), Fill = new SolidColorPaint(SKColor.Parse("#A78BFA")) }
            ];
            XAxes = [new Axis { Labels = monthly.Select(item => item.Date.ToString("MMM", System.Globalization.CultureInfo.GetCultureInfo("it-IT"))).ToArray() }];
        }
        else
        {
            var days = new List<TrendPoint>();
            for (var day = range.FromUtc.ToLocalTime().Date; day < range.ToUtcExclusive.ToLocalTime().Date; day = day.AddDays(1))
                days.Add(summary.Trend.FirstOrDefault(item => item.Date == day) ?? new TrendPoint(day, 0, 0));
            TrendSeries =
            [
                new LineSeries<decimal> { Name = "Entrate", Values = days.Select(item => item.Income).ToArray(), Stroke = new SolidColorPaint(SKColor.Parse("#34D399"), 3), Fill = null },
                new LineSeries<decimal> { Name = "Spese", Values = days.Select(item => item.Expense).ToArray(), Stroke = new SolidColorPaint(SKColor.Parse("#A78BFA"), 3), Fill = null }
            ];
            XAxes = [new Axis { Labels = days.Select(item => item.Date.ToString("dd/MM")).ToArray() }];
        }
        ExpenseSeries = summary.ExpensesByCategory
            .Select(item => (ISeries)new PieSeries<decimal> { Name = item.Name, Values = [item.Total], InnerRadius = 65, Fill = new SolidColorPaint(SKColor.Parse(item.Color)), Stroke = null })
            .ToArray();
    }
}
