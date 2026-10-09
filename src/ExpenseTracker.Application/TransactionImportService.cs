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
    string? Comment, Guid? NativeId = null, string? Description = null, Guid? TransferId = null);

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

        if (parsed.Warnings.Any(w => w.Contains("riga", StringComparison.OrdinalIgnoreCase)))
            throw new DomainValidationException("Importazione annullata: " + string.Join(" ", parsed.Warnings.Take(3)));
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
            // Persist together with categories and transactions after validating every row.
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
                CategoryAppearance.SuggestColor(categories.Select(item => item.Color)));
            // No writes before the entire import has been validated.
            categories.Add(category);
            categoriesCreated++;
        }

        var pending = new List<Transaction>();
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

            if (!string.Equals(account.Currency, row.Currency, StringComparison.OrdinalIgnoreCase))
                throw new DomainValidationException($"Valuta {row.Currency} incompatibile con il conto {account.Name} ({account.Currency}). Importazione annullata.");
            var id = row.NativeId ?? StableGuid($"money-manager-import|{row.SourceKey}");
            var occurredAtUtc = DateTime.SpecifyKind(row.OccurredAtLocal, DateTimeKind.Local).ToUniversalTime();
            var existing = await repository.GetTransactionAsync(id, cancellationToken);
            if (existing?.TransferId is not null && existing.TransferId != row.TransferId)
                throw new DomainValidationException("Il file tenta di separare un trasferimento già registrato. Importazione annullata.");
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
                    row.Description,
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
                    row.Description,
                    row.Comment);
                updated++;
            }

            existing.SetTransfer(row.TransferId);
            pending.Add(existing);
        }

        foreach (var transfer in pending.Where(t => t.TransferId.HasValue).GroupBy(t => t.TransferId))
            if (transfer.Count() != 2 || transfer.Select(t => t.AccountId).Distinct().Count() != 2
                || transfer.Count(t => t.Type == TransactionType.Income) != 1
                || transfer.Count(t => t.Type == TransactionType.Expense) != 1
                || transfer.Select(t => t.Currency).Distinct().Count() != 1
                || transfer.Select(t => t.Amount).Distinct().Count() != 1)
                throw new DomainValidationException("Il file contiene un trasferimento incompleto o incoerente. Esporta entrambi i conti.");
        if (pending.Select(t => t.Id).Distinct().Count() != pending.Count)
            throw new DomainValidationException("Il file contiene identificativi duplicati.");
        await repository.SaveBatchAsync(accounts, categories, pending, cancellationToken: cancellationToken);
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
