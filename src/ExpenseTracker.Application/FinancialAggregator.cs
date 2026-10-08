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

    public static IReadOnlyList<ExpenseGroup> GroupExpensesByCategory(
        IEnumerable<Transaction> transactions,
        IReadOnlyCollection<Category> categories,
        DateRange range) => GroupByCategory(transactions, categories, range, TransactionType.Expense);

    public static IReadOnlyList<ExpenseGroup> GroupByCategory(
        IEnumerable<Transaction> transactions,
        IReadOnlyCollection<Category> categories,
        DateRange range,
        TransactionType type)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        var categoryMap = categories.ToDictionary(item => item.Id);
        return transactions
            .Where(item => item.Type == type && range.Contains(item.OccurredAtUtc))
            .GroupBy(item => item.CategoryId)
            .Select(group =>
            {
                var category = categoryMap[group.Key];
                return new ExpenseGroup(category.Id, category.Name, category.Color, group.Sum(item => item.Amount)) { Icon = category.Icon };
            })
            .OrderByDescending(item => item.Total)
            .ToArray();
    }

    public static IReadOnlyList<TrendPoint> BuildDailyTrend(IEnumerable<Transaction> transactions, DateRange range)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        return transactions
            .Where(item => range.Contains(item.OccurredAtUtc))
            .GroupBy(item => item.OccurredAtUtc.ToLocalTime().Date)
            .OrderBy(group => group.Key)
            .Select(group => new TrendPoint(
                group.Key,
                group.Where(item => item.Type == TransactionType.Income).Sum(item => item.Amount),
                group.Where(item => item.Type == TransactionType.Expense).Sum(item => item.Amount)))
            .ToArray();
    }
}
