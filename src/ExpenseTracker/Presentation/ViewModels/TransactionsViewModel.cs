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
    public IReadOnlyList<PeriodOption> Periods { get; } = PeriodOption.All;

    public IReadOnlyList<TransactionTypeOption> Types { get; } = TransactionTypeOption.Optional;

    public ObservableCollection<AccountFilterOption> Accounts { get; } = [];

    public ObservableCollection<CategoryFilterOption> Categories { get; } = [];

    public ObservableCollection<TransactionListItem> Transactions { get; } = [];
    public ObservableCollection<TransactionDayGroup> Groups { get; } = [];

    [ObservableProperty]
    public partial bool FiltersVisible { get; set; }

    public bool IsCustomPeriod => SelectedPeriod.Value == DateRangePreset.Custom;

    [RelayCommand]
    private void ToggleFilters() => FiltersVisible = !FiltersVisible;

    partial void OnSelectedPeriodChanged(PeriodOption value) => OnPropertyChanged(nameof(IsCustomPeriod));

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
    private Task NewTransactionAsync() => navigation.GoToTransactionEditorAsync();

    [RelayCommand]
    private Task DeleteAsync(TransactionListItem item) => RunAsync(async () =>
    {
        var confirmed = await dialogs.ConfirmAsync(
            "Elimina movimento",
            $"Eliminare definitivamente il movimento da {MoneyFormatter.Format(item.Transaction.Amount, item.Transaction.Currency)}?",
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

        var categoryId = SelectedCategory?.Id;
        Categories.Clear();
        Categories.Add(new CategoryFilterOption(null, "Tutte le categorie"));
        foreach (var item in await categoryService.GetCategoriesAsync())
        {
            Categories.Add(new CategoryFilterOption(item.Id, item.Name));
        }

        SelectedCategory = Categories.FirstOrDefault(item => item.Id == categoryId) ?? Categories[0];
    }

    private async Task LoadTransactionsCoreAsync()
    {
        var range = DateRangeFactory.Create(SelectedPeriod.Value, DateTime.Now, CustomFrom, CustomTo);
        var filter = new TransactionFilter(
            range.FromUtc,
            range.ToUtcExclusive,
            SelectedAccount?.Id,
            SelectedType.Value,
            SelectedCategory?.Id);
        var items = await transactionService.GetTransactionsAsync(filter);
        Transactions.Clear();
        foreach (var item in items)
        {
            Transactions.Add(item);
        }
        Groups.Clear();
        foreach (var group in items.GroupBy(item => item.LocalDate.Date).OrderByDescending(group => group.Key))
            Groups.Add(new TransactionDayGroup(group.Key, group));
    }
}

public sealed class TransactionDayGroup(DateTime date, IEnumerable<TransactionListItem> items)
    : ObservableCollection<TransactionListItem>(items)
{
    public string Title => date == DateTime.Today ? "Oggi" : date == DateTime.Today.AddDays(-1)
        ? "Ieri" : date.ToString("dddd d MMMM", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
}
