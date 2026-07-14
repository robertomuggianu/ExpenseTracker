using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using SQLite;

namespace ExpenseTracker.Infrastructure;

public sealed class SqliteExpenseRepository : IExpenseRepository, IAsyncDisposable
{
    private const int CurrentSchemaVersion = 1;
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
            if (version < 1)
            {
                await ApplyVersionOneAsync();
                await _connection.ExecuteAsync($"PRAGMA user_version = {CurrentSchemaVersion};");
            }

            if (version > CurrentSchemaVersion)
            {
                throw new InvalidOperationException($"Versione database {version} non supportata dall'applicazione.");
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

    public async Task<IReadOnlyList<MacroCategory>> GetMacroCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        var records = await _connection.Table<MacroCategoryRecord>().OrderBy(item => item.Type).ThenBy(item => item.Name).ToListAsync();
        return records.Select(ToDomain).ToArray();
    }

    public async Task<MacroCategory?> GetMacroCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        var record = await _connection.FindAsync<MacroCategoryRecord>(id.ToString("D"));
        return record is null ? null : ToDomain(record);
    }

    public async Task SaveMacroCategoryAsync(MacroCategory macroCategory, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        var record = ToRecord(macroCategory);
        if (await _connection.FindAsync<MacroCategoryRecord>(record.Id) is null)
        {
            await _connection.InsertAsync(record);
        }
        else
        {
            await _connection.UpdateAsync(record);
        }
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(
        Guid? macroCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        var query = _connection.Table<CategoryRecord>();
        if (macroCategoryId.HasValue)
        {
            var id = macroCategoryId.Value.ToString("D");
            query = query.Where(item => item.MacroCategoryId == id);
        }

        var records = await query.OrderBy(item => item.Name).ToListAsync();
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

        if (filter?.MacroCategoryId is not null)
        {
            sql += " AND CategoryId IN (SELECT Id FROM Categories WHERE MacroCategoryId = ?)";
            arguments.Add(filter.MacroCategoryId.Value.ToString("D"));
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

    private async Task SeedCategoriesAsync()
    {
        if (await _connection.Table<MacroCategoryRecord>().CountAsync() > 0)
        {
            return;
        }

        var home = new MacroCategoryRecord { Id = "10111111-1111-1111-1111-111111111111", Name = "Casa", Type = 2, Color = "#7C4DFF", IsActive = true };
        var transport = new MacroCategoryRecord { Id = "10222222-2222-2222-2222-222222222222", Name = "Trasporti", Type = 2, Color = "#0288D1", IsActive = true };
        var food = new MacroCategoryRecord { Id = "10333333-3333-3333-3333-333333333333", Name = "Alimentazione", Type = 2, Color = "#F57C00", IsActive = true };
        var work = new MacroCategoryRecord { Id = "10444444-4444-4444-4444-444444444444", Name = "Lavoro", Type = 2, Color = "#455A64", IsActive = true };
        var salary = new MacroCategoryRecord { Id = "10555555-5555-5555-5555-555555555555", Name = "Stipendio", Type = 1, Color = "#2E7D32", IsActive = true };

        var categories = new[]
        {
            Category("20111111-1111-1111-1111-111111111111", home.Id, "Affitto"),
            Category("20211111-1111-1111-1111-111111111111", home.Id, "Bollette"),
            Category("20311111-1111-1111-1111-111111111111", home.Id, "Manutenzione"),
            Category("20122222-2222-2222-2222-222222222222", transport.Id, "Carburante"),
            Category("20222222-2222-2222-2222-222222222222", transport.Id, "Trasporto pubblico"),
            Category("20133333-3333-3333-3333-333333333333", food.Id, "Spesa"),
            Category("20233333-3333-3333-3333-333333333333", food.Id, "Ristoranti"),
            Category("20144444-4444-4444-4444-444444444444", work.Id, "Strumenti"),
            Category("20155555-5555-5555-5555-555555555555", salary.Id, "Retribuzione")
        };

        await _connection.RunInTransactionAsync(database =>
        {
            database.InsertAll(new[] { home, transport, food, work, salary });
            database.InsertAll(categories);
        });

        static CategoryRecord Category(string id, string macroId, string name) =>
            new() { Id = id, MacroCategoryId = macroId, Name = name, IsActive = true };
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

    private static MacroCategoryRecord ToRecord(MacroCategory item) => new()
    {
        Id = item.Id.ToString("D"), Name = item.Name, Type = (int)item.Type, Color = item.Color, IsActive = item.IsActive
    };

    private static MacroCategory ToDomain(MacroCategoryRecord item) =>
        new(Guid.Parse(item.Id), item.Name, (TransactionType)item.Type, item.Color, item.IsActive);

    private static CategoryRecord ToRecord(Category item) => new()
    {
        Id = item.Id.ToString("D"), MacroCategoryId = item.MacroCategoryId.ToString("D"), Name = item.Name, IsActive = item.IsActive
    };

    private static Category ToDomain(CategoryRecord item) =>
        new(Guid.Parse(item.Id), Guid.Parse(item.MacroCategoryId), item.Name, item.IsActive);

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
