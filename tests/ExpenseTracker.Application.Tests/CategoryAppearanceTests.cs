using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using SQLite;

namespace ExpenseTracker.Application.Tests;

public sealed class CategoryAppearanceTests
{
    [Fact]
    public async Task Appearance_IsPersisted_AndVersionThreeUpgradePreservesCustomColors()
    {
        var path = Path.Combine(Path.GetTempPath(), $"expense-appearance-{Guid.NewGuid():N}.db3");
        Guid customId;
        try
        {
            await using (var repository = new SqliteExpenseRepository(path))
            {
                var categories = await repository.GetCategoriesAsync();
                Assert.Equal(categories.Count, categories.Select(item => item.Color).Distinct().Count());
                var service = new CategoryService(repository);
                var custom = await service.SaveCategoryAsync(new CategoryInput(null, "Personale", TransactionType.Expense,
                    "#123456", true, "heart"));
                customId = custom.Id;
            }

            using (var raw = new SQLiteConnection(path))
            {
                raw.Execute("ALTER TABLE Categories DROP COLUMN Icon;");
                raw.Execute("UPDATE Categories SET Color = '#E2383F' WHERE Name IN ('Spesa', 'Ristoranti');");
                raw.Execute("PRAGMA user_version = 3;");
            }

            await using (var upgraded = new SqliteExpenseRepository(path))
            {
                var categories = await upgraded.GetCategoriesAsync();
                Assert.Equal("#123456", categories.Single(item => item.Id == customId).Color);
                var groceries = categories.Single(item => item.Name == "Spesa");
                var restaurant = categories.Single(item => item.Name == "Ristoranti");
                Assert.Equal("cart", groceries.Icon);
                Assert.Equal("food", restaurant.Icon);
                Assert.NotEqual(groceries.Color, restaurant.Color);
                var custom = categories.Single(item => item.Id == customId);
                await new CategoryService(upgraded).SaveCategoryAsync(new CategoryInput(custom.Id,
                    custom.Name, custom.Type, "#F472B6", true, "gift"));
            }

            await using var reopened = new SqliteExpenseRepository(path);
            var saved = await reopened.GetCategoryAsync(customId);
            Assert.Equal("gift", saved!.Icon);
            Assert.Equal("#F472B6", saved.Color);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public void InvalidAppearance_IsRejectedBeforeSaving()
    {
        Assert.Throws<DomainValidationException>(() => new Category(Guid.NewGuid(), "Test",
            TransactionType.Expense, "red"));
        Assert.Throws<DomainValidationException>(() => new Category(Guid.NewGuid(), "Test",
            TransactionType.Expense, "#FFFFFF", icon: "missing"));
    }
}
