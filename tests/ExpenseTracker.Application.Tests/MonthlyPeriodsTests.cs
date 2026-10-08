using ExpenseTracker.Application;

namespace ExpenseTracker.Application.Tests;

public sealed class MonthlyPeriodsTests
{
    [Fact]
    public void CurrentMonth_ComparesSameDays_AndClampsFebruary()
    {
        var periods = MonthlyPeriods.Comparison(new DateTime(2026, 3, 1), new DateTime(2026, 3, 31));
        Assert.True(periods.Partial);
        Assert.Equal(new DateTime(2026, 3, 1), periods.Current.FromUtc.ToLocalTime().Date);
        Assert.Equal(new DateTime(2026, 4, 1), periods.Current.ToUtcExclusive.ToLocalTime().Date);
        Assert.Equal(new DateTime(2026, 2, 1), periods.Previous.FromUtc.ToLocalTime().Date);
        Assert.Equal(new DateTime(2026, 3, 1), periods.Previous.ToUtcExclusive.ToLocalTime().Date);
    }

    [Fact]
    public void PastMonth_UsesWholePreviousMonth_AndHandlesYearBoundary()
    {
        var periods = MonthlyPeriods.Comparison(new DateTime(2026, 1, 1), new DateTime(2026, 10, 8));
        Assert.False(periods.Partial);
        Assert.Equal(new DateTime(2025, 12, 1), periods.Previous.FromUtc.ToLocalTime().Date);
        Assert.Equal(new DateTime(2026, 1, 1), periods.Previous.ToUtcExclusive.ToLocalTime().Date);
        Assert.Equal(new DateTime(2026, 2, 1), periods.Current.ToUtcExclusive.ToLocalTime().Date);
    }

    [Fact]
    public void CurrentMonth_DoesNotCompareEightDaysToAnEntireMonth()
    {
        var periods = MonthlyPeriods.Comparison(new DateTime(2026, 10, 1), new DateTime(2026, 10, 8));
        Assert.Equal(new DateTime(2026, 10, 9), periods.Current.ToUtcExclusive.ToLocalTime().Date);
        Assert.Equal(new DateTime(2026, 9, 9), periods.Previous.ToUtcExclusive.ToLocalTime().Date);
    }
}
