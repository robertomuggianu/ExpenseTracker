using System.Text;
using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;

namespace ExpenseTracker.Application.Tests;

public sealed class ImportAccountAmbiguityTests
{
    [Theory]
    [InlineData("Conto")]
    [InlineData("CONTO")]
    public Task NativeReimport_RejectsAmbiguousAccountNameWithoutReassigningOrAddingData(string duplicateName) => WithDatabase(async repo =>
    {
        var first = new Account(Guid.NewGuid(), "Conto", 100m, isPrimary: true);
        var second = new Account(Guid.NewGuid(), duplicateName, 200m);
        await repo.SaveAccountsAsync([first, second]);
        var categories = await repo.GetCategoriesAsync();
        var category = categories.First(c => c.Type == TransactionType.Expense);
        var original = new Transaction(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense,
            second.Id, category.Id, 12m, description: "Originale");
        await repo.SaveTransactionAsync(original);
        var csv = CsvFormatter.Format([
            new(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, "Conto nuovo", "Categoria nuova", "Nuovo", 5m, "EUR", null),
            new(original.Id, original.OccurredAtUtc, original.Type, second.Name, category.Name, "Modificato", 99m, "EUR", null)
        ]);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var error = await Assert.ThrowsAsync<DomainValidationException>(() =>
            new TransactionImportService(repo, new TransactionImportParser()).ImportAsync(stream, "movimenti.csv"));

        Assert.Contains("nomi distinti", error.Message);
        Assert.Equal(2, (await repo.GetAccountsAsync()).Count);
        Assert.Equal(categories.Count, (await repo.GetCategoriesAsync()).Count);
        var saved = Assert.Single(await repo.GetTransactionsAsync());
        Assert.Equal(original.Id, saved.Id);
        Assert.Equal(second.Id, saved.AccountId);
        Assert.Equal(12m, saved.Amount);
        Assert.Equal("Originale", saved.Description);
    });

    [Fact]
    public Task Import_UniqueReferencedAccountWorksDespiteUnrelatedDuplicateNames() => WithDatabase(async repo =>
    {
        var target = new Account(Guid.NewGuid(), "Carta", 0m, isPrimary: true);
        await repo.SaveAccountsAsync([target, new(Guid.NewGuid(), "Contanti", 0m), new(Guid.NewGuid(), "Contanti", 0m)]);
        var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
        var csv = CsvFormatter.Format([new(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense,
            target.Name, category.Name, "Spesa", 8m, "EUR", null)]);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await new TransactionImportService(repo, new TransactionImportParser()).ImportAsync(stream, "movimenti.csv");

        Assert.Equal(1, result.Created);
        Assert.Equal(target.Id, Assert.Single(await repo.GetTransactionsAsync()).AccountId);
    });

    private static async Task WithDatabase(Func<SqliteExpenseRepository, Task> test)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ambiguous-import-{Guid.NewGuid():N}.db3");
        try
        {
            await using var repo = new SqliteExpenseRepository(path);
            await test(repo);
        }
        finally { File.Delete(path); }
    }
}
