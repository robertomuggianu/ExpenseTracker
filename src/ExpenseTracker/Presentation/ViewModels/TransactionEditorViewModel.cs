using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.Services;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class TransactionEditorViewModel(
    AccountService accountService,
    CategoryService categoryService,
    TransactionService transactionService,
    IUserDialogService dialogs,
    IAppNavigationService navigation) : ViewModelBase
{
    private IReadOnlyList<Category> _allCategories = [];

    public IReadOnlyList<TransactionTypeOption> Types { get; } = TransactionTypeOption.Required;

    public ObservableCollection<AccountSummary> Accounts { get; } = [];

    public ObservableCollection<Category> Categories { get; } = [];

    [ObservableProperty]
    public partial Guid? EditingId { get; set; }

    [ObservableProperty]
    public partial TransactionTypeOption SelectedType { get; set; } = TransactionTypeOption.Required[1];

    [ObservableProperty]
    public partial AccountSummary? SelectedAccount { get; set; }

    [ObservableProperty]
    public partial Category? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial DateTime Date { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial TimeSpan Time { get; set; } = DateTime.Now.TimeOfDay;

    [ObservableProperty]
    public partial string Amount { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Currency { get; set; } = "EUR";

    [ObservableProperty]
    public partial string? Description { get; set; }

    [ObservableProperty]
    public partial string? Notes { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "Nuovo movimento";

    public Task LoadAsync(Guid? transactionId = null) => RunAsync(async () =>
    {
        EditingId = transactionId;
        Title = transactionId.HasValue ? "Modifica movimento" : "Nuovo movimento";

        Accounts.Clear();
        foreach (var item in (await accountService.GetAccountsAsync()).Where(item => item.Account.IsActive))
        {
            Accounts.Add(item);
        }

        _allCategories = (await categoryService.GetCategoriesAsync(activeOnly: true)).ToArray();

        if (transactionId.HasValue)
        {
            var item = await transactionService.GetTransactionAsync(transactionId.Value)
                ?? throw new DomainValidationException("Il movimento selezionato non esiste.");
            var localDate = item.OccurredAtUtc.ToLocalTime();
            SelectedType = Types.Single(option => option.Value == item.Type);
            SelectedAccount = Accounts.SingleOrDefault(account => account.Account.Id == item.AccountId);
            RefreshCategories();
            SelectedCategory = Categories.SingleOrDefault(category => category.Id == item.CategoryId);
            Date = localDate.Date;
            Time = localDate.TimeOfDay;
            Amount = item.Amount.ToString("0.00", CultureInfo.CurrentCulture);
            Currency = item.Currency;
            Description = item.Description;
            Notes = item.Notes;
        }
        else
        {
            ResetForm();
        }
    });

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async () =>
    {
        if (SelectedAccount is null)
        {
            throw new DomainValidationException("Seleziona un conto.");
        }

        if (SelectedCategory is null)
        {
            throw new DomainValidationException("Seleziona una categoria.");
        }

        if (!TryParseDecimal(Amount, out var parsedAmount))
        {
            throw new DomainValidationException("Inserisci un importo valido.");
        }

        var localDateTime = Date.Date.Add(Time);
        var occurredAtUtc = DateTime.SpecifyKind(localDateTime, DateTimeKind.Local).ToUniversalTime();
        await transactionService.SaveAsync(new TransactionInput(
            EditingId,
            occurredAtUtc,
            SelectedType.Value!.Value,
            SelectedAccount.Account.Id,
            SelectedCategory.Id,
            parsedAmount,
            Currency,
            Description,
            Notes));

        await dialogs.ShowMessageAsync("Movimento salvato", "I dati sono stati salvati sul dispositivo.");
        await navigation.GoToTransactionsAsync();
    });

    partial void OnSelectedTypeChanged(TransactionTypeOption value)
    {
        RefreshCategories();
        SelectedCategory = Categories.FirstOrDefault();
    }

    private void ResetForm()
    {
        SelectedType = Types[1];
        SelectedAccount = Accounts.FirstOrDefault(item => item.Account.IsPrimary) ?? Accounts.FirstOrDefault();
        RefreshCategories();
        SelectedCategory = Categories.FirstOrDefault();
        Date = DateTime.Today;
        Time = DateTime.Now.TimeOfDay;
        Amount = string.Empty;
        Currency = SelectedAccount?.Account.Currency ?? "EUR";
        Description = null;
        Notes = null;
    }

    private void RefreshCategories()
    {
        var selectedId = SelectedCategory?.Id;
        Categories.Clear();
        foreach (var item in _allCategories.Where(item => item.Type == SelectedType.Value))
        {
            Categories.Add(item);
        }

        SelectedCategory = Categories.FirstOrDefault(item => item.Id == selectedId);
    }

    private static bool TryParseDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result)
        || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
}
