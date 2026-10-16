using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.Services;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class AccountsViewModel(AccountService accountService, IUserDialogService dialogs) : ViewModelBase
{
    public ObservableCollection<AccountSummary> Accounts { get; } = [];

    [ObservableProperty]
    public partial string? BalanceCurrency { get; set; } = "EUR";

    [ObservableProperty]
    public partial decimal TotalBalance { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFloatingActionsVisible))]
    public partial bool IsEditorVisible { get; set; }

    public bool IsFloatingActionsVisible => !IsEditorVisible;

    [ObservableProperty]
    public partial Guid? EditingId { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OpeningBalance { get; set; } = "0,00";

    [ObservableProperty]
    public partial string Currency { get; set; } = "EUR";

    [ObservableProperty]
    public partial bool IsPrimary { get; set; }

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    [RelayCommand]
    public Task LoadAsync() => RunAsync(LoadCoreAsync);

    private async Task LoadCoreAsync()
    {
        Accounts.Clear();
        foreach (var item in await accountService.GetAccountsAsync())
        {
            Accounts.Add(item);
        }

        BalanceCurrency = CurrencySelection.SingleOrDefault(Accounts.Where(item => item.Account.IsActive).Select(item => item.Account.Currency));
        if (Accounts.Any(a => a.HasCurrencyMismatch)) ErrorMessage = "Il saldo esclude movimenti con valuta errata: correggili dalla lista Movimenti.";
        TotalBalance = Accounts.Where(item => item.Account.IsActive).Sum(item => item.CurrentBalance);
    }

    [RelayCommand]
    private void NewAccount()
    {
        ErrorMessage = null;
        EditingId = null;
        Name = string.Empty;
        OpeningBalance = "0,00";
        Currency = "EUR";
        IsPrimary = Accounts.Count == 0;
        IsActive = true;
        IsEditorVisible = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditorVisible = false;
        EditingId = null;
        Name = string.Empty;
        OpeningBalance = "0,00";
        Currency = "EUR";
        IsPrimary = false;
        IsActive = true;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void Edit(AccountSummary summary)
    {
        ErrorMessage = null;
        EditingId = summary.Account.Id;
        Name = summary.Account.Name;
        OpeningBalance = summary.Account.OpeningBalance.ToString("0.00", CultureInfo.GetCultureInfo("it-IT"));
        Currency = summary.Account.Currency;
        IsPrimary = summary.Account.IsPrimary;
        IsActive = summary.Account.IsActive;
        IsEditorVisible = true;
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (!AmountParser.TryParse(OpeningBalance, out var balance))
        {
            throw new DomainValidationException("Inserisci un saldo iniziale valido.");
        }

        await accountService.SaveAsync(new AccountInput(EditingId, Name, balance, Currency, IsPrimary, IsActive));
        IsEditorVisible = false;
        await LoadCoreAsync();
    });

    [RelayCommand]
    private Task SetPrimaryAsync(AccountSummary summary) => RunAsync(async () =>
    {
        await accountService.SetPrimaryAsync(summary.Account.Id);
        await LoadCoreAsync();
    });

    [RelayCommand]
    private Task ToggleActiveAsync(AccountSummary summary) => RunAsync(async () =>
    {
        if (summary.Account.IsActive)
        {
            var confirmed = await dialogs.ConfirmAsync(
                "Disattiva conto",
                $"Vuoi disattivare {summary.Account.Name}? I movimenti storici resteranno disponibili.",
                "Disattiva",
                "Annulla");
            if (!confirmed)
            {
                return;
            }
        }

        await accountService.SetActiveAsync(summary.Account.Id, !summary.Account.IsActive);
        await LoadCoreAsync();
    });
}
