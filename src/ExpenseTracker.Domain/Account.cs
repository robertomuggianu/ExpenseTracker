namespace ExpenseTracker.Domain;

public sealed class Account
{
    public Account(
        Guid id,
        string name,
        decimal openingBalance,
        string currency = "EUR",
        bool isPrimary = false,
        bool isActive = true,
        DateTime? createdAtUtc = null,
        DateTime? updatedAtUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("L'identificativo del conto è obbligatorio.");
        }

        Id = id;
        Update(name, openingBalance, currency);
        IsPrimary = isPrimary;
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
        UpdatedAtUtc = updatedAtUtc ?? CreatedAtUtc;
    }

    public Guid Id { get; }

    public string Name { get; private set; } = string.Empty;

    public decimal OpeningBalance { get; private set; }

    public string Currency { get; private set; } = "EUR";

    public bool IsPrimary { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; }

    public DateTime UpdatedAtUtc { get; private set; }

    public void Update(string name, decimal openingBalance, string currency)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Il nome del conto è obbligatorio.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new DomainValidationException("La valuta deve essere un codice di tre lettere.");
        }

        Name = name.Trim();
        OpeningBalance = decimal.Round(openingBalance, 2, MidpointRounding.AwayFromZero);
        Currency = currency.Trim().ToUpperInvariant();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetPrimary(bool value)
    {
        if (value && !IsActive)
        {
            throw new DomainValidationException("Un conto non attivo non può essere impostato come principale.");
        }

        IsPrimary = value;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetActive(bool value)
    {
        if (!value && IsPrimary)
        {
            throw new DomainValidationException("Seleziona un altro conto principale prima di disattivare questo conto.");
        }

        IsActive = value;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public decimal CalculateCurrentBalance(IEnumerable<Transaction> transactions)
    {
        ArgumentNullException.ThrowIfNull(transactions);

        return transactions
            .Where(item => item.AccountId == Id)
            .Aggregate(
                OpeningBalance,
                (balance, item) => item.Type == TransactionType.Income
                    ? balance + item.Amount
                    : balance - item.Amount);
    }
}
