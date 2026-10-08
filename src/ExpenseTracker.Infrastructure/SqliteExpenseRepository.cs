using System.Security.Cryptography;
using System.Text;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using SQLite;

namespace ExpenseTracker.Infrastructure;

public sealed class SqliteExpenseRepository : IExpenseRepository, IAsyncDisposable
{
    private const int CurrentSchemaVersion = 2;
    private readonly SQLiteAsyncConnection _connection;
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private bool _initialized;

    public SqliteExpenseRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var flags = SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache | SQLiteOpenFlags.FullMutex;
        _connection = new SQLiteAsyncConnection(databasePath, flags, storeDateTimeAsTicks: true);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        await _initializationGate.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await _connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
            var version = await _connection.ExecuteScalarAsync<int>("PRAGMA user_version;");
            if (version > CurrentSchemaVersion)
            {
                throw new InvalidOperationException($"Versione database {version} non supportata dall'applicazione.");
            }

            if (version < 1)
            {
                await ApplyVersionOneAsync();
            }

            if (version < 2)
            {
                await ApplyVersionTwoAsync();
            }

            if (version < CurrentSchemaVersion)
            {
                await _connection.ExecuteAsync($"PRAGMA user_version = {CurrentSchemaVersion};");
            }

            await SeedCategoriesAsync();
            _initialized = true;
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public async Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        var records = await _connection.Table<AccountRecord>().OrderByDescending(item => item.IsPrimary).ThenBy(item => item.Name).ToListAsync();
        return records.Select(ToDomain).ToArray();
    }

    public async Task<Account?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        var record = await _connection.FindAsync<AccountRecord>(id.ToString("D"));
        return record is null ? null : ToDomain(record);
    }

    public async Task SaveAccountsAsync(IReadOnlyCollection<Account> accounts, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var records = accounts.Select(ToRecord).ToArray();
        await _connection.RunInTransactionAsync(database =>
        {
            foreach (var record in records)
            {
                if (database.Find<AccountRecord>(record.Id) is null)
                {
                    database.Insert(record);
                }
                else
                {
                    database.Update(record);
                }
            }
        });
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        var records = await _connection.Table<CategoryRecord>().OrderBy(item => item.Type).ThenBy(item => item.Name).ToListAsync();
        return records.Select(ToDomain).ToArray();
    }

    public async Task<Category?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        var record = await _connection.FindAsync<CategoryRecord>(id.ToString("D"));
        return record is null ? null : ToDomain(record);
    }

    public async Task SaveCategoryAsync(Category category, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        var record = ToRecord(category);
        if (await _connection.FindAsync<CategoryRecord>(record.Id) is null)
        {
            await _connection.InsertAsync(record);
        }
        else
        {
            await _connection.UpdateAsync(record);
        }
    }

    public async Task<IReadOnlyList<Transaction>> GetTransactionsAsync(
        TransactionFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var sql = "SELECT * FROM Transactions WHERE 1 = 1";
        var arguments = new List<object>();
        if (filter?.FromUtc is not null)
        {
            sql += " AND OccurredAtUtc >= ?";
            arguments.Add(filter.FromUtc.Value.Ticks);
        }

        if (filter?.ToUtcExclusive is not null)
        {
            sql += " AND OccurredAtUtc < ?";
            arguments.Add(filter.ToUtcExclusive.Value.Ticks);
        }

        if (filter?.AccountId is not null)
        {
            sql += " AND AccountId = ?";
            arguments.Add(filter.AccountId.Value.ToString("D"));
        }

        if (filter?.Type is not null)
        {
            sql += " AND Type = ?";
            arguments.Add((int)filter.Type.Value);
        }

        if (filter?.CategoryId is not null)
        {
            sql += " AND CategoryId = ?";
            arguments.Add(filter.CategoryId.Value.ToString("D"));
        }

        sql += " ORDER BY OccurredAtUtc DESC";
        var records = await _connection.QueryAsync<TransactionRecord>(sql, arguments.ToArray());
        return records.Select(ToDomain).ToArray();
    }

    public async Task<Transaction?> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        var record = await _connection.FindAsync<TransactionRecord>(id.ToString("D"));
        return record is null ? null : ToDomain(record);
    }

    public async Task SaveTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        var record = ToRecord(transaction);
        if (await _connection.FindAsync<TransactionRecord>(record.Id) is null)
        {
            await _connection.InsertAsync(record);
        }
        else
        {
            await _connection.UpdateAsync(record);
        }
    }

    public async Task DeleteTransactionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await _connection.DeleteAsync<TransactionRecord>(id.ToString("D"));
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.CloseAsync();
        _initializationGate.Dispose();
    }

    private Task EnsureReadyAsync(CancellationToken cancellationToken) => InitializeAsync(cancellationToken);

    private async Task ApplyVersionOneAsync()
    {
        await _connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS Accounts (
                Id TEXT PRIMARY KEY NOT NULL,
                Name TEXT NOT NULL,
                OpeningBalanceMinor INTEGER NOT NULL,
                Currency TEXT NOT NULL,
                IsPrimary INTEGER NOT NULL,
                IsActive INTEGER NOT NULL,
                CreatedAtUtc INTEGER NOT NULL,
                UpdatedAtUtc INTEGER NOT NULL
            );
            """);
        await _connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS MacroCategories (
                Id TEXT PRIMARY KEY NOT NULL,
                Name TEXT NOT NULL,
                Type INTEGER NOT NULL CHECK (Type IN (1, 2)),
                Color TEXT NOT NULL,
                IsActive INTEGER NOT NULL
            );
            """);
        await _connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS Categories (
                Id TEXT PRIMARY KEY NOT NULL,
                MacroCategoryId TEXT NOT NULL,
                Name TEXT NOT NULL,
                IsActive INTEGER NOT NULL,
                FOREIGN KEY (MacroCategoryId) REFERENCES MacroCategories (Id) ON DELETE RESTRICT
            );
            """);
        await _connection.ExecuteAsync(
            """
            CREATE TABLE IF NOT EXISTS Transactions (
                Id TEXT PRIMARY KEY NOT NULL,
                OccurredAtUtc INTEGER NOT NULL,
                Type INTEGER NOT NULL CHECK (Type IN (1, 2)),
                AccountId TEXT NOT NULL,
                CategoryId TEXT NOT NULL,
                Description TEXT NULL,
                AmountMinor INTEGER NOT NULL CHECK (AmountMinor > 0),
                Currency TEXT NOT NULL,
                Notes TEXT NULL,
                CreatedAtUtc INTEGER NOT NULL,
                UpdatedAtUtc INTEGER NOT NULL,
                FOREIGN KEY (AccountId) REFERENCES Accounts (Id) ON DELETE RESTRICT,
                FOREIGN KEY (CategoryId) REFERENCES Categories (Id) ON DELETE RESTRICT
            );
            """);
        await _connection.ExecuteAsync("CREATE UNIQUE INDEX IF NOT EXISTS UX_Accounts_OnePrimary ON Accounts (IsPrimary) WHERE IsPrimary = 1;");
        await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Categories_MacroCategoryId ON Categories (MacroCategoryId);");
        await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Transactions_OccurredAtUtc ON Transactions (OccurredAtUtc DESC);");
        await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Transactions_AccountId ON Transactions (AccountId);");
        await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Transactions_CategoryId ON Transactions (CategoryId);");
    }

    private async Task ApplyVersionTwoAsync()
    {
        await _connection.ExecuteAsync("PRAGMA foreign_keys = OFF;");
        try
        {
            await _connection.ExecuteAsync(
                """
                CREATE TABLE CategoriesV2 (
                    Id TEXT PRIMARY KEY NOT NULL,
                    Name TEXT NOT NULL,
                    Type INTEGER NOT NULL CHECK (Type IN (1, 2)),
                    Color TEXT NOT NULL,
                    IsActive INTEGER NOT NULL
                );
                """);
            await _connection.ExecuteAsync(
                """
                INSERT INTO CategoriesV2 (Id, Name, Type, Color, IsActive)
                SELECT c.Id, c.Name, m.Type, m.Color,
                       CASE WHEN c.IsActive = 1 AND m.IsActive = 1 THEN 1 ELSE 0 END
                FROM Categories c
                INNER JOIN MacroCategories m ON m.Id = c.MacroCategoryId;
                """);
            await _connection.ExecuteAsync(
                """
                CREATE TABLE TransactionsV2 (
                    Id TEXT PRIMARY KEY NOT NULL,
                    OccurredAtUtc INTEGER NOT NULL,
                    Type INTEGER NOT NULL CHECK (Type IN (1, 2)),
                    AccountId TEXT NOT NULL,
                    CategoryId TEXT NOT NULL,
                    Description TEXT NULL,
                    AmountMinor INTEGER NOT NULL CHECK (AmountMinor > 0),
                    Currency TEXT NOT NULL,
                    Notes TEXT NULL,
                    CreatedAtUtc INTEGER NOT NULL,
                    UpdatedAtUtc INTEGER NOT NULL,
                    FOREIGN KEY (AccountId) REFERENCES Accounts (Id) ON DELETE RESTRICT,
                    FOREIGN KEY (CategoryId) REFERENCES CategoriesV2 (Id) ON DELETE RESTRICT
                );
                """);
            await _connection.ExecuteAsync(
                """
                INSERT INTO TransactionsV2
                SELECT Id, OccurredAtUtc, Type, AccountId, CategoryId, Description,
                       AmountMinor, Currency, Notes, CreatedAtUtc, UpdatedAtUtc
                FROM Transactions;
                """);
            await _connection.ExecuteAsync("DROP TABLE Transactions;");
            await _connection.ExecuteAsync("DROP TABLE Categories;");
            await _connection.ExecuteAsync("DROP TABLE MacroCategories;");
            await _connection.ExecuteAsync("ALTER TABLE CategoriesV2 RENAME TO Categories;");
            await _connection.ExecuteAsync("ALTER TABLE TransactionsV2 RENAME TO Transactions;");
            await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Categories_Type ON Categories (Type);");
            await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Transactions_OccurredAtUtc ON Transactions (OccurredAtUtc DESC);");
            await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Transactions_AccountId ON Transactions (AccountId);");
            await _connection.ExecuteAsync("CREATE INDEX IF NOT EXISTS IX_Transactions_CategoryId ON Transactions (CategoryId);");
        }
        finally
        {
            await _connection.ExecuteAsync("PRAGMA foreign_keys = ON;");
        }
    }

    private async Task SeedCategoriesAsync()
    {
        (string? Id, string Name, int Type, string Color)[] definitions =
        [
            ("20111111-1111-1111-1111-111111111111", "Affitto", 2, "#E2383F"),
            ("20211111-1111-1111-1111-111111111111", "Bollette", 2, "#E2383F"),
            ("20311111-1111-1111-1111-111111111111", "Manutenzione", 2, "#E2383F"),
            ("20122222-2222-2222-2222-222222222222", "Carburante", 2, "#5AA9FF"),
            ("20222222-2222-2222-2222-222222222222", "Trasporto pubblico", 2, "#5AA9FF"),
            ("20133333-3333-3333-3333-333333333333", "Spesa", 2, "#F28C45"),
            ("20233333-3333-3333-3333-333333333333", "Ristoranti", 2, "#F28C45"),
            ("20144444-4444-4444-4444-444444444444", "Strumenti", 2, "#6F7D91"),
            ("20155555-5555-5555-5555-555555555555", "Retribuzione", 1, "#36C88A"),
            (null, "Benzina", 2, "#F4B942"),
            (null, "Cafe", 2, "#F28C45"),
            (null, "Education", 2, "#5AA9FF"),
            (null, "Gifts", 2, "#F477B3"),
            (null, "Groceries", 2, "#7DBB52"),
            (null, "Health", 2, "#E2383F"),
            (null, "Home", 2, "#FF6B70"),
            (null, "Leisure", 2, "#35B5B0"),
            (null, "Other", 2, "#9B8B7A"),
            (null, "Restaurants", 2, "#F28C45"),
            (null, "Spese per macchina", 2, "#6F7D91"),
            (null, "Subscription", 2, "#9C7CFF"),
            (null, "Transportation", 2, "#5AA9FF"),
            (null, "Uscite", 2, "#E2383F"),
            (null, "Vape", 2, "#B76E79"),
            (null, "Gift", 1, "#36C88A"),
            (null, "Other", 1, "#35B5B0"),
            (null, "Paycheck", 1, "#36C88A")
        ];

        var existing = await _connection.Table<CategoryRecord>().ToListAsync();
        foreach (var definition in definitions)
        {
            if (existing.Any(item =>
                    item.Type == definition.Type
                    && string.Equals(item.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var category = new CategoryRecord
            {
                Id = definition.Id ?? StableGuid($"reference-category|{definition.Type}|{definition.Name.ToUpperInvariant()}").ToString("D"),
                Name = definition.Name,
                Type = definition.Type,
                Color = definition.Color,
                IsActive = true
            };
            await _connection.InsertAsync(category);
            existing.Add(category);
        }
    }

    private static Guid StableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static long ToMinorUnits(decimal value) =>
        checked((long)decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));

    private static decimal FromMinorUnits(long value) => value / 100m;

    private static AccountRecord ToRecord(Account item) => new()
    {
        Id = item.Id.ToString("D"),
        Name = item.Name,
        OpeningBalanceMinor = ToMinorUnits(item.OpeningBalance),
        Currency = item.Currency,
        IsPrimary = item.IsPrimary,
        IsActive = item.IsActive,
        CreatedAtUtc = item.CreatedAtUtc,
        UpdatedAtUtc = item.UpdatedAtUtc
    };

    private static Account ToDomain(AccountRecord item) => new(
        Guid.Parse(item.Id), item.Name, FromMinorUnits(item.OpeningBalanceMinor), item.Currency,
        item.IsPrimary, item.IsActive, item.CreatedAtUtc, item.UpdatedAtUtc);

    private static CategoryRecord ToRecord(Category item) => new()
    {
        Id = item.Id.ToString("D"), Name = item.Name, Type = (int)item.Type, Color = item.Color, IsActive = item.IsActive
    };

    private static Category ToDomain(CategoryRecord item) =>
        new(Guid.Parse(item.Id), item.Name, (TransactionType)item.Type, item.Color, item.IsActive);

    private static TransactionRecord ToRecord(Transaction item) => new()
    {
        Id = item.Id.ToString("D"),
        OccurredAtUtc = item.OccurredAtUtc,
        Type = (int)item.Type,
        AccountId = item.AccountId.ToString("D"),
        CategoryId = item.CategoryId.ToString("D"),
        Description = item.Description,
        AmountMinor = ToMinorUnits(item.Amount),
        Currency = item.Currency,
        Notes = item.Notes,
        CreatedAtUtc = item.CreatedAtUtc,
        UpdatedAtUtc = item.UpdatedAtUtc
    };

    private static Transaction ToDomain(TransactionRecord item) => new(
        Guid.Parse(item.Id), item.OccurredAtUtc, (TransactionType)item.Type,
        Guid.Parse(item.AccountId), Guid.Parse(item.CategoryId), FromMinorUnits(item.AmountMinor),
        item.Currency, item.Description, item.Notes, item.CreatedAtUtc, item.UpdatedAtUtc);
}
