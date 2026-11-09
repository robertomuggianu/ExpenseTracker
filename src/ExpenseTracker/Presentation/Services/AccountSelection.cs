using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using ExpenseTracker.Application;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Presentation.Services;

/// <summary>The browsing scope. Editing a transaction never changes this selection.</summary>
public sealed class AccountSelection : ObservableObject
{
    private const string PreferenceKey = "selected-browsing-account";
    private readonly IAppPreferences? preferences;
    private IReadOnlyList<AccountSummary> accounts = [];
    public Guid? AccountId { get; private set; }
    public string Name { get; private set; } = "Tutti i conti";
    public string Currency { get; private set; } = "EUR";
    public string Summary => $"{Name} · {Currency}";
    public bool IsSingleAccount => AccountId.HasValue;
    public IReadOnlyList<AccountFilterOption> Options { get; private set; } = [new(null, "Tutti i conti")];

    public AccountSelection(IAppPreferences? preferences = null)
    {
        this.preferences = preferences;
        if (Guid.TryParse(preferences?.Get(PreferenceKey, ""), out var id)) AccountId = id;
    }

    public void UpdateAccounts(IReadOnlyList<AccountSummary> items)
    {
        accounts = items;
        Options = new[] { new AccountFilterOption(null, "Tutti i conti") }
            .Concat(items.Select(a => new AccountFilterOption(a.Account.Id, a.Account.Name,
                a.Account.IconSource, a.Account.Currency, a.Account.IsPrimary))).ToArray();
        OnPropertyChanged(nameof(Options));
        Select(AccountId.HasValue && items.All(a => a.Account.Id != AccountId) ? null : AccountId);
    }

    public void Select(Guid? accountId)
    {
        var account = accounts.FirstOrDefault(a => a.Account.Id == accountId)?.Account;
        var name = account?.Name ?? "Tutti i conti";
        var currency = account?.Currency ?? accounts.Where(a => a.Account.IsActive)
            .OrderByDescending(a => a.Account.IsPrimary).ThenBy(a => a.Account.CreatedAtUtc).ThenBy(a => a.Account.Id)
            .Select(a => a.Account.Currency).FirstOrDefault() ?? "EUR";
        var changed = AccountId != accountId || Name != name || Currency != currency;
        AccountId = accountId; Name = name; Currency = currency;
        if (accountId.HasValue) preferences?.Set(PreferenceKey, accountId.Value.ToString());
        else preferences?.Remove(PreferenceKey);
        if (!changed) return;
        OnPropertyChanged(nameof(AccountId)); OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Currency)); OnPropertyChanged(nameof(Summary)); OnPropertyChanged(nameof(IsSingleAccount));
        WeakReferenceMessenger.Default.Send(new AccountSelectionChanged(this));
    }
}

public sealed record AccountSelectionChanged(AccountSelection Selection);
