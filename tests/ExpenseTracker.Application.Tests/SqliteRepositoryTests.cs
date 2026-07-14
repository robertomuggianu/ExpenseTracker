using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;

namespace ExpenseTracker.Application.Tests;

public sealed class SqliteRepositoryTests
{
    [Fact]
    public async Task Initialization_IsIdempotent_SeedsCategoriesAndPersistsRelations()
    {
        var path = Path.Combine(Path.GetTempPath(), $"expense-tracker-{Guid.NewGuid():N}.db3");
        try
        {
            await using (var repository = new SqliteExpenseRepository(path))
            {
                await repository.InitializeAsync();
                var firstMacros = await repository.GetMacroCategoriesAsync();
                var firstCategories = await repository.GetCategoriesAsync();
                await repository.InitializeAsync();

                Assert.Equal(5, firstMacros.Count);
                Assert.Equal(9, firstCategories.Count);
                Assert.Equal(firstMacros.Count, (await repository.GetMacroCategoriesAsync()).Count);
                Assert.Equal(firstCategories.Count, (await repository.GetCategoriesAsync()).Count);

                var account = new Account(Guid.NewGuid(), "Test", 10m, isPrimary: true);
                await repository.SaveAccountsAsync([account]);
                var incomeMacro = firstMacros.Single(item => item.Type == TransactionType.Income);
                var incomeCategory = firstCategories.First(item => item.MacroCategoryId == incomeMacro.Id);
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
