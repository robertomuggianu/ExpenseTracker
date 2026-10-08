using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public sealed class AccountService(IExpenseRepository repository)
{
    public async Task<IReadOnlyList<AccountSummary>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await repository.GetAccountsAsync(cancellationToken);
        var transactions = await repository.GetTransactionsAsync(cancellationToken: cancellationToken);

        return accounts
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Name)
            .Select(item => new AccountSummary(item, item.CalculateCurrentBalance(transactions)))
            .ToArray();
    }

    public Task<Account?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.GetAccountAsync(id, cancellationToken);

    public async Task<Account> SaveAsync(AccountInput input, CancellationToken cancellationToken = default)
    {
        var accounts = (await repository.GetAccountsAsync(cancellationToken)).ToList();
        var account = input.Id.HasValue
            ? accounts.SingleOrDefault(item => item.Id == input.Id.Value)
                ?? throw new DomainValidationException("Il conto da modificare non esiste.")
            : new Account(Guid.NewGuid(), input.Name, input.OpeningBalance, input.Currency);

        if (input.Id.HasValue)
        {
            account.Update(input.Name, input.OpeningBalance, input.Currency);
        }
        else
        {
            accounts.Add(account);
        }

        if (account.IsPrimary && !input.IsPrimary)
        {
            throw new DomainValidationException("Imposta prima un altro conto come principale.");
        }

        if (!input.IsActive && account.IsPrimary)
        {
            throw new DomainValidationException("Seleziona un altro conto principale prima di disattivare questo conto.");
        }

        account.SetActive(input.IsActive);

        var shouldSelectPrimary = input.IsPrimary || !accounts.Any(item => item.IsPrimary && item.IsActive);
        if (shouldSelectPrimary)
        {
            AccountRules.SelectPrimary(accounts, account.Id);
        }
        else
        {
            account.SetPrimary(false);
        }

        await repository.SaveAccountsAsync(accounts, cancellationToken);
        return account;
    }

    public async Task SetPrimaryAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var accounts = (await repository.GetAccountsAsync(cancellationToken)).ToList();
        AccountRules.SelectPrimary(accounts, accountId);
        await repository.SaveAccountsAsync(accounts, cancellationToken);
    }

    public async Task SetActiveAsync(Guid accountId, bool isActive, CancellationToken cancellationToken = default)
    {
        var accounts = (await repository.GetAccountsAsync(cancellationToken)).ToList();
        var account = accounts.SingleOrDefault(item => item.Id == accountId)
            ?? throw new DomainValidationException("Il conto selezionato non esiste.");
        account.SetActive(isActive);
        await repository.SaveAccountsAsync(accounts, cancellationToken);
    }
}
