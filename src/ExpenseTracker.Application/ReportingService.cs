using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public sealed class ReportingService(IExpenseRepository repository)
{
    public async Task<DashboardSummary> GetDashboardAsync(
        DateRange range,
        CancellationToken cancellationToken = default, string? currency = null)
    {
        var accounts = await repository.GetAccountsAsync(cancellationToken);
        var balances = await repository.GetAccountSummariesAsync(cancellationToken);
        var categories = await repository.GetCategoriesAsync(cancellationToken);
        currency ??= accounts.FirstOrDefault(a => a.IsPrimary)?.Currency ?? "EUR";
        var reportTransactions = await repository.GetTransactionsAsync(new TransactionFilter(range.FromUtc, range.ToUtcExclusive, Currency: currency), cancellationToken);
        var filtered = reportTransactions.Where(item => range.Contains(item.OccurredAtUtc)).ToArray();
        var joined = TransactionService.Join(filtered, accounts, categories);
        var primary = accounts.SingleOrDefault(item => item.IsPrimary && item.IsActive);

        return new DashboardSummary(
            balances.FirstOrDefault(b => b.Account.Id == primary?.Id)?.CurrentBalance ?? 0m,
            balances.Where(b => b.Account.IsActive && b.Account.Currency == currency).Sum(b => b.CurrentBalance),
            FinancialAggregator.CalculateTotals(reportTransactions, range),
            FinancialAggregator.GroupExpensesByCategory(reportTransactions, categories, range),
            joined.Take(8).ToArray())
        {
            BalanceCurrency = currency,
            BalanceWarning = balances.Any(b => b.HasCurrencyMismatch) ? "Alcuni conti contengono movimenti con valuta diversa: il saldo è parziale. Correggili da Movimenti." : null,
            PrimaryCurrency = primary?.Currency ?? "EUR",
            IncomeByCategory = FinancialAggregator.GroupByCategory(reportTransactions, categories, range, TransactionType.Income),
            ReportCurrency = currency
        };
    }

    public async Task<AnalyticsSummary> GetAnalyticsAsync(
        DateRange range,
        Guid? accountId = null,
        CancellationToken cancellationToken = default, string? currency = null)
    {
        var accounts = await repository.GetAccountsAsync(cancellationToken);
        currency ??= (accountId.HasValue ? accounts.FirstOrDefault(a => a.Id == accountId) : accounts.FirstOrDefault(a => a.IsPrimary))?.Currency ?? "EUR";
        var filter = new TransactionFilter(range.FromUtc, range.ToUtcExclusive, accountId, Currency: currency);
        var transactions = await repository.GetTransactionsAsync(filter, cancellationToken);
        var categories = await repository.GetCategoriesAsync(cancellationToken);

        return new AnalyticsSummary(
            FinancialAggregator.CalculateTotals(transactions, range),
            FinancialAggregator.BuildDailyTrend(transactions, range),
            FinancialAggregator.GroupExpensesByCategory(transactions, categories, range))
        {
            ReportCurrency = currency
        };
    }
}
