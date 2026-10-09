using ExpenseTracker.Application;

namespace ExpenseTracker.AssistantServer;

public static class ReportValidation
{
    public static bool IsValid(AssistantReportRequest r)
    {
        if (!Enum.IsDefined(r.Kind) || r.Currency is null || r.Currency.Length != 3 || r.Currency.Any(c => c is < 'A' or > 'Z') || !ValidPeriod(r.Current)) return false;
        if (r.Kind == AssistantReportKind.MonthlySummary) return r.Previous is null && r.ExpenseChange is null && r.ExpenseChangePercent is null;
        if (!ValidPeriod(r.Previous)) return false;
        var p = r.Previous!;
        var previousStart = r.Current.From.AddMonths(-1);
        if (p.From != previousStart || p.Through.Day != Math.Min(r.Current.Through.Day, DateTime.DaysInMonth(p.From.Year, p.From.Month))) return false;
        var delta = r.Current.Expense - p.Expense;
        var percent = p.Expense == 0 ? (decimal?)null : Math.Round(delta / p.Expense * 100m, 2);
        return r.ExpenseChange == delta && r.ExpenseChangePercent == percent;
    }

    private static bool ValidPeriod(AssistantPeriod? p) => p is not null && p.From.Year >= 2000 && p.From.Day == 1
        && p.Through >= p.From && p.Through.Year == p.From.Year && p.Through.Month == p.From.Month
        && p.Income is >= 0 and <= 1_000_000_000m && p.Expense is >= 0 and <= 1_000_000_000m
        && p.MovementCount >= 0 && p.Categories is not null && p.Categories.Count <= 11
        && p.Categories.All(c => c is not null && !string.IsNullOrWhiteSpace(c.Name) && c.Name.Length <= 120 && c.Expense is > 0 and <= 1_000_000_000m)
        && p.Categories.Sum(c => c.Expense) == p.Expense;
}
