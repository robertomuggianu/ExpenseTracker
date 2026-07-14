using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.Services;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class ExportViewModel(
    ICsvExportService csvExportService,
    IFileShareService fileShareService,
    AccountService accountService,
    CategoryService categoryService,
    IUserDialogService dialogs) : ViewModelBase
{
    private IReadOnlyList<Category> _allCategories = [];

    public IReadOnlyList<PeriodOption> Periods { get; } = PeriodOption.All;

    public IReadOnlyList<TransactionTypeOption> Types { get; } = TransactionTypeOption.Optional;

    public ObservableCollection<AccountFilterOption> Accounts { get; } = [];

    public ObservableCollection<MacroCategoryFilterOption> MacroCategories { get; } = [];

    public ObservableCollection<CategoryFilterOption> Categories { get; } = [];

    [ObservableProperty]
    public partial bool ExportAll { get; set; } = true;

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

    [RelayCommand]
    public Task LoadAsync() => RunAsync(async () =>
    {
        Accounts.Clear();
        Accounts.Add(new AccountFilterOption(null, "Tutti i conti"));
        foreach (var item in await accountService.GetAccountsAsync())
        {
            Accounts.Add(new AccountFilterOption(item.Account.Id, item.Account.Name));
        }

        SelectedAccount = Accounts[0];
        MacroCategories.Clear();
        MacroCategories.Add(new MacroCategoryFilterOption(null, "Tutte le macro-categorie"));
        foreach (var item in await categoryService.GetMacroCategoriesAsync())
        {
            MacroCategories.Add(new MacroCategoryFilterOption(item.Id, item.Name));
        }

        SelectedMacroCategory = MacroCategories[0];
        _allCategories = await categoryService.GetCategoriesAsync();
        RefreshCategories();
    });

    [RelayCommand]
    private Task ExportAsync() => RunAsync(async () =>
    {
        TransactionFilter? filter = null;
        if (!ExportAll)
        {
            var range = DateRangeFactory.Create(SelectedPeriod.Value, DateTime.Now, CustomFrom, CustomTo);
            filter = new TransactionFilter(
                range.FromUtc,
                range.ToUtcExclusive,
                SelectedAccount?.Id,
                SelectedType.Value,
                SelectedMacroCategory?.Id,
                SelectedCategory?.Id);
        }

        var export = await csvExportService.ExportAsync(filter);
        await fileShareService.ShareAsync(export);
        await dialogs.ShowMessageAsync("Esportazione pronta", "Il file CSV è stato creato ed è disponibile nel pannello di condivisione.");
    });

    partial void OnSelectedMacroCategoryChanged(MacroCategoryFilterOption? value) => RefreshCategories();

    private void RefreshCategories()
    {
        Categories.Clear();
        Categories.Add(new CategoryFilterOption(null, "Tutte le categorie"));
        var items = SelectedMacroCategory?.Id is Guid macroId
            ? _allCategories.Where(item => item.MacroCategoryId == macroId)
            : _allCategories;
        foreach (var item in items)
        {
            Categories.Add(new CategoryFilterOption(item.Id, item.Name));
        }

        SelectedCategory = Categories[0];
    }
}
