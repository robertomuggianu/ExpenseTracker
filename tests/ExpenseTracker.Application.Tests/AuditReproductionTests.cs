using System.Text;
using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;

namespace ExpenseTracker.Application.Tests;

public sealed class AuditReproductionTests
{
    [Fact]
    public async Task CsvComma_ShouldRemainDecimal()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Data;Tipo;Conto;Categoria;Importo;Valuta\n2026-10-09;Uscita;Conto;Spesa;6,50;EUR"));
        var parsed = await new TransactionImportParser().ParseAsync(stream, "test.csv");
        Assert.Equal(6.50m, Assert.Single(parsed.Transactions).Amount);
    }

    [Theory]
    [InlineData("Affitto")]
    [InlineData("Cafe")]
    public async Task RenamingSeedCategory_ShouldSurviveRestart(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.db3");
        try
        {
            await using (var repo = new SqliteExpenseRepository(path))
            {
                var category = (await repo.GetCategoriesAsync()).Single(c => c.Name == name);
                category.Update("Nome personalizzato", category.Type, category.Color, category.Icon);
                await repo.SaveCategoryAsync(category);
            }
            await using var reopened = new SqliteExpenseRepository(path);
            await reopened.InitializeAsync();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task NativeCsvRoundtrip_ShouldNotDuplicateTransaction()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.db3");
        try
        {
            await using var repo = new SqliteExpenseRepository(path);
            var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
            var account = new Account(Guid.NewGuid(), "Conto", 100m, isPrimary: true);
            await repo.SaveAccountsAsync([account]);
            await repo.SaveTransactionAsync(new Transaction(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, account.Id, category.Id, 6.50m, description: "Pranzo"));
            var export = await new CsvExportService(repo).ExportAsync();
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(export.Content));
            await new TransactionImportService(repo, new TransactionImportParser()).ImportAsync(stream, export.FileName);
            Assert.Single(await repo.GetTransactionsAsync());
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ForeignCurrency_ShouldNotBeSubtractedAsEuros()
    {
        var path = Path.Combine(Path.GetTempPath(), $"audit-{Guid.NewGuid():N}.db3");
        try
        {
            await using var repo = new SqliteExpenseRepository(path);
            var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
            var account = new Account(Guid.NewGuid(), "Euro", 100m, isPrimary: true);
            await repo.SaveAccountsAsync([account]);
            await Assert.ThrowsAsync<DomainValidationException>(() => new TransactionService(repo).SaveAsync(
                new TransactionInput(null, DateTime.UtcNow, TransactionType.Expense, account.Id, category.Id, 10m, "USD", null, null)));
        }
        finally { File.Delete(path); }
    }
}
