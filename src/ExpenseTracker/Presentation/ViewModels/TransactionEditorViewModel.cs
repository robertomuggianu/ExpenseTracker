using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
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
    IAppNavigationService navigation, IUserDialogService dialogs, IAppPreferences preferences) : ViewModelBase
{
    private bool initialized;
    private Guid? defaultAccountId;
    private TimeSpan defaultTime;
    public bool ReturnToHome { get; set; }
    private Guid? originalCategoryId;
    [ObservableProperty] public partial string? StatusMessage { get; set; }
    [ObservableProperty] public partial string? AmountError { get; set; }
    [ObservableProperty] public partial string? CategoryError { get; set; }
    [ObservableProperty] public partial string? AccountError { get; set; }
    [ObservableProperty] public partial bool RememberAccount { get; set; } = preferences.Get("remember-account", false);
    [ObservableProperty] public partial bool IsFormReady { get; set; }
    partial void OnRememberAccountChanged(bool value) => preferences.Set("remember-account", value);
    public void StoreDraft()
    {
        if (!initialized || !IsFormReady || IsBusy) return;
        if (!HasDraft)
        { preferences.Remove("transaction-draft"); return; }
        preferences.Set("transaction-draft", TransactionDraftSerializer.Serialize(new TransactionEditorDraft(EditingId, SelectedType.Value!.Value,
            SelectedAccount?.Account.Id, SelectedCategory?.Id, Date, Time, Amount, Description, Notes, ShowDetails)));
    }
    private bool HasDraft => EditingId is not null || SelectedType.Value != TransactionType.Expense
        || SelectedCategory is not null || SelectedAccount?.Account.Id != defaultAccountId
        || Date.Date != DateTime.Today || Time != defaultTime || ShowDetails
        || !string.IsNullOrWhiteSpace(Amount) || !string.IsNullOrWhiteSpace(Description) || !string.IsNullOrWhiteSpace(Notes);
    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (initialized && !IsBusy && e.PropertyName is nameof(Amount) or nameof(Date) or nameof(Time)
            or nameof(Description) or nameof(Notes) or nameof(SelectedAccount) or nameof(SelectedCategory) or nameof(SelectedType) or nameof(ShowDetails)) StoreDraft();
    }
    private IReadOnlyList<Category> _allCategories = [];
    private IReadOnlyList<Guid> _recentCategoryRanking = [];

    [ObservableProperty]
    public partial bool ShowDetails { get; set; }

    [RelayCommand]
    private void ToggleDetails() => ShowDetails = !ShowDetails;

    [RelayCommand]
    private void ChooseCategory(Category category)
    {
        SelectedCategory = category;
        ShowAllCategories = false;
    }

    [RelayCommand]
    private void ToggleCategoryBrowser() => ShowAllCategories = !ShowAllCategories;

    public IReadOnlyList<TransactionTypeOption> Types { get; } = TransactionTypeOption.Required;

    public ObservableCollection<AccountSummary> Accounts { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOtherCategories))]
    public partial IReadOnlyList<Category> Categories { get; set; } = [];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOtherCategories))]
    public partial IReadOnlyList<Category> QuickCategories { get; set; } = [];

    [ObservableProperty] public partial int QuickCategoryCapacity { get; set; } = 4;

    partial void OnQuickCategoryCapacityChanged(int value) => RefreshCategories();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CategoryPickerToggleText))]
    [NotifyPropertyChangedFor(nameof(HasNoCategoryMatches))]
    public partial bool ShowAllCategories { get; set; }

    [ObservableProperty] public partial string CategorySearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoCategoryMatches))]
    public partial IReadOnlyList<Category> VisibleCategories { get; set; } = [];

    public string CategoryPickerToggleText => ShowAllCategories ? "Chiudi ⌃" : "Altre ⌄";
    public bool HasOtherCategories => Categories.Count > QuickCategories.Count;
    public bool HasNoCategoryMatches => ShowAllCategories && VisibleCategories.Count == 0;

    partial void OnShowAllCategoriesChanged(bool value)
    {
        if (!value) CategorySearchText = string.Empty;
        RefreshVisibleCategories();
    }

    partial void OnCategorySearchTextChanged(string value) => RefreshVisibleCategories();

    private void RefreshVisibleCategories()
    {
        var search = CategorySearchText.Trim();
        VisibleCategories = ShowAllCategories
            ? Categories.Where(category => (search.Length > 0 || !QuickCategories.Any(quick => quick.Id == category.Id))
                    && CultureInfo.CurrentCulture.CompareInfo.IndexOf(
                    category.Name, search, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
                .OrderBy(category => category.Name, StringComparer.CurrentCultureIgnoreCase).ToArray()
            : [];
    }

    [ObservableProperty]
    public partial Guid? EditingId { get; set; }

    [ObservableProperty]
    public partial TransactionTypeOption SelectedType { get; set; } = TransactionTypeOption.Required[1];

    [ObservableProperty]
    public partial AccountSummary? SelectedAccount { get; set; }

    [ObservableProperty]
    public partial Category? SelectedCategory { get; set; }

    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (value is null) return;
        if (ErrorMessage == CategoryError) ErrorMessage = null;
        CategoryError = null;
    }

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

    public async Task LoadAsync(Guid? transactionId = null, bool duplicate = false, TransactionType? initialType = null)
    {
        var openingExisting = transactionId.HasValue;
        await RunAsync(async () =>
        {
            TransactionEditorDraft? draft = initialized && HasDraft && transactionId is null
                ? new(EditingId, SelectedType.Value!.Value, SelectedAccount?.Account.Id, SelectedCategory?.Id, Date, Time, Amount, Description, Notes, ShowDetails)
                : null;
            if (initialized && transactionId.HasValue && !string.IsNullOrWhiteSpace(Amount)
                && !await dialogs.ConfirmAsync("Bozza in corso", "Aprire il movimento sostituirà la bozza attuale.", "Continua", "Annulla")) return;
            IsFormReady = false;
            StatusMessage = null;
            if (openingExisting) Title = duplicate ? "Duplica movimento" : "Modifica movimento";
            if (!transactionId.HasValue)
            {
                try { draft ??= TransactionDraftSerializer.Deserialize(preferences.Get("transaction-draft", "null")); }
                catch (JsonException) { preferences.Remove("transaction-draft"); }
                transactionId = draft?.Id;
            }

            var original = transactionId.HasValue ? await transactionService.GetTransactionAsync(transactionId.Value) : null;
            if (transactionId.HasValue && original is null)
            {
                if (openingExisting) throw new DomainValidationException("Il movimento selezionato non esiste più.");
                preferences.Remove("transaction-draft");
                draft = null; transactionId = null; EditingId = null; originalCategoryId = null;
                StatusMessage = "Il movimento non esiste più. Puoi inserirne uno nuovo.";
            }
            if (original?.TransferId is not null) throw new DomainValidationException("Gestisci i trasferimenti dalla sezione Pianifica. Per correggerne uno, elimina la coppia e ricreala.");
            EditingId = duplicate ? null : transactionId;
            Title = transactionId.HasValue ? "Modifica movimento" : "Nuovo movimento";
            originalCategoryId = duplicate ? null : original?.CategoryId;
            Accounts.Clear();
            foreach (var item in (await accountService.GetAccountsAsync()).Where(item => item.Account.IsActive || (!duplicate && item.Account.Id == original?.AccountId)))
            {
                Accounts.Add(item);
            }

            _allCategories = (await categoryService.GetCategoriesAsync()).Where(c => c.IsActive || c.Id == originalCategoryId).ToArray();
            _recentCategoryRanking = await transactionService.GetRecentCategoryRankingAsync();
            ShowDetails = transactionId.HasValue;

            if (transactionId.HasValue)
            {
                var item = original!;
                var localDate = item.OccurredAtUtc.ToLocalTime();
                SelectedType = Types.Single(option => option.Value == item.Type);
                SelectedAccount = Accounts.SingleOrDefault(account => account.Account.Id == item.AccountId);
                RefreshCategories();
                SelectedCategory = Categories.SingleOrDefault(category => category.Id == item.CategoryId);
                Date = localDate.Date;
                Time = localDate.TimeOfDay;
                Amount = item.Amount.ToString("0.00", CultureInfo.GetCultureInfo("it-IT"));
                Currency = SelectedAccount?.Account.Currency ?? item.Currency;
                Description = item.Description;
                Notes = item.Notes;
            }
            else
            {
                ResetForm(initialType);
            }
            if (duplicate) { EditingId = null; Date = DateTime.Today; Time = DateTime.Now.TimeOfDay; Title = "Duplica movimento"; }
            if (draft is not null)
            {
                SelectedType = Types.FirstOrDefault(t => t.Value == draft.Type) ?? Types[1];
                SelectedAccount = Accounts.FirstOrDefault(a => a.Account.Id == draft.Account);
                RefreshCategories(); SelectedCategory = Categories.FirstOrDefault(c => c.Id == draft.Category);
                Date = draft.Date; Time = draft.Time; Amount = draft.Amount; Description = draft.Description; Notes = draft.Notes; ShowDetails = draft.Details;
                StatusMessage = "Bozza ripristinata.";
            }
            initialized = true;
            IsFormReady = true;
        });
        StoreDraft();
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(() => SaveCoreAsync(false));
    [RelayCommand] private async Task SaveAndAddAsync()
    {
        await RunAsync(() => SaveCoreAsync(true));
        StoreDraft();
    }
    [RelayCommand] private Task DiscardDraftAsync() => RunAsync(async () =>
    {
        if (await dialogs.ConfirmAsync("Scarta bozza", "Vuoi svuotare i campi? I movimenti salvati restano invariati.", "Scarta", "Annulla"))
        { EditingId = null; originalCategoryId = null; ResetForm(); Title = "Nuovo movimento"; preferences.Remove("transaction-draft"); StatusMessage = null; }
    });
    private async Task SaveCoreAsync(bool addAnother)
    {
        if (!IsFormReady) throw new DomainValidationException("Apri un movimento valido prima di salvare.");
        AmountError = CategoryError = AccountError = null; StatusMessage = null;
        if (SelectedAccount is null)
        {
            AccountError = "Seleziona un conto."; throw new DomainValidationException(AccountError);
        }

        if (SelectedCategory is null)
        {
            CategoryError = "Seleziona una categoria."; throw new DomainValidationException(CategoryError);
        }

        if (!AmountParser.TryParse(Amount, out var parsedAmount) || parsedAmount <= 0 || decimal.Round(parsedAmount, 2) != parsedAmount)
        {
            AmountError = "Inserisci un importo positivo con al massimo due decimali."; throw new DomainValidationException(AmountError);
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

        if (RememberAccount) preferences.Set("last-account", SelectedAccount.Account.Id.ToString());
        preferences.Remove("transaction-draft");
        _recentCategoryRanking = await transactionService.GetRecentCategoryRankingAsync();
        var nextType = addAnother ? SelectedType.Value : null;
        EditingId = null; originalCategoryId = null; ResetForm(nextType); Title = "Nuovo movimento";
        StatusMessage = "Movimento salvato.";
        if (!addAnother)
        {
            if (ReturnToHome) await navigation.GoToDashboardAsync();
            else await navigation.GoToTransactionsAsync(message: "Movimento salvato.");
        }
    }

    partial void OnSelectedTypeChanged(TransactionTypeOption value)
    {
        ShowAllCategories = false;
        CategorySearchText = string.Empty;
        SelectedCategory = null;
        RefreshCategories();
        CategoryError = null;
    }

    private void ResetForm(TransactionType? initialType = null)
    {
        AmountError = CategoryError = AccountError = null;
        ShowDetails = false;
        ShowAllCategories = false;
        CategorySearchText = string.Empty;
        SelectedType = Types.FirstOrDefault(t => t.Value == initialType) ?? Types[1];
        var remembered = RememberAccount && Guid.TryParse(preferences.Get("last-account", ""), out var id) ? id : (Guid?)null;
        SelectedAccount = Accounts.FirstOrDefault(item => item.Account.IsActive && item.Account.Id == remembered)
            ?? Accounts.FirstOrDefault(item => item.Account.IsPrimary) ?? Accounts.FirstOrDefault(item => item.Account.IsActive);
        RefreshCategories();
        SelectedCategory = null;
        Date = DateTime.Today;
        Time = DateTime.Now.TimeOfDay;
        defaultTime = Time;
        defaultAccountId = SelectedAccount?.Account.Id;
        Amount = string.Empty;
        Currency = SelectedAccount?.Account.Currency ?? "EUR";
        Description = null;
        Notes = null;
    }

    private void RefreshCategories()
    {
        var selectedId = SelectedCategory?.Id;
        var ranking = _recentCategoryRanking.Select((id, index) => (id, index)).ToDictionary(item => item.id, item => item.index);
        Categories = _allCategories.Where(item => item.Type == SelectedType.Value && (item.IsActive || item.Id == originalCategoryId))
            .OrderBy(item => ranking.GetValueOrDefault(item.Id, int.MaxValue)).ThenBy(item => item.Name).ToArray();
        QuickCategories = Categories.Take(Math.Max(1, QuickCategoryCapacity)).ToArray();
        if (!HasOtherCategories) ShowAllCategories = false;
        RefreshVisibleCategories();
        SelectedCategory = Categories.FirstOrDefault(item => item.Id == selectedId);
    }

    partial void OnSelectedAccountChanged(AccountSummary? value)
    {
        Currency = value?.Account.Currency ?? "EUR";
    }
}
