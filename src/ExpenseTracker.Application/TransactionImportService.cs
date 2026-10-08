using System.Security.Cryptography;
using System.Text;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public sealed record ImportedTransactionRow(
    string SourceKey,
    DateTime OccurredAtLocal,
    TransactionType Type,
    string AccountName,
    string CategoryName,
    decimal Amount,
    string Currency,
    string? Comment);

public sealed record ImportParseResult(
    IReadOnlyList<ImportedTransactionRow> Transactions,
    int SkippedTransfers,
    IReadOnlyList<string> Warnings);

public sealed record TransactionImportResult(
    int Created,
    int Updated,
    int AccountsCreated,
    int CategoriesCreated,
    int SkippedTransfers,
    IReadOnlyList<string> Warnings)
{
    public int TotalProcessed => Created + Updated;
}

public interface ITransactionImportParser
{
    Task<ImportParseResult> ParseAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default);
}

public sealed class TransactionImportService(
    IExpenseRepository repository,
    ITransactionImportParser parser)
{
    private static readonly string[] CategoryColors =
    [
        "#E2383F", "#FF6B70", "#F4B942", "#36C88A", "#5AA9FF", "#9C7CFF",
        "#F477B3", "#F28C45", "#35B5B0", "#7DBB52", "#9B8B7A", "#6F7D91"
    ];

    public async Task<TransactionImportResult> ImportAsync(
        Stream stream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var parsed = await parser.ParseAsync(stream, fileName, cancellationToken);
        if (parsed.Transactions.Count == 0)
        {
            throw new DomainValidationException("Il file non contiene movimenti importabili.");
        }

        var accounts = (await repository.GetAccountsAsync(cancellationToken)).ToList();
        var categories = (await repository.GetCategoriesAsync(cancellationToken)).ToList();
        var accountsCreated = 0;
        var categoriesCreated = 0;

        foreach (var sourceAccount in parsed.Transactions
                     .GroupBy(item => item.AccountName, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First()))
        {
            if (accounts.Any(item => string.Equals(item.Name, sourceAccount.AccountName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var account = new Account(
                StableGuid($"import-account|{sourceAccount.AccountName.ToUpperInvariant()}"),
                sourceAccount.AccountName,
                0m,
                sourceAccount.Currency);
            accounts.Add(account);
            accountsCreated++;
        }

        if (accounts.Count > 0 && !accounts.Any(item => item.IsPrimary && item.IsActive))
        {
            AccountRules.SelectPrimary(accounts, accounts.First(item => item.IsActive).Id);
        }

        if (accountsCreated > 0)
        {
            await repository.SaveAccountsAsync(accounts, cancellationToken);
        }

        foreach (var sourceCategory in parsed.Transactions
                     .GroupBy(item => new { item.Type, Name = item.CategoryName.ToUpperInvariant() })
                     .Select(group => group.First()))
        {
            var category = categories.FirstOrDefault(item =>
                item.Type == sourceCategory.Type
                && string.Equals(item.Name, sourceCategory.CategoryName, StringComparison.OrdinalIgnoreCase));
            if (category is not null)
            {
                continue;
            }

            category = new Category(
                StableGuid($"import-category|{sourceCategory.Type}|{sourceCategory.CategoryName.ToUpperInvariant()}"),
                sourceCategory.CategoryName,
                sourceCategory.Type,
                CategoryColors[categories.Count % CategoryColors.Length]);
            await repository.SaveCategoryAsync(category, cancellationToken);
            categories.Add(category);
            categoriesCreated++;
        }

        var created = 0;
        var updated = 0;
        foreach (var row in parsed.Transactions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var account = accounts.First(item =>
                string.Equals(item.Name, row.AccountName, StringComparison.OrdinalIgnoreCase));
            var category = categories.First(item =>
                item.Type == row.Type
                && string.Equals(item.Name, row.CategoryName, StringComparison.OrdinalIgnoreCase));

            var id = StableGuid($"money-manager-import|{row.SourceKey}");
            var occurredAtUtc = DateTime.SpecifyKind(row.OccurredAtLocal, DateTimeKind.Local).ToUniversalTime();
            var existing = await repository.GetTransactionAsync(id, cancellationToken);
            if (existing is null)
            {
                existing = new Transaction(
                    id,
                    occurredAtUtc,
                    row.Type,
                    account.Id,
                    category.Id,
                    row.Amount,
                    row.Currency,
                    row.CategoryName,
                    row.Comment);
                created++;
            }
            else
            {
                existing.Update(
                    occurredAtUtc,
                    row.Type,
                    account.Id,
                    category.Id,
                    row.Amount,
                    row.Currency,
                    row.CategoryName,
                    row.Comment);
                updated++;
            }

            await repository.SaveTransactionAsync(existing, cancellationToken);
        }

        return new TransactionImportResult(
            created,
            updated,
            accountsCreated,
            categoriesCreated,
            parsed.SkippedTransfers,
            parsed.Warnings);
    }

    private static Guid StableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }
}
