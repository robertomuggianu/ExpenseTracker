using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

/// <summary>Creates local, currency-specific facts. No descriptions, notes or account names leave the device.</summary>
public sealed class AssistantReportService(IExpenseRepository repository)
{
    public async Task<AssistantReportRequest> CreateAsync(AssistantReportKind kind, string currency,
        DateTime todayLocal, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(kind)) throw new DomainValidationException("Tipo di resoconto non valido.");
        currency = currency.Trim().ToUpperInvariant();
        if (currency.Length != 3 || currency.Any(c => c is < 'A' or > 'Z'))
            throw new DomainValidationException("Seleziona una valuta valida.");
        var today = todayLocal.Date;
        var start = new DateTime(today.Year, today.Month, 1);
        var categories = await repository.GetCategoriesAsync(cancellationToken);
        async Task<AssistantPeriod> ReadAsync(DateTime from, DateTime through)
        {
            var range = new DateRange(ToUtc(from), ToUtc(through.AddDays(1)));
            var items = (await repository.GetTransactionsAsync(new(range.FromUtc, range.ToUtcExclusive, Currency: currency), cancellationToken))
                .Where(t => range.Contains(t.OccurredAtUtc) && t.Currency == currency && t.TransferId is null).ToArray();
            var totals = FinancialAggregator.CalculateTotals(items, range);
            var groups = FinancialAggregator.GroupExpensesByCategory(items, categories, range);
            var selected = groups.Take(10).Select(g => new AssistantCategory(g.Name.Length > 120 ? g.Name[..120] : g.Name, g.Total)).ToList();
            if (groups.Count > 10) selected.Add(new("Altre categorie", groups.Skip(10).Sum(g => g.Total)));
            return new(DateOnly.FromDateTime(from), DateOnly.FromDateTime(through), totals.Income, totals.Expense, items.Length, selected);
        }
        var current = await ReadAsync(start, today);
        AssistantPeriod? previous = null;
        decimal? change = null, percent = null;
        if (kind == AssistantReportKind.MonthComparison)
        {
            var previousStart = start.AddMonths(-1);
            var previousThrough = previousStart.AddDays(Math.Min(today.Day, DateTime.DaysInMonth(previousStart.Year, previousStart.Month)) - 1);
            previous = await ReadAsync(previousStart, previousThrough);
            change = current.Expense - previous.Expense;
            percent = previous.Expense == 0 ? null : Math.Round(change.Value / previous.Expense * 100m, 2);
        }
        return new(kind, currency, current, previous, change, percent);
    }

    private static DateTime ToUtc(DateTime date) => DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();
}
