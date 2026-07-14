using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.Services;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class TransactionsViewModel(
    TransactionService transactionService,
    AccountService accountService,
    CategoryService categoryService,
    IUserDialogService dialogs,
    IAppNavigationService navigation) : ViewModelBase
{
    private IReadOnlyList<Category> _allCategories = [];

    public IReadOnlyList<PeriodOption> Periods { get; } = PeriodOption.All;

    public IReadOnlyList<TransactionTypeOption> Types { get; } = TransactionTypeOption.Optional;

    public ObservableCollection<AccountFilterOption> Accounts { get; } = [];

    public ObservableCollection<MacroCategoryFilterOption> MacroCategories { get; } = [];

    public ObservableCollection<CategoryFilterOption> Categories { get; } = [];

    public ObservableCollection<TransactionListItem> Transactions { get; } = [];

    [ObservableProperty]
    public partial PeriodOption SelectedPeriod { get; set; } = PeriodOption.All[1];

    [ObservableProperty]
    public partial DateTime CustomFrom { get; set; } = DateTime.Today.AddMonths(-1);

    [ObservableProperty]
    public partial DateTime CustomTo { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial AccountFilterOption? SelectedAccount { get; set; }

    [ObservableProperty]
    public partial TransactionTypeOption SelectedType { get; set; } = TransactionTypeOption.Optional[0];

    [ObservableProperty]
    public partial MacroCategoryFilterOption? SelectedMacroCategory { get; set; }

    [ObservableProperty]
    public partial CategoryFilterOption? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial TransactionListItem? SelectedTransaction { get; set; }

    [ObservableProperty]
    public partial bool HasSelectedTransaction { get; set; }

    [RelayCommand]
    public Task LoadAsync() => RunAsync(async () =>
    {
        await LoadFiltersAsync();
        await LoadTransactionsCoreAsync();
    });

    [RelayCommand]
    private Task ApplyFiltersAsync() => RunAsync(LoadTransactionsCoreAsync);

    [RelayCommand]
    private void ShowDetail(TransactionListItem item) => SelectedTransaction = item;

    [RelayCommand]
    private Task EditAsync(TransactionListItem item) => navigation.GoToTransactionEditorAsync(item.Transaction.Id);

    [RelayCommand]
    private Task DeleteAsync(TransactionListItem item) => RunAsync(async () =>
    {
        var confirmed = await dialogs.ConfirmAsync(
            "Elimina movimento",
            $"Eliminare definitivamente il movimento da {item.Transaction.Amount:C}?",
            "Elimina",
            "Annulla");
        if (!confirmed)
        {
            return;
        }

        await transactionService.DeleteAsync(item.Transaction.Id);
        SelectedTransaction = null;
        await LoadTransactionsCoreAsync();
    });

    partial void OnSelectedMacroCategoryChanged(MacroCategoryFilterOption? value)
    {
        Categories.Clear();
        Categories.Add(new CategoryFilterOption(null, "Tutte le categorie"));
        if (value?.Id is Guid macroId)
        {
            foreach (var item in _allCategories.Where(item => item.MacroCategoryId == macroId))
            {
                Categories.Add(new CategoryFilterOption(item.Id, item.Name));
            }
        }
        else
        {
            foreach (var item in _allCategories)
            {
                Categories.Add(new CategoryFilterOption(item.Id, item.Name));
            }
        }

        SelectedCategory = Categories[0];
    }

    partial void OnSelectedTransactionChanged(TransactionListItem? value) =>
        HasSelectedTransaction = value is not null;

    private async Task LoadFiltersAsync()
    {
        var accountId = SelectedAccount?.Id;
        Accounts.Clear();
        Accounts.Add(new AccountFilterOption(null, "Tutti i conti"));
        foreach (var item in await accountService.GetAccountsAsync())
        {
            Accounts.Add(new AccountFilterOption(item.Account.Id, item.Account.Name));
        }

        SelectedAccount = Accounts.FirstOrDefault(item => item.Id == accountId) ?? Accounts[0];

        var macroId = SelectedMacroCategory?.Id;
        MacroCategories.Clear();
        MacroCategories.Add(new MacroCategoryFilterOption(null, "Tutte le macro-categorie"));
        foreach (var item in await categoryService.GetMacroCategoriesAsync())
        {
            MacroCategories.Add(new MacroCategoryFilterOption(item.Id, item.Name));
        }

        _allCategories = await categoryService.GetCategoriesAsync();
        SelectedMacroCategory = MacroCategories.FirstOrDefault(item => item.Id == macroId) ?? MacroCategories[0];
    }

    private async Task LoadTransactionsCoreAsync()
    {
        var range = DateRangeFactory.Create(SelectedPeriod.Value, DateTime.Now, CustomFrom, CustomTo);
        var filter = new TransactionFilter(
            range.FromUtc,
            range.ToUtcExclusive,
            SelectedAccount?.Id,
            SelectedType.Value,
            SelectedMacroCategory?.Id,
            SelectedCategory?.Id);
        var items = await transactionService.GetTransactionsAsync(filter);
        Transactions.Clear();
        foreach (var item in items)
        {
            Transactions.Add(item);
        }
    }
}
