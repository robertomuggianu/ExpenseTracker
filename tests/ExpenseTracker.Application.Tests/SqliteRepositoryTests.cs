using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using SQLite;

namespace ExpenseTracker.Application.Tests;

public sealed class SqliteRepositoryTests
{
    [Fact]
    public async Task VersionSix_AddsDefaultAccountIconAndPreservesExistingCategories()
    {
        var path = Path.Combine(Path.GetTempPath(), $"expense-account-icon-{Guid.NewGuid():N}.db3");
        var account = new Account(Guid.NewGuid(), "Banca", 25m, isPrimary: true);
        var category = new Category(Guid.NewGuid(), "Groceries", TransactionType.Expense, "#123456");
        try
        {
            await using (var original = new SqliteExpenseRepository(path))
            {
                await original.SaveAccountsAsync([account]);
                await original.SaveCategoryAsync(category);
            }
            using (var raw = new SQLiteConnection(path))
            {
                raw.Execute("ALTER TABLE Accounts DROP COLUMN Icon;");
                raw.Execute("PRAGMA user_version = 6;");
            }

            await using (var upgraded = new SqliteExpenseRepository(path))
            {
                Assert.Equal("wallet", (await upgraded.GetAccountAsync(account.Id))!.Icon);
                Assert.Equal(category.Name, (await upgraded.GetCategoryAsync(category.Id))!.Name);
                var service = new AccountService(upgraded);
                await service.SaveAsync(new(account.Id, account.Name, 25m, "EUR", true, true, "bank"));
                await service.SaveAsync(new(account.Id, "Banca rinominata", 25m, "EUR", true, true));
            }
            await using var reopened = new SqliteExpenseRepository(path);
            var saved = (await reopened.GetAccountAsync(account.Id))!;
            Assert.Equal("bank", saved.Icon);
            Assert.Equal(25m, saved.OpeningBalance);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task DeleteCategory_ReassignsAllReferencesAndKeepsDefaultDeletedAfterRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"expense-delete-category-{Guid.NewGuid():N}.db3");
        var account = new Account(Guid.NewGuid(), "Conto", 100m, isPrimary: true);
        var month = new DateTime(2026, 1, 1);
        Guid sourceId, targetId, unusedId, movementId;
        try
        {
            await using (var repo = new SqliteExpenseRepository(path))
            {
                await repo.SaveAccountsAsync([account]);
                var planning = new PlanningService(repo);
                var service = new CategoryService(repo, planning);
                var categories = await service.GetCategoriesAsync();
                sourceId = categories.Single(c => c.Name == "Affitto").Id;
                targetId = categories.Single(c => c.Name == "Bollette").Id;
                unusedId = categories.Single(c => c.Name == "Benzina").Id;
                movementId = Guid.NewGuid();
                await repo.SaveTransactionAsync(new(movementId, DateTime.UtcNow, TransactionType.Expense,
                    account.Id, sourceId, 10m, description: "Storico"));
                await planning.SaveRuleAsync(new(Guid.NewGuid(), "Regola", account.Id, sourceId,
                    TransactionType.Expense, 30m, "EUR", month, RepeatFrequency.Monthly));
                await planning.SaveBudgetAsync(sourceId, "EUR", month, 75m);
                var impact = await service.GetDeletionImpactAsync(sourceId);
                Assert.Equal(new CategoryDeletionImpact(1, 1, 1), impact);
                await Assert.ThrowsAsync<DomainValidationException>(() => service.DeleteAsync(sourceId, null, impact));
                var income = categories.Single(c => c.Type == TransactionType.Income);
                await Assert.ThrowsAsync<DomainValidationException>(() => service.DeleteAsync(sourceId, income.Id, impact));

                await planning.SaveBudgetAsync(targetId, "EUR", month, 50m);
                await Assert.ThrowsAsync<DomainValidationException>(() => service.DeleteAsync(sourceId, targetId, impact));
                Assert.NotNull(await repo.GetCategoryAsync(sourceId));
                Assert.Equal(sourceId, (await repo.GetTransactionAsync(movementId))!.CategoryId);
                var duplicate = (await planning.GetAsync()).Budgets.Single(b => b.CategoryId == targetId);
                await planning.RemoveBudgetAsync(duplicate.Id);

                await service.DeleteAsync(sourceId, targetId, impact);
                await service.DeleteAsync(unusedId, null, await service.GetDeletionImpactAsync(unusedId));
            }

            await using var reopened = new SqliteExpenseRepository(path);
            Assert.Null(await reopened.GetCategoryAsync(sourceId));
            Assert.Null(await reopened.GetCategoryAsync(unusedId));
            var movement = (await reopened.GetTransactionAsync(movementId))!;
            Assert.Equal(targetId, movement.CategoryId);
            Assert.Equal(10m, movement.Amount);
            Assert.Equal("Storico", movement.Description);
            var state = await new PlanningService(reopened).GetAsync();
            Assert.Equal(targetId, Assert.Single(state.Rules).CategoryId);
            var budget = Assert.Single(state.Budgets);
            Assert.Equal(targetId, budget.CategoryId);
            Assert.Equal(75m, budget.Limit);
        }
        finally { File.Delete(path); }
    }

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
            Assert.Equal(invalidData ? 2 : 7, check.ExecuteScalar<int>("PRAGMA user_version;"));
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

                Assert.Equal(["Affitto", "Benzina", "Bollette", "Carburante", "Manutenzione", "Ristoranti", "Spesa",
                    "Spese per macchina", "Strumenti", "Trasporto pubblico", "Uscite"],
                    firstCategories.Where(item => item.Type == TransactionType.Expense).Select(item => item.Name));
                Assert.Equal("Retribuzione", Assert.Single(firstCategories, item => item.Type == TransactionType.Income).Name);
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
