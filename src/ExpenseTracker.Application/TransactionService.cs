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
        var categories = await repository.GetCategoriesAsync(cancellationToken);

        return Join(transactions, accounts, categories);
    }

    public Task<Transaction?> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetTransactionAsync(id, cancellationToken);

    public async Task<Transaction> SaveAsync(TransactionInput input, CancellationToken cancellationToken = default)
    {
        var original = input.Id.HasValue ? await repository.GetTransactionAsync(input.Id.Value, cancellationToken) : null;
        if (original?.TransferId is not null) throw new DomainValidationException("Per correggere un trasferimento, elimina la coppia e ricreala dalla sezione Trasferimenti.");
        var account = await repository.GetAccountAsync(input.AccountId, cancellationToken)
            ?? throw new DomainValidationException("Il conto selezionato non esiste.");
        if (!account.IsActive && original?.AccountId != account.Id)
        {
            throw new DomainValidationException("Il conto selezionato non è attivo.");
        }

        var category = await repository.GetCategoryAsync(input.CategoryId, cancellationToken)
            ?? throw new DomainValidationException("La categoria selezionata non esiste.");
        if (!category.IsActive && original?.CategoryId != category.Id)
        {
            throw new DomainValidationException("La categoria selezionata non è attiva.");
        }

        if (!string.Equals(input.Currency.Trim(), account.Currency, StringComparison.OrdinalIgnoreCase))
            throw new DomainValidationException("La valuta del movimento deve coincidere con quella del conto.");
        TransactionRules.EnsureCategoryCompatible(input.Type, category);

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

    public Task<IReadOnlyDictionary<Guid, int>> GetCategoryUsageAsync() => repository.GetCategoryUsageAsync();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.DeleteTransactionAsync(id, cancellationToken);

    internal static IReadOnlyList<TransactionListItem> Join(
        IEnumerable<Transaction> transactions,
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<Category> categories)
    {
        var accountMap = accounts.ToDictionary(item => item.Id);
        var categoryMap = categories.ToDictionary(item => item.Id);
        return transactions
            .OrderByDescending(item => item.OccurredAtUtc)
            .Select(item =>
            {
                var account = accountMap[item.AccountId];
                var category = categoryMap[item.CategoryId];
                return new TransactionListItem(item, account.Name, category.Name, category.Color, category.Icon);
            })
            .ToArray();
    }
}
