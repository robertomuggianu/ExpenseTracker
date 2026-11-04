using System.Text;
using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;

namespace ExpenseTracker.Application.Tests;

public sealed class ImportIntegrityPolishTests
{
    [Fact]
    public Task UnclosedCsvQuoteRejectsEntireImportBeforeAnyWrite() => WithDatabase(async repo =>
    {
        var account = Assert.Single(await repo.GetAccountsAsync());
        var categories = await repo.GetCategoriesAsync();
        var category = categories.First(c => c.Type == TransactionType.Expense);
        var original = new Transaction(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense,
            account.Id, category.Id, 7m, notes: "Nota originale");
        await repo.SaveTransactionAsync(original);
        var csv = CsvFormatter.Header + "\n"
            + $"{original.Id:D};2026-11-01T12:00:00Z;Uscita;Principale;{category.Name};Modifica;99;EUR;Nota modificata;\n"
            + "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb;2026-11-02T12:00:00Z;Uscita;Conto nuovo;Categoria nuova;Nuovo;5;EUR;\"Nota non chiusa\n"
            + "cccccccc-cccc-cccc-cccc-cccccccccccc;2026-11-03T12:00:00Z;Uscita;Conto nuovo;Categoria nuova;Ultimo;6;EUR;Ultima nota;";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var error = await Assert.ThrowsAsync<DomainValidationException>(() =>
            new TransactionImportService(repo, new TransactionImportParser()).ImportAsync(stream, "movimenti.csv"));

        Assert.Contains("virgolette", error.Message);
        var unchanged = Assert.Single(await repo.GetTransactionsAsync());
        Assert.Equal(original.Id, unchanged.Id);
        Assert.Equal(7m, unchanged.Amount);
        Assert.Equal("Nota originale", unchanged.Notes);
        Assert.Single(await repo.GetAccountsAsync());
        Assert.Equal(categories.Count, (await repo.GetCategoriesAsync()).Count);
    });

    [Fact]
    public Task ValidMultilineAndEscapedQuotedFieldsImportWithoutSwallowingNextRow() => WithDatabase(async repo =>
    {
        var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        const string note = "Prima riga; dettaglio\nSeconda \"citazione\"";
        const string description = "Spesa \"speciale\"; casa";
        var csv = CsvFormatter.Format([
            new(firstId, new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), TransactionType.Expense,
                "Principale", category.Name, description, 12.50m, "EUR", note),
            new(secondId, new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc), TransactionType.Expense,
                "Principale", category.Name, "Riga successiva", 3m, "EUR", null)
        ]);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await new TransactionImportService(repo, new TransactionImportParser()).ImportAsync(stream, "movimenti.csv");

        Assert.Equal(2, result.Created);
        Assert.Empty(result.Warnings);
        var transactions = await repo.GetTransactionsAsync();
        Assert.Equal(2, transactions.Count);
        var first = transactions.Single(t => t.Id == firstId);
        Assert.Equal(note, first.Notes);
        Assert.Equal(description, first.Description);
        Assert.Equal(12.50m, first.Amount);
        Assert.Equal(3m, transactions.Single(t => t.Id == secondId).Amount);
    });

    private static async Task WithDatabase(Func<SqliteExpenseRepository, Task> test)
    {
        var path = Path.Combine(Path.GetTempPath(), $"import-integrity-{Guid.NewGuid():N}.db3");
        try
        {
            await using var repo = new SqliteExpenseRepository(path);
            await repo.SaveAccountsAsync([new Account(Guid.NewGuid(), "Principale", 100m, isPrimary: true)]);
            await test(repo);
        }
        finally { File.Delete(path); }
    }
}
