using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public interface IExpenseRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

    Task<Account?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAccountsAsync(IReadOnlyCollection<Account> accounts, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<Category?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveCategoryAsync(Category category, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetTransactionsAsync(
        TransactionFilter? filter = null,
        CancellationToken cancellationToken = default);

    Task<Transaction?> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default);

    Task DeleteTransactionAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveBatchAsync(IReadOnlyCollection<Account> accounts, IReadOnlyCollection<Category> categories,
        IReadOnlyCollection<Transaction> transactions, string? stateKey = null, string? stateValue = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AccountSummary>> GetAccountSummariesAsync(CancellationToken cancellationToken = default);
    Task<string?> GetStateAsync(string key);
    Task SaveStateAsync(string key, string value);
    Task<IReadOnlyDictionary<Guid, int>> GetCategoryUsageAsync();
}

public interface ICsvExportService
{
    Task<CsvExportResult> ExportAsync(TransactionFilter? filter = null, CancellationToken cancellationToken = default);
}
