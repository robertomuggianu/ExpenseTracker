using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public static class FinancialAggregator
{
    public static PeriodTotals CalculateTotals(IEnumerable<Transaction> transactions, DateRange range)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var items = transactions.Where(item => range.Contains(item.OccurredAtUtc)).ToArray();
        return new PeriodTotals(
            items.Where(item => item.Type == TransactionType.Income).Sum(item => item.Amount),
            items.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Amount));
    }

    public static IReadOnlyList<ExpenseGroup> GroupExpensesByMacroCategory(
        IEnumerable<Transaction> transactions,
        IReadOnlyCollection<Category> categories,
        IReadOnlyCollection<MacroCategory> macroCategories,
        DateRange range)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var categoryMap = categories.ToDictionary(item => item.Id);
        var macroMap = macroCategories.ToDictionary(item => item.Id);

        return transactions
            .Where(item => item.Type == TransactionType.Expense && range.Contains(item.OccurredAtUtc))
            .GroupBy(item => categoryMap[item.CategoryId].MacroCategoryId)
            .Select(group =>
            {
                var macro = macroMap[group.Key];
                return new ExpenseGroup(macro.Id, macro.Name, macro.Color, group.Sum(item => item.Amount));
            })
            .OrderByDescending(item => item.Total)
            .ToArray();
    }

    public static IReadOnlyList<TrendPoint> BuildDailyTrend(IEnumerable<Transaction> transactions, DateRange range)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        return transactions
            .Where(item => range.Contains(item.OccurredAtUtc))
            .GroupBy(item => item.OccurredAtUtc.Date)
            .OrderBy(group => group.Key)
            .Select(group => new TrendPoint(
                group.Key,
                group.Where(item => item.Type == TransactionType.Income).Sum(item => item.Amount),
                group.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Amount)))
            .ToArray();
    }
}
