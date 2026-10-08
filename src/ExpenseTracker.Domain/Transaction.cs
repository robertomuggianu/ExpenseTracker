namespace ExpenseTracker.Domain;

public sealed class Transaction
{
    public Transaction(
        Guid id,
        DateTime occurredAtUtc,
        TransactionType type,
        Guid accountId,
        Guid categoryId,
        decimal amount,
        string currency = "EUR",
        string? description = null,
        string? notes = null,
        DateTime? createdAtUtc = null,
        DateTime? updatedAtUtc = null)
    {
        if (id == Guid.Empty || accountId == Guid.Empty || categoryId == Guid.Empty)
        {
            throw new DomainValidationException("Movimento, conto e categoria sono obbligatori.");
        }

        Id = id;
        AccountId = accountId;
        CategoryId = categoryId;
        Update(occurredAtUtc, type, accountId, categoryId, amount, currency, description, notes);
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
        UpdatedAtUtc = updatedAtUtc ?? CreatedAtUtc;
    }

    public Guid Id { get; }

    public DateTime OccurredAtUtc { get; private set; }

    public TransactionType Type { get; private set; }

    public Guid AccountId { get; private set; }

    public Guid CategoryId { get; private set; }

    public string? Description { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = "EUR";

    public string? Notes { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(
        DateTime occurredAtUtc,
        TransactionType type,
        Guid accountId,
        Guid categoryId,
        decimal amount,
        string currency,
        string? description,
        string? notes)
    {
        if (accountId == Guid.Empty || categoryId == Guid.Empty)
        {
            throw new DomainValidationException("Conto e categoria sono obbligatori.");
        }

        if (amount <= 0)
        {
            throw new DomainValidationException("L'importo deve essere maggiore di zero.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new DomainValidationException("La valuta deve essere un codice di tre lettere.");
        }

        OccurredAtUtc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        Type = type;
        AccountId = accountId;
        CategoryId = categoryId;
        Amount = decimal.Round(Math.Abs(amount), 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public static class TransactionRules
{
    public static void EnsureCategoryCompatible(TransactionType type, Category category)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (category.Type != type)
        {
            throw new DomainValidationException("La categoria selezionata non è compatibile con il tipo di movimento.");
        }
    }
}

public static class AccountRules
{
    public static void SelectPrimary(IReadOnlyCollection<Account> accounts, Guid accountId)
    {
        ArgumentNullException.ThrowIfNull(accounts);

        var selected = accounts.SingleOrDefault(item => item.Id == accountId)
            ?? throw new DomainValidationException("Il conto selezionato non esiste.");

        if (!selected.IsActive)
        {
            throw new DomainValidationException("Il conto principale deve essere attivo.");
        }

        foreach (var account in accounts)
        {
            account.SetPrimary(account.Id == accountId);
        }
    }
}
