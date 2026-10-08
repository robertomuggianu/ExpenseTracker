using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public enum DateRangePreset
{
    CurrentWeek,
    CurrentMonth,
    CurrentYear,
    Custom
}

public readonly record struct DateRange(DateTime FromUtc, DateTime ToUtcExclusive)
{
    public bool Contains(DateTime valueUtc) => valueUtc >= FromUtc && valueUtc < ToUtcExclusive;
}

public static class DateRangeFactory
{
    public static DateRange Create(
        DateRangePreset preset,
        DateTime nowLocal,
        DateTime? customFromLocal = null,
        DateTime? customToLocal = null)
    {
        var today = nowLocal.Date;
        DateTime from;
        DateTime toInclusive;

        switch (preset)
        {
            case DateRangePreset.CurrentWeek:
                var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
                from = today.AddDays(-daysFromMonday);
                toInclusive = from.AddDays(6);
                break;
            case DateRangePreset.CurrentMonth:
                from = new DateTime(today.Year, today.Month, 1);
                toInclusive = from.AddMonths(1).AddDays(-1);
                break;
            case DateRangePreset.CurrentYear:
                from = new DateTime(today.Year, 1, 1);
                toInclusive = new DateTime(today.Year, 12, 31);
                break;
            case DateRangePreset.Custom:
                from = customFromLocal?.Date
                    ?? throw new DomainValidationException("La data iniziale è obbligatoria.");
                toInclusive = customToLocal?.Date
                    ?? throw new DomainValidationException("La data finale è obbligatoria.");
                if (from > toInclusive)
                {
                    throw new DomainValidationException("La data iniziale non può essere successiva alla data finale.");
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset));
        }

        return new DateRange(ToUtc(from), ToUtc(toInclusive.AddDays(1)));
    }

    private static DateTime ToUtc(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Local).ToUniversalTime();
    }
}

public sealed record TransactionFilter(
    DateTime? FromUtc = null,
    DateTime? ToUtcExclusive = null,
    Guid? AccountId = null,
    TransactionType? Type = null,
    Guid? CategoryId = null);

public sealed record AccountInput(
    Guid? Id,
    string Name,
    decimal OpeningBalance,
    string Currency,
    bool IsPrimary,
    bool IsActive);

public sealed record CategoryInput(
    Guid? Id,
    string Name,
    TransactionType Type,
    string Color,
    bool IsActive);

public sealed record TransactionInput(
    Guid? Id,
    DateTime OccurredAtUtc,
    TransactionType Type,
    Guid AccountId,
    Guid CategoryId,
    decimal Amount,
    string Currency,
    string? Description,
    string? Notes);

public sealed record AccountSummary(Account Account, decimal CurrentBalance)
{
    public string DisplayName => Account.IsPrimary ? $"{Account.Name} • Principale" : Account.Name;

    public bool IsInactive => !Account.IsActive;
}

public sealed record TransactionListItem(
    Transaction Transaction,
    string AccountName,
    string CategoryName,
    string CategoryColor)
{
    public decimal SignedAmount => Transaction.Type == TransactionType.Income
        ? Transaction.Amount
        : -Transaction.Amount;

    public string TypeLabel => Transaction.Type == TransactionType.Income ? "Entrata" : "Uscita";
}

public sealed record PeriodTotals(decimal Income, decimal Expense)
{
    public decimal Difference => Income - Expense;
}

public sealed record ExpenseGroup(Guid CategoryId, string Name, string Color, decimal Total);

public sealed record TrendPoint(DateTime Date, decimal Income, decimal Expense);

public sealed record DashboardSummary(
    decimal PrimaryAccountBalance,
    decimal ActiveAccountsBalance,
    PeriodTotals Totals,
    IReadOnlyList<ExpenseGroup> ExpensesByCategory,
    IReadOnlyList<TransactionListItem> RecentTransactions);

public sealed record AnalyticsSummary(
    PeriodTotals Totals,
    IReadOnlyList<TrendPoint> Trend,
    IReadOnlyList<ExpenseGroup> ExpensesByCategory);

public sealed record CsvExportResult(string FileName, string Content);
