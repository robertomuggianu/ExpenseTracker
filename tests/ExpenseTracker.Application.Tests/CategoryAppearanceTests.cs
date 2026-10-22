using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using SQLite;

namespace ExpenseTracker.Application.Tests;

public sealed class CategoryAppearanceTests
{
    [Theory]
    [InlineData("Vape", "cigarette")]
    [InlineData("  CAFFÈ e colazione  ", "coffee")]
    [InlineData("Assicurazione auto", "insurance")]
    [InlineData("Bollo auto", "tax")]
    [InlineData("Manutenzione auto", "tools")]
    [InlineData("Stipendio ottobre", "work")]
    [InlineData("Rimborsi", "transfer")]
    [InlineData("Abbonamento Netflix", "movie")]
    [InlineData("Farmacia / medicine", "medical")]
    [InlineData("Other", "wallet")]
    [InlineData("Altro", "wallet")]
    [InlineData("Barbara", "wallet")]
    [InlineData("Categoria personale", "wallet")]
    public void IconSuggestions_RecognizeNamesWithoutSubstringFalsePositives(string name, string icon) =>
        Assert.Equal(icon, CategoryAppearance.InferIcon(name));

    [Fact]
    public void EverySelectableIcon_HasAValidPackagedSvg()
    {
        foreach (var icon in CategoryAppearance.Icons)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", $"category_{icon.Key}.svg");
            Assert.True(File.Exists(path), $"Missing icon: {icon.Key}");
            Assert.Equal("svg", System.Xml.Linq.XDocument.Load(path).Root!.Name.LocalName);
        }
    }

    [Fact]
    public async Task VersionFive_UpgradesOnlyStandardIconsOnceAndPreservesHistory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"expense-icons-{Guid.NewGuid():N}.db3");
        var legacy = new Category(Guid.NewGuid(), "Rimborsi viaggio", TransactionType.Income, "#123456", false, "tag");
        var custom = new Category(Guid.NewGuid(), "Farmacia", TransactionType.Expense, "#654321", icon: "heart");
        var account = new Account(Guid.NewGuid(), "Conto", 10m);
        var transaction = new Transaction(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Income, account.Id, legacy.Id, 25m);
        try
        {
            await using (var repo = new SqliteExpenseRepository(path))
            {
                await repo.SaveAccountsAsync([account]);
                await repo.SaveCategoryAsync(legacy); await repo.SaveCategoryAsync(custom);
                await repo.SaveTransactionAsync(transaction);
            }
            using (var raw = new SQLiteConnection(path))
            {
                raw.Execute("UPDATE Categories SET Icon = 'tag' WHERE Name IN ('Vape', 'Other');");
                raw.Execute("PRAGMA user_version = 5;");
            }
            await using (var upgraded = new SqliteExpenseRepository(path))
            {
                var categories = await upgraded.GetCategoriesAsync();
                Assert.DoesNotContain(categories, c => c.Icon == "tag");
                var saved = categories.Single(c => c.Id == legacy.Id);
                Assert.Equal("transfer", saved.Icon); Assert.Equal("#123456", saved.Color);
                Assert.Equal(legacy.Name, saved.Name); Assert.False(saved.IsActive);
                Assert.Equal("heart", categories.Single(c => c.Id == custom.Id).Icon);
                Assert.Equal("cigarette", categories.Single(c => c.Name == "Vape").Icon);
                var movement = Assert.Single(await upgraded.GetTransactionsAsync());
                Assert.Equal(transaction.Id, movement.Id); Assert.Equal(legacy.Id, movement.CategoryId);
                Assert.Equal(25m, movement.Amount);
                Assert.Equal(35m, Assert.Single(await new AccountService(upgraded).GetAccountsAsync()).CurrentBalance);
                saved.Update(saved.Name, saved.Type, saved.Color, "tag");
                await upgraded.SaveCategoryAsync(saved);
            }
            await using (var reopened = new SqliteExpenseRepository(path))
                Assert.Equal("tag", (await reopened.GetCategoryAsync(legacy.Id))!.Icon);
            using var check = new SQLiteConnection(path);
            Assert.Equal(6, check.ExecuteScalar<int>("PRAGMA user_version;"));
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("Viaggi", "luggage")]
    [InlineData("Palestra", "sport")]
    [InlineData("Benzina", "fuel")]
    [InlineData("Abbigliamento", "shirt")]
    public void ExpandedIcons_InferNewCategoriesButPreserveExplicitLegacyChoices(string name, string expected)
    {
        Assert.Equal(expected, new Category(Guid.NewGuid(), name, TransactionType.Expense, "#123456").Icon);
        Assert.Equal("tag", new Category(Guid.NewGuid(), name, TransactionType.Expense, "#123456", icon: "tag").Icon);
        Assert.Equal("car", new Category(Guid.NewGuid(), name, TransactionType.Expense, "#123456", icon: "car").Icon);
    }
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
                raw.Execute("DROP INDEX IX_Transactions_TransferId;");
                raw.Execute("ALTER TABLE Transactions DROP COLUMN TransferId;");
                raw.Execute("DROP TABLE AppState;");
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
