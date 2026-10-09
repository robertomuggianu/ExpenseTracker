using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public enum RepeatFrequency { Weekly, Monthly, Yearly }
public sealed record RecurringRule(Guid Id, string Name, Guid AccountId, Guid CategoryId, TransactionType Type,
    decimal Amount, string Currency, DateTime StartDate, RepeatFrequency Frequency, int NextIndex = 0, bool IsActive = true)
{
    public DateTime NextDate => Frequency switch
    {
        RepeatFrequency.Weekly => StartDate.Date.AddDays(7 * NextIndex),
        RepeatFrequency.Monthly => StartDate.Date.AddMonths(NextIndex),
        _ => StartDate.Date.AddYears(NextIndex)
    };
}
public sealed record MonthlyBudget(Guid Id, Guid CategoryId, string Currency, DateTime Month, decimal Limit);
public sealed record PlanningState(List<RecurringRule> Rules, List<MonthlyBudget> Budgets);
public sealed record BudgetProgress(MonthlyBudget Budget, string CategoryName, decimal Spent)
{
    public decimal Remaining => Budget.Limit - Spent;
    public double Progress => (double)Math.Clamp(Spent / Budget.Limit, 0, 1);
}

public sealed class PlanningService(IExpenseRepository repository)
{
    private const string Key = "planning-v1";
    private readonly SemaphoreSlim gate = new(1, 1);
    public async Task<PlanningState> GetAsync() => JsonSerializer.Deserialize<PlanningState>(await repository.GetStateAsync(Key) ?? "null") ?? new([], []);

    public async Task SaveRuleAsync(RecurringRule rule)
    {
        await gate.WaitAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(rule.Name)) throw new DomainValidationException("Inserisci il nome della ricorrenza.");
            if (rule.Id == Guid.Empty || !Enum.IsDefined(rule.Frequency) || rule.NextIndex < 0)
                throw new DomainValidationException("La ricorrenza contiene un calendario non valido.");
            await ValidateAsync(rule.AccountId, rule.CategoryId, rule.Type, rule.Currency);
            if (rule.Amount <= 0 || decimal.Round(rule.Amount, 2) != rule.Amount) throw new DomainValidationException("Inserisci un importo positivo con al massimo due decimali.");
            var state = await GetAsync();
            if (state.Rules.FirstOrDefault(r => r.Id == rule.Id) is { } existing)
            {
                if (existing.StartDate != rule.StartDate || existing.Frequency != rule.Frequency)
                    throw new DomainValidationException("Per cambiare il calendario, sospendi la ricorrenza e creane una nuova.");
                // A form opened before confirmation/pause must not rewind or reactivate the rule.
                rule = rule with { NextIndex = existing.NextIndex, IsActive = existing.IsActive };
            }
            state.Rules.RemoveAll(r => r.Id == rule.Id);
            state.Rules.Add(rule);
            await repository.SaveStateAsync(Key, JsonSerializer.Serialize(state));
        }
        finally { gate.Release(); }
    }
    public async Task SetRuleActiveAsync(Guid id, bool active)
    {
        await gate.WaitAsync();
        try
        {
            var state = await GetAsync();
            var index = state.Rules.FindIndex(r => r.Id == id);
            if (index >= 0) state.Rules[index] = state.Rules[index] with { IsActive = active };
            await repository.SaveStateAsync(Key, JsonSerializer.Serialize(state));
        }
        finally { gate.Release(); }
    }
    public async Task RemoveRuleAsync(Guid id)
    {
        await gate.WaitAsync();
        try { var state = await GetAsync(); state.Rules.RemoveAll(r => r.Id == id); await repository.SaveStateAsync(Key, JsonSerializer.Serialize(state)); }
        finally { gate.Release(); }
    }
    public async Task ConfirmAsync(Guid ruleId, DateTime expectedDate, DateTime today)
    {
        await gate.WaitAsync();
        try
        {
            var state = await GetAsync();
            var index = state.Rules.FindIndex(r => r.Id == ruleId);
            if (index < 0) throw new DomainValidationException("Ricorrenza non trovata.");
            var rule = state.Rules[index];
            if (!rule.IsActive || rule.NextDate != expectedDate.Date || rule.NextDate > today.Date)
                throw new DomainValidationException("Scadenza già gestita, sospesa o non ancora dovuta. Aggiorna l'elenco.");
            await ValidateAsync(rule.AccountId, rule.CategoryId, rule.Type, rule.Currency);
            var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"recurring|{rule.Id:D}|{rule.NextDate:yyyy-MM-dd}")).AsSpan(0, 16));
            if (await repository.GetTransactionAsync(id) is not null) throw new DomainValidationException("Questo movimento è già stato registrato.");
            var transaction = new Transaction(id, DateTime.SpecifyKind(rule.NextDate.AddHours(12), DateTimeKind.Local).ToUniversalTime(),
                rule.Type, rule.AccountId, rule.CategoryId, rule.Amount, rule.Currency, rule.Name, "Da ricorrenza confermata");
            state.Rules[index] = rule with { NextIndex = rule.NextIndex + 1 };
            await repository.SaveBatchAsync([], [], [transaction], Key, JsonSerializer.Serialize(state));
        }
        finally { gate.Release(); }
    }
    public async Task SkipAsync(Guid ruleId, DateTime expectedDate)
    {
        await gate.WaitAsync();
        try
        {
            var state = await GetAsync(); var index = state.Rules.FindIndex(r => r.Id == ruleId);
            if (index < 0 || state.Rules[index].NextDate != expectedDate.Date) throw new DomainValidationException("Scadenza già gestita.");
            state.Rules[index] = state.Rules[index] with { NextIndex = state.Rules[index].NextIndex + 1 };
            await repository.SaveStateAsync(Key, JsonSerializer.Serialize(state));
        }
        finally { gate.Release(); }
    }
    public async Task SaveBudgetAsync(Guid categoryId, string currency, DateTime month, decimal limit)
    {
        await gate.WaitAsync();
        try
        {
            var category = await repository.GetCategoryAsync(categoryId);
            if (category is null || category.Type != TransactionType.Expense) throw new DomainValidationException("Scegli una categoria di spesa.");
            if (limit <= 0 || decimal.Round(limit, 2) != limit) throw new DomainValidationException("Inserisci un limite positivo con al massimo due decimali.");
            if (!(await repository.GetAccountsAsync()).Any(a => a.Currency == currency)) throw new DomainValidationException("Scegli una valuta dei tuoi conti.");
            month = new DateTime(month.Year, month.Month, 1);
            var state = await GetAsync();
            state.Budgets.RemoveAll(b => b.CategoryId == categoryId && b.Currency == currency && b.Month == month);
            state.Budgets.Add(new(Guid.NewGuid(), categoryId, currency, month, limit));
            await repository.SaveStateAsync(Key, JsonSerializer.Serialize(state));
        }
        finally { gate.Release(); }
    }
    public async Task RemoveBudgetAsync(Guid id)
    {
        await gate.WaitAsync();
        try { var state = await GetAsync(); state.Budgets.RemoveAll(b => b.Id == id); await repository.SaveStateAsync(Key, JsonSerializer.Serialize(state)); }
        finally { gate.Release(); }
    }
    public async Task<IReadOnlyList<BudgetProgress>> GetBudgetsAsync(DateTime month)
    {
        var state = await GetAsync(); var range = MonthlyPeriods.FullMonth(month);
        var transactions = await repository.GetTransactionsAsync(new(range.FromUtc, range.ToUtcExclusive, Type: TransactionType.Expense));
        var categories = (await repository.GetCategoriesAsync()).ToDictionary(c => c.Id);
        return state.Budgets.Where(b => b.Month.Year == month.Year && b.Month.Month == month.Month)
            .Select(b => new BudgetProgress(b, categories.GetValueOrDefault(b.CategoryId)?.Name ?? "Categoria archiviata",
                transactions.Where(t => t.TransferId is null && t.CategoryId == b.CategoryId && t.Currency == b.Currency).Sum(t => t.Amount))).ToArray();
    }
    private async Task ValidateAsync(Guid accountId, Guid categoryId, TransactionType type, string currency)
    {
        var account = await repository.GetAccountAsync(accountId); var category = await repository.GetCategoryAsync(categoryId);
        if (account is null || !account.IsActive || category is null || !category.IsActive) throw new DomainValidationException("Seleziona un conto e una categoria attivi.");
        if (account.Currency != currency) throw new DomainValidationException("La valuta deve coincidere con quella del conto.");
        TransactionRules.EnsureCategoryCompatible(type, category);
    }
}

