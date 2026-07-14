using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public sealed class TransactionService(IExpenseRepository repository)
{
    public async Task<IReadOnlyList<TransactionListItem>> GetTransactionsAsync(
        TransactionFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        var transactions = await repository.GetTransactionsAsync(filter, cancellationToken);
        var accounts = await repository.GetAccountsAsync(cancellationToken);
        var categories = await repository.GetCategoriesAsync(cancellationToken: cancellationToken);
        var macroCategories = await repository.GetMacroCategoriesAsync(cancellationToken);

        return Join(transactions, accounts, categories, macroCategories);
    }

    public Task<Transaction?> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetTransactionAsync(id, cancellationToken);

    public async Task<Transaction> SaveAsync(TransactionInput input, CancellationToken cancellationToken = default)
    {
        var account = await repository.GetAccountAsync(input.AccountId, cancellationToken)
            ?? throw new DomainValidationException("Il conto selezionato non esiste.");
        if (!account.IsActive)
        {
            throw new DomainValidationException("Il conto selezionato non è attivo.");
        }

        var category = await repository.GetCategoryAsync(input.CategoryId, cancellationToken)
            ?? throw new DomainValidationException("La categoria selezionata non esiste.");
        var macroCategory = await repository.GetMacroCategoryAsync(category.MacroCategoryId, cancellationToken)
            ?? throw new DomainValidationException("La macro-categoria selezionata non esiste.");

        if (!category.IsActive || !macroCategory.IsActive)
        {
            throw new DomainValidationException("La categoria selezionata non è attiva.");
        }

        TransactionRules.EnsureCategoryCompatible(input.Type, macroCategory);

        var transaction = input.Id.HasValue
            ? await repository.GetTransactionAsync(input.Id.Value, cancellationToken)
                ?? throw new DomainValidationException("Il movimento da modificare non esiste.")
            : new Transaction(
                Guid.NewGuid(),
                input.OccurredAtUtc,
                input.Type,
                input.AccountId,
                input.CategoryId,
                input.Amount,
                input.Currency,
                input.Description,
                input.Notes);

        if (input.Id.HasValue)
        {
            transaction.Update(
                input.OccurredAtUtc,
                input.Type,
                input.AccountId,
                input.CategoryId,
                input.Amount,
                input.Currency,
                input.Description,
                input.Notes);
        }

        await repository.SaveTransactionAsync(transaction, cancellationToken);
        return transaction;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.DeleteTransactionAsync(id, cancellationToken);

    internal static IReadOnlyList<TransactionListItem> Join(
        IEnumerable<Transaction> transactions,
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<Category> categories,
        IReadOnlyCollection<MacroCategory> macroCategories)
    {
        var accountMap = accounts.ToDictionary(item => item.Id);
        var categoryMap = categories.ToDictionary(item => item.Id);
        var macroMap = macroCategories.ToDictionary(item => item.Id);

        return transactions
            .OrderByDescending(item => item.OccurredAtUtc)
            .Select(item =>
            {
                var account = accountMap[item.AccountId];
                var category = categoryMap[item.CategoryId];
                var macro = macroMap[category.MacroCategoryId];
                return new TransactionListItem(item, account.Name, category.Name, macro.Name, macro.Color);
            })
            .ToArray();
    }
}
