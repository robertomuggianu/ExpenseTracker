using SQLite;

namespace ExpenseTracker.Infrastructure;

[Table("Accounts")]
internal sealed class AccountRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public long OpeningBalanceMinor { get; set; }

    public string Currency { get; set; } = "EUR";

    public bool IsPrimary { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

[Table("Categories")]
internal sealed class CategoryRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Type { get; set; }

    public string Color { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

[Table("Transactions")]
internal sealed class TransactionRecord
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public int Type { get; set; }

    public string AccountId { get; set; } = string.Empty;

    public string CategoryId { get; set; } = string.Empty;

    public string? Description { get; set; }

    public long AmountMinor { get; set; }

    public string Currency { get; set; } = "EUR";

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