public sealed class TransferService(IExpenseRepository repository)
{
    public async Task SaveAsync(Guid sourceId, Guid destinationId, decimal amount, DateTime date, string? notes)
    {
        if (sourceId == destinationId) throw new DomainValidationException("Scegli due conti diversi.");
        var accounts = await repository.GetAccountsAsync();
        var source = accounts.FirstOrDefault(a => a.Id == sourceId && a.IsActive);
        var destination = accounts.FirstOrDefault(a => a.Id == destinationId && a.IsActive);
        if (source is null || destination is null) throw new DomainValidationException("Scegli due conti attivi.");
        if (source.Currency != destination.Currency) throw new DomainValidationException("I trasferimenti richiedono conti nella stessa valuta.");
        var categories = await repository.GetCategoriesAsync();
        var sent = categories.FirstOrDefault(c => c.Name == "Trasferimento inviato" && c.Type == TransactionType.Expense)
            ?? new Category(Guid.NewGuid(), "Trasferimento inviato", TransactionType.Expense, "#80D9BE");
        var received = categories.FirstOrDefault(c => c.Name == "Trasferimento ricevuto" && c.Type == TransactionType.Income)
            ?? new Category(Guid.NewGuid(), "Trasferimento ricevuto", TransactionType.Income, "#80D9BE");
        var transferId = Guid.NewGuid();
        var utc = DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();
        Transaction[] pair = [
            new(Guid.NewGuid(), utc, TransactionType.Expense, sourceId, sent.Id, amount, source.Currency, $"Verso {destination.Name}", notes, transferId: transferId),
            new(Guid.NewGuid(), utc, TransactionType.Income, destinationId, received.Id, amount, source.Currency, $"Da {source.Name}", notes, transferId: transferId)];
        await repository.SaveBatchAsync([], [sent, received], pair);
    }
}
