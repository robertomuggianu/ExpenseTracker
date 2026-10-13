using System.Globalization;
using System.Text.Json;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

/// <summary>Executes the bounded query plan locally. The model never receives SQL, notes or descriptions.</summary>
public sealed class AssistantChatService(IExpenseRepository repository)
{
    private static readonly CultureInfo Italian = CultureInfo.GetCultureInfo("it-IT");
    private static readonly System.Text.Encodings.Web.JavaScriptEncoder ReadableEncoder =
        System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All);

    public async Task<IReadOnlyList<string>> GetCategoryNamesAsync(CancellationToken token = default)
    {
        var names = (await repository.GetCategoriesAsync(token)).Select(c => c.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length > 128 || names.Any(n => n.Length > 120) || names.Sum(n => n.Length) > 4096)
            throw new DomainValidationException("This chat supports up to 128 categories, 120 characters per name and 4096 characters in total.");
        return names;
    }

    public async Task<AssistantChatEvidence> ExecuteAsync(string question, AssistantChatPlan plan, string currency,
        DateTime todayLocal, CancellationToken token = default)
    {
        if (!AssistantChatPolicy.ValidQuestion(question) || !AssistantChatPolicy.ValidPlan(plan) || !AssistantChatPolicy.ValidCurrency(currency))
            throw new DomainValidationException("The question could not be interpreted. Please specify the period and the information you need.");
        if (AssistantQuestionPeriod.Resolve(question) is { } explicitPeriod) plan = plan with { Period = explicitPeriod };
        var today = DateOnly.FromDateTime(todayLocal);
        if (today.Year is < 2000 or > 9998) throw new DomainValidationException("The device date is invalid.");
        var start = new DateOnly(today.Year, today.Month, 1);
        var previousStart = start.AddMonths(-1);
        var categories = await repository.GetCategoriesAsync(token);
        var filterName = string.IsNullOrWhiteSpace(plan.Category) ? null : categories.FirstOrDefault(c =>
            c.Name.Equals(plan.Category.Trim(), StringComparison.OrdinalIgnoreCase))?.Name;
        if (!string.IsNullOrWhiteSpace(plan.Category) && filterName is null)
            throw new DomainValidationException("This category does not exist. Use a category name from the app.");
        plan = plan with { Category = filterName, Comparison = AssistantQuestionPeriod.ResolveComparison(question) };
        var categoryIds = categories.Where(c => filterName is null || c.Name.Equals(filterName, StringComparison.OrdinalIgnoreCase)).Select(c => c.Id).ToHashSet();
        var categoryMap = categories.ToDictionary(c => c.Id, c => c.Name);
        string Name(Transaction t) => categoryMap.GetValueOrDefault(t.CategoryId, "Unavailable category");
        string Money(decimal value) => value.ToString("N2", Italian) + " " + currency;
        string Quote(string name) => '"' + JsonEncodedText.Encode(name, ReadableEncoder).ToString() + '"';
        decimal Amount(IEnumerable<Transaction> items) => plan.Measure switch
        {
            AssistantChatMeasure.Income => items.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount),
            AssistantChatMeasure.Net => items.Sum(t => t.Type == TransactionType.Income ? t.Amount : -t.Amount),
            _ => items.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount)
        };
        Transaction[] Measured(IEnumerable<Transaction> items) => items.Where(t => plan.Measure == AssistantChatMeasure.Net
            || t.Type == (plan.Measure == AssistantChatMeasure.Income ? TransactionType.Income : TransactionType.Expense)).ToArray();
        string measure = plan.Measure switch { AssistantChatMeasure.Income => "Income", AssistantChatMeasure.Net => "Income minus expenses", _ => "Expenses" };
        var periods = new List<(string Label, DateOnly From, DateOnly Through, Transaction[] Items)>();
        async Task ReadAsync(string label, DateOnly from, DateOnly through)
        {
            DateTime Utc(DateOnly d) => DateTime.SpecifyKind(d.ToDateTime(TimeOnly.MinValue), DateTimeKind.Local).ToUniversalTime();
            var range = new DateRange(Utc(from), Utc(through.AddDays(1)));
            var items = (await repository.GetTransactionsAsync(new(range.FromUtc, range.ToUtcExclusive, Currency: currency), token))
                .Where(t => range.Contains(t.OccurredAtUtc) && t.Currency == currency && t.TransferId is null && categoryIds.Contains(t.CategoryId)).ToArray();
            periods.Add((label, from, through, items));
        }
        if (plan.Period != AssistantChatPeriod.Previous) await ReadAsync("Current month", start, today);
        if (plan.Period != AssistantChatPeriod.Current)
        {
            var day = plan.Period == AssistantChatPeriod.Comparison && plan.Comparison == AssistantChatComparison.SameDays
                ? Math.Min(today.Day, DateTime.DaysInMonth(previousStart.Year, previousStart.Month))
                : DateTime.DaysInMonth(previousStart.Year, previousStart.Month);
            await ReadAsync("Previous month", previousStart, new(previousStart.Year, previousStart.Month, day));
        }
        var facts = new List<string> { $"Currency: {currency}. Scope: all accounts; transfers excluded.",
            filterName is null ? "Categories: all." : $"Selected category: {Quote(filterName)}.",
            "Source: transactions recorded in the app. Income minus expenses is neither a bank balance nor a complete measure of savings.",
            "Periods and counts cover only the stated dates; these data do not establish the cause of a change.",
            "Amounts use Italian number formatting (decimal comma); dates use DD/MM/YYYY." };
        var answers = new List<string>();
        var values = new List<decimal>();
        foreach (var p in periods)
        {
            var measured = Measured(p.Items);
            var income = p.Items.Where(t => t.Type == TransactionType.Income).Sum(t => t.Amount);
            var expenses = p.Items.Where(t => t.Type == TransactionType.Expense).Sum(t => t.Amount);
            var period = $"{p.Label}: {p.From:dd/MM/yyyy} – {p.Through:dd/MM/yyyy}";
            facts.Add($"{period}, inclusive dates. {p.Items.Length} transactions in scope; {measured.Length} transactions for the requested measure.");
            string result;
            decimal value = Amount(p.Items);
            switch (plan.Operation)
            {
                case AssistantChatOperation.Summary:
                    result = $"Income {Money(income)}; expenses {Money(expenses)}; income minus expenses {Money(income - expenses)}.";
                    break;
                case AssistantChatOperation.Categories:
                    var groups = measured.GroupBy(Name).Select(g => (Name: g.Key, Total: g.Sum(t => t.Amount))).OrderByDescending(g => g.Total).ThenBy(g => g.Name).ToArray();
                    var rows = groups.Take(10).Select(g => $"{Quote(g.Name)}: {Money(g.Total)}").ToList();
                    if (groups.Length > 10) rows.Add($"Other categories: {Money(groups.Skip(10).Sum(g => g.Total))}");
                    result = rows.Count == 0 ? "No transactions for this measure." : $"{measure} by category (top ten, with the remainder grouped): " + string.Join("; ", rows) + ".";
                    break;
                case AssistantChatOperation.AverageDaily:
                    var days = p.Through.DayNumber - p.From.DayNumber + 1;
                    value = Math.Round(value / days, 2, MidpointRounding.AwayFromZero);
                    result = $"Daily average ({measure.ToLowerInvariant()}): {Money(value)} over {days} days, including days without transactions.";
                    break;
                case AssistantChatOperation.AverageTransaction:
                    value = measured.Length == 0 ? 0 : Math.Round(value / measured.Length, 2, MidpointRounding.AwayFromZero);
                    result = $"Average transaction ({measure.ToLowerInvariant()}): {Money(value)} across {measured.Length} transactions.";
                    break;
                case AssistantChatOperation.Largest:
                    var largest = measured.OrderByDescending(t => t.Amount).ThenBy(t => t.OccurredAtUtc).ThenBy(t => t.Id).FirstOrDefault();
                    value = largest?.Amount ?? 0;
                    result = largest is null ? "No transactions for this measure." : $"Largest transaction ({measure.ToLowerInvariant()}): {Money(value)}, category {Quote(Name(largest))}, date {largest.OccurredAtUtc.ToLocalTime():dd/MM/yyyy}.";
                    break;
                case AssistantChatOperation.Count:
                    value = measured.Length;
                    result = $"Transaction count ({(plan.Measure == AssistantChatMeasure.Net ? "income and expenses" : measure.ToLowerInvariant())}): {measured.Length}.";
                    break;
                default:
                    result = $"{measure}: {Money(value)}.";
                    break;
            }
            values.Add(value); facts.Add(result); answers.Add($"{period}. {result}");
            if (p.Items.Length == 0) facts.Add("No transactions recorded in this period: this does not establish an absence of actual spending.");
        }
        if (plan.Period == AssistantChatPeriod.Comparison)
        {
            var change = values[0] - values[1];
            string Delta(decimal d) => plan.Operation == AssistantChatOperation.Count ? d.ToString("0", Italian) + " transactions" : Money(d);
            var percent = values[1] == 0 || plan.Measure == AssistantChatMeasure.Net ? "Percentage change cannot be calculated for this baseline."
                : $"Percentage change: {Math.Round(change / values[1] * 100m, 2).ToString("N2", Italian)}%.";
            var comparison = $"Change in {measure.ToLowerInvariant()} (current minus previous): {Delta(change)}. {percent}";
            facts.Add(comparison); answers.Add(comparison);
            if (plan.Operation == AssistantChatOperation.Categories)
            {
                var current = Measured(periods[0].Items).GroupBy(Name).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
                var previous = Measured(periods[1].Items).GroupBy(Name).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
                var changes = current.Keys.Union(previous.Keys).Select(n => (Name: n, Change: current.GetValueOrDefault(n) - previous.GetValueOrDefault(n))).ToArray();
                var increase = changes.Where(c => c.Change > 0).OrderByDescending(c => c.Change).ThenBy(c => c.Name).FirstOrDefault();
                var decrease = changes.Where(c => c.Change < 0).OrderBy(c => c.Change).ThenBy(c => c.Name).FirstOrDefault();
                var extremes = (increase.Name is null ? "No category increased." : $"Largest increase: {Quote(increase.Name)}, {Money(increase.Change)}.")
                    + " " + (decrease.Name is null ? "No category decreased." : $"Largest decrease: {Quote(decrease.Name)}, {Money(decrease.Change)}.");
                facts.Add(extremes); answers.Add(extremes);
            }
            if (plan.Comparison == AssistantChatComparison.FullPreviousMonth)
            { const string warning = "Comparison with the full previous month: the current period may contain fewer days."; facts.Add(warning); answers.Add(warning); }
        }
        if (plan.Measure == AssistantChatMeasure.Net || plan.Operation == AssistantChatOperation.Summary)
            answers.Add("Income minus expenses refers to recorded transactions and is not a bank balance.");
        if (periods.All(p => p.Items.Length == 0)) answers.Add("No transactions recorded in the selected scope.");
        return new(question, plan, currency, string.Join("\n", facts), string.Join("\n", answers),
            periods.Select(p => new AssistantChatPeriodData(p.From, p.Through, p.Items.Length, Measured(p.Items).Length)).ToArray());
    }
}
