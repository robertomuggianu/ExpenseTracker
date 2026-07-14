using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public interface IExpenseRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);

    Task<Account?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveAccountsAsync(IReadOnlyCollection<Account> accounts, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MacroCategory>> GetMacroCategoriesAsync(CancellationToken cancellationToken = default);

    Task<MacroCategory?> GetMacroCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveMacroCategoryAsync(MacroCategory macroCategory, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> GetCategoriesAsync(Guid? macroCategoryId = null, CancellationToken cancellationToken = default);

    Task<Category?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveCategoryAsync(Category category, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetTransactionsAsync(
        TransactionFilter? filter = null,
        CancellationToken cancellationToken = default);

    Task<Transaction?> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default);

    Task DeleteTransactionAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface ICsvExportService
{
    Task<CsvExportResult> ExportAsync(TransactionFilter? filter = null, CancellationToken cancellationToken = default);
}
