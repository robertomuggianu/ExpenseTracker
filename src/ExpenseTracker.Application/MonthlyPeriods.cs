namespace ExpenseTracker.Application;

public static class MonthlyPeriods
{
    public static DateRange FullMonth(DateTime month) => DateRangeFactory.Create(
        DateRangePreset.CurrentMonth, month);

    public static (DateRange Current, DateRange Previous, bool Partial) Comparison(DateTime month, DateTime today)
    {
        var start = new DateTime(month.Year, month.Month, 1);
        var previous = start.AddMonths(-1);
        var partial = start.Year == today.Year && start.Month == today.Month;
        var lastDay = partial ? today.Day : DateTime.DaysInMonth(start.Year, start.Month);
        var previousLastDay = partial
            ? Math.Min(lastDay, DateTime.DaysInMonth(previous.Year, previous.Month))
            : DateTime.DaysInMonth(previous.Year, previous.Month);
        return (
            DateRangeFactory.Create(DateRangePreset.Custom, today, start, start.AddDays(lastDay - 1)),
            DateRangeFactory.Create(DateRangePreset.Custom, today, previous, previous.AddDays(previousLastDay - 1)),
            partial);
    }
}
