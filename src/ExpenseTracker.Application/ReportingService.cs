using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public sealed class ReportingService(IExpenseRepository repository)
{
    public async Task<DashboardSummary> GetDashboardAsync(
        DateRange range,
        CancellationToken cancellationToken = default)
    {
        var accounts = await repository.GetAccountsAsync(cancellationToken);
        var allTransactions = await repository.GetTransactionsAsync(cancellationToken: cancellationToken);
        var categories = await repository.GetCategoriesAsync(cancellationToken: cancellationToken);
        var macros = await repository.GetMacroCategoriesAsync(cancellationToken);
        var filtered = allTransactions.Where(item => range.Contains(item.OccurredAtUtc)).ToArray();
        var joined = TransactionService.Join(filtered, accounts, categories, macros);
        var primary = accounts.SingleOrDefault(item => item.IsPrimary && item.IsActive);

        return new DashboardSummary(
            primary?.CalculateCurrentBalance(allTransactions) ?? 0m,
            accounts.Where(item => item.IsActive).Sum(item => item.CalculateCurrentBalance(allTransactions)),
            FinancialAggregator.CalculateTotals(allTransactions, range),
            FinancialAggregator.GroupExpensesByMacroCategory(allTransactions, categories, macros, range),
            joined.Take(8).ToArray());
    }

    public async Task<AnalyticsSummary> GetAnalyticsAsync(
        DateRange range,
        Guid? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new TransactionFilter(range.FromUtc, range.ToUtcExclusive, accountId);
        var transactions = await repository.GetTransactionsAsync(filter, cancellationToken);
        var categories = await repository.GetCategoriesAsync(cancellationToken: cancellationToken);
        var macros = await repository.GetMacroCategoriesAsync(cancellationToken);

        return new AnalyticsSummary(
            FinancialAggregator.CalculateTotals(transactions, range),
            FinancialAggregator.BuildDailyTrend(transactions, range),
            FinancialAggregator.GroupExpensesByMacroCategory(transactions, categories, macros, range));
    }
}
