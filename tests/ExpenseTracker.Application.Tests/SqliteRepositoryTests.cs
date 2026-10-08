using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using SQLite;

namespace ExpenseTracker.Application.Tests;

public sealed class SqliteRepositoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VersionTwo_RepairsDanglingCategoryReference_AndPreservesData(bool invalidData)
    {
        var path = Path.Combine(Path.GetTempPath(), $"expense-repair-{Guid.NewGuid():N}.db3");
        var account = new Account(Guid.NewGuid(), "Existing account", 12.34m, isPrimary: true);
        Transaction transaction;
        try
        {
            await using (var initial = new SqliteExpenseRepository(path))
            {
                var category = (await initial.GetCategoriesAsync()).First(item => item.Type == TransactionType.Income);
                await initial.SaveAccountsAsync([account]);
                transaction = new Transaction(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Income,
                    account.Id, category.Id, 25.50m, "EUR", "Preserve description", "Preserve notes");
                await initial.SaveTransactionAsync(transaction);
            }

            using (var raw = new SQLiteConnection(path))
            {
                raw.Execute("PRAGMA foreign_keys = OFF;");
                var sql = raw.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE name = 'Transactions';");
                raw.Execute(sql.Replace("\"Transactions\"", "BrokenTransactions")
                    .Replace("REFERENCES Categories (", "REFERENCES CategoriesV2 ("));
                raw.Execute("INSERT INTO BrokenTransactions SELECT * FROM Transactions;");
                raw.Execute("DROP TABLE Transactions;");
                raw.Execute("ALTER TABLE BrokenTransactions RENAME TO Transactions;");
                raw.Execute("PRAGMA user_version = 2;");
                raw.Execute("ALTER TABLE Categories DROP COLUMN Icon;");
                if (invalidData) raw.Execute("UPDATE Transactions SET CategoryId = 'missing';");
                raw.Execute("PRAGMA foreign_keys = ON;");
                var exception = Assert.Throws<SQLiteException>(() => raw.Execute("UPDATE Transactions SET CategoryId = CategoryId;"));
                Assert.Contains("CategoriesV2", exception.Message);
            }

            await using (var repaired = new SqliteExpenseRepository(path))
            {
                if (invalidData)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => repaired.InitializeAsync());
                }
                else
                {
                    await repaired.InitializeAsync();
                    var saved = Assert.Single(await repaired.GetTransactionsAsync());
                    Assert.Equal(transaction.Id, saved.Id);
                    Assert.Equal(transaction.Amount, saved.Amount);
                    Assert.Equal(transaction.CategoryId, saved.CategoryId);
                    Assert.Equal(transaction.Notes, saved.Notes);
                    Assert.Equal(transaction.Description, saved.Description);
                    Assert.Equal(account.OpeningBalance, (await repaired.GetAccountAsync(account.Id))!.OpeningBalance);
                    await repaired.SaveTransactionAsync(saved);
                    await repaired.SaveTransactionAsync(new Transaction(Guid.NewGuid(), DateTime.UtcNow,
                        saved.Type, saved.AccountId, saved.CategoryId, 10m));
                    await repaired.DeleteTransactionAsync(saved.Id);
                    Assert.Single(await repaired.GetTransactionsAsync());
                }
            }

            using var check = new SQLiteConnection(path);
            Assert.Equal(invalidData ? 2 : 4, check.ExecuteScalar<int>("PRAGMA user_version;"));
            Assert.Equal(1, check.ExecuteScalar<int>("SELECT COUNT(*) FROM Transactions;"));
            Assert.Equal(0, check.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'TransactionsV3';"));
            if (!invalidData)
                Assert.DoesNotContain("CategoriesV2", check.ExecuteScalar<string>("SELECT sql FROM sqlite_master WHERE name = 'Transactions';"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Initialization_IsIdempotent_SeedsCategoriesAndPersistsRelations(bool legacyRename)
    {
        var path = Path.Combine(Path.GetTempPath(), $"expense-tracker-{Guid.NewGuid():N}.db3");
        try
        {
            // Match the repository's pooled connection so the migration also runs
            // with the rename semantics that exposed the original iPhone bug.
            var setup = new SQLiteAsyncConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create
                | SQLiteOpenFlags.SharedCache | SQLiteOpenFlags.FullMutex, storeDateTimeAsTicks: true);
            await setup.ExecuteAsync($"PRAGMA legacy_alter_table = {(legacyRename ? "ON" : "OFF")};");
            await using (var repository = new SqliteExpenseRepository(path))
            {
                await repository.InitializeAsync();
                var firstCategories = await repository.GetCategoriesAsync();
                await repository.InitializeAsync();

                Assert.Equal(27, firstCategories.Count);
                (string Name, TransactionType Type)[] importedCategorySet =
                [
                    ("Benzina", TransactionType.Expense), ("Cafe", TransactionType.Expense),
                    ("Education", TransactionType.Expense), ("Gifts", TransactionType.Expense),
                    ("Groceries", TransactionType.Expense), ("Health", TransactionType.Expense),
                    ("Home", TransactionType.Expense), ("Leisure", TransactionType.Expense),
                    ("Other", TransactionType.Expense), ("Restaurants", TransactionType.Expense),
                    ("Spese per macchina", TransactionType.Expense), ("Subscription", TransactionType.Expense),
                    ("Transportation", TransactionType.Expense), ("Uscite", TransactionType.Expense),
                    ("Vape", TransactionType.Expense), ("Gift", TransactionType.Income),
                    ("Other", TransactionType.Income), ("Paycheck", TransactionType.Income)
                ];
                Assert.All(importedCategorySet, expected =>
                    Assert.Contains(firstCategories, item => item.Name == expected.Name && item.Type == expected.Type));
                Assert.Equal(firstCategories.Count, (await repository.GetCategoriesAsync()).Count);

                var account = new Account(Guid.NewGuid(), "Test", 10m, isPrimary: true);
                await repository.SaveAccountsAsync([account]);
                var incomeCategory = firstCategories.First(item => item.Type == TransactionType.Income);
                var transaction = new Transaction(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    TransactionType.Income,
                    account.Id,
                    incomeCategory.Id,
                    25.50m);
                await repository.SaveTransactionAsync(transaction);

                var saved = Assert.Single(await repository.GetTransactionsAsync());
                Assert.Equal(25.50m, saved.Amount);
                Assert.Equal(account.Id, saved.AccountId);
                Assert.Equal(incomeCategory.Id, saved.CategoryId);
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
