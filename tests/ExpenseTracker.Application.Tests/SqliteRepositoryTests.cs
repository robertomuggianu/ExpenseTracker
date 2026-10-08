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
