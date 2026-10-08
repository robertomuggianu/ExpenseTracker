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
    TransactionImportService importService,
    IImportFilePickerService importFilePicker,
    AccountService accountService,
    CategoryService categoryService,
    IUserDialogService dialogs) : ViewModelBase
{
    public IReadOnlyList<PeriodOption> Periods { get; } = PeriodOption.All;

    public IReadOnlyList<TransactionTypeOption> Types { get; } = TransactionTypeOption.Optional;

    public ObservableCollection<AccountFilterOption> Accounts { get; } = [];

    public ObservableCollection<CategoryFilterOption> Categories { get; } = [];

    [ObservableProperty]
    public partial bool ExportAll { get; set; } = true;

    public bool UseExportFilters => !ExportAll;

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
    public partial string? ImportSummary { get; set; }

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
        Categories.Clear();
        Categories.Add(new CategoryFilterOption(null, "Tutte le categorie"));
        foreach (var item in await categoryService.GetCategoriesAsync())
        {
            Categories.Add(new CategoryFilterOption(item.Id, item.Name));
        }

        SelectedCategory = Categories[0];
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
                SelectedCategory?.Id);
        }

        var export = await csvExportService.ExportAsync(filter);
        await fileShareService.ShareAsync(export);
        await dialogs.ShowMessageAsync("Esportazione pronta", "Il file CSV è stato creato ed è disponibile nel pannello di condivisione.");
    });

    [RelayCommand]
    private Task ImportAsync() => RunAsync(async () =>
    {
        await using var selectedFile = await importFilePicker.PickAsync();
        if (selectedFile is null)
        {
            return;
        }

        var confirmed = await dialogs.ConfirmAsync(
            "Importa movimenti",
            $"Importare i dati da {selectedFile.FileName}? Se il file è già stato importato, i movimenti esistenti verranno aggiornati senza duplicati.",
            "Importa",
            "Annulla");
        if (!confirmed)
        {
            return;
        }

        var result = await importService.ImportAsync(selectedFile.Content, selectedFile.FileName);
        ImportSummary = $"{result.Created} nuovi · {result.Updated} aggiornati · {result.CategoriesCreated} categorie create";
        var warningText = result.Warnings.Count == 0
            ? string.Empty
            : $"\n\nNote: {string.Join(" ", result.Warnings.Take(3))}";
        await dialogs.ShowMessageAsync(
            "Importazione completata",
            $"Elaborati {result.TotalProcessed} movimenti. Creati {result.AccountsCreated} conti e {result.CategoriesCreated} categorie.{warningText}");
    });

    partial void OnExportAllChanged(bool value) => OnPropertyChanged(nameof(UseExportFilters));
}
