using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.Services;

namespace ExpenseTracker.Presentation.ViewModels;

public partial class TransactionsViewModel(TransactionService transactionService, AccountService accountService,
    CategoryService categoryService, IUserDialogService dialogs, IAppNavigationService navigation, NavigationRequests requests) : ViewModelBase
{
    private bool loadingFilters;
    private int filterVersion;
    private int visibleCount = 100;
    private int searchVersion;
    public IReadOnlyList<PeriodOption> Periods => PeriodOption.All;
    public IReadOnlyList<TransactionTypeOption> Types => TransactionTypeOption.Optional;
    public ObservableCollection<AccountFilterOption> Accounts { get; } = [];
    public ObservableCollection<CategoryFilterOption> Categories { get; } = [];
    public ObservableCollection<string> Currencies { get; } = [];
    public ObservableCollection<TransactionListItem> Transactions { get; } = [];
    public ObservableCollection<TransactionDayGroup> Groups { get; } = [];
    [ObservableProperty] public partial bool FiltersVisible { get; set; }
    [ObservableProperty] public partial PeriodOption SelectedPeriod { get; set; } = PeriodOption.All[1];
    [ObservableProperty] public partial DateTime CustomFrom { get; set; } = DateTime.Today.AddMonths(-1);
    [ObservableProperty] public partial DateTime CustomTo { get; set; } = DateTime.Today;
    [ObservableProperty] public partial DateTime ReferenceDate { get; set; } = DateTime.Today;
    [ObservableProperty] public partial AccountFilterOption? SelectedAccount { get; set; }
    [ObservableProperty] public partial TransactionTypeOption SelectedType { get; set; } = TransactionTypeOption.Optional[0];
    [ObservableProperty] public partial CategoryFilterOption? SelectedCategory { get; set; }
    [ObservableProperty] public partial string SelectedCurrency { get; set; } = "Tutte le valute";
    [ObservableProperty] public partial string SearchText { get; set; } = "";
    [ObservableProperty] public partial string FilterSummary { get; set; } = "";
    [ObservableProperty] public partial string? StatusMessage { get; set; }
    [ObservableProperty] public partial string TotalsLabel { get; set; } = "";
    [ObservableProperty] public partial bool HasMore { get; set; }
    [ObservableProperty] public partial TransactionListItem? SelectedTransaction { get; set; }
    [ObservableProperty] public partial bool HasSelectedTransaction { get; set; }
    public bool IsCustomPeriod => SelectedPeriod.Value == DateRangePreset.Custom;
    public bool IsReferencePeriod => SelectedPeriod.Value is DateRangePreset.SelectedMonth or DateRangePreset.SelectedYear;
    [RelayCommand] private void ToggleFilters() => FiltersVisible = !FiltersVisible;
    [RelayCommand] private void CloseDetail() => SelectedTransaction = null;
    [RelayCommand] private void ShowDetail(TransactionListItem item) => SelectedTransaction = item;
    [RelayCommand] private Task EditAsync(TransactionListItem item) => navigation.GoToTransactionEditorAsync(item.Transaction.Id);
    [RelayCommand] private Task DuplicateAsync(TransactionListItem item) => navigation.GoToTransactionEditorAsync(item.Transaction.Id, true);
    [RelayCommand] private Task NewTransactionAsync() => navigation.GoToTransactionEditorAsync();
    partial void OnSelectedTransactionChanged(TransactionListItem? value) => HasSelectedTransaction = value is not null;
    partial void OnSelectedPeriodChanged(PeriodOption value) { OnPropertyChanged(nameof(IsCustomPeriod)); OnPropertyChanged(nameof(IsReferencePeriod)); RequestRefresh(); }
    partial void OnSelectedAccountChanged(AccountFilterOption? value) => RequestRefresh();
    partial void OnSelectedCategoryChanged(CategoryFilterOption? value) => RequestRefresh();
    partial void OnSelectedTypeChanged(TransactionTypeOption value) => RequestRefresh();
    partial void OnSelectedCurrencyChanged(string value) => RequestRefresh();
    partial void OnCustomFromChanged(DateTime value) { if (IsCustomPeriod) RequestRefresh(); }
    partial void OnCustomToChanged(DateTime value) { if (IsCustomPeriod) RequestRefresh(); }
    partial void OnReferenceDateChanged(DateTime value) { if (IsReferencePeriod) RequestRefresh(); }
    partial void OnSearchTextChanged(string value) { var version = ++searchVersion; _ = DebounceSearchAsync(version); }
    private async Task DebounceSearchAsync(int version) { await Task.Delay(250); if (version == searchVersion) RequestRefresh(); }
    private void RequestRefresh() { filterVersion++; visibleCount = 100; if (!loadingFilters) _ = ApplyFiltersAsync(); }
    [RelayCommand] private Task ApplyFiltersAsync() => RunAsync(LoadLatestAsync);
    [RelayCommand] private Task LoadMoreAsync() => RunAsync(async () => { visibleCount += 100; await LoadLatestAsync(); });
    [RelayCommand] public Task LoadAsync() => RunAsync(async () =>
    {
        StatusMessage = requests.Message; requests.Message = null;
        loadingFilters = true;
        try
        {
            var accountId = SelectedAccount?.Id; var categoryId = SelectedCategory?.Id;
            var accountItems = await accountService.GetAccountsAsync();
            Accounts.Clear(); Accounts.Add(new(null, "Tutti i conti")); foreach (var a in accountItems) Accounts.Add(new(a.Account.Id, a.Account.Name));
            Categories.Clear(); Categories.Add(new(null, "Tutte le categorie")); foreach (var c in await categoryService.GetCategoriesAsync()) Categories.Add(new(c.Id, c.Name));
            SelectedAccount = Accounts.FirstOrDefault(a => a.Id == accountId) ?? Accounts[0]; SelectedCategory = Categories.FirstOrDefault(c => c.Id == categoryId) ?? Categories[0];
            Currencies.Clear(); Currencies.Add("Tutte le valute"); foreach (var currency in accountItems.Select(a => a.Account.Currency).Distinct()) Currencies.Add(currency);
            if (requests.Filter is { } pending)
            {
                requests.Filter = null;
                SelectedPeriod = Periods.Single(p => p.Value == DateRangePreset.Custom);
                CustomFrom = pending.FromUtc?.ToLocalTime().Date ?? DateTime.Today;
                CustomTo = pending.ToUtcExclusive?.ToLocalTime().AddDays(-1).Date ?? DateTime.Today;
                SelectedAccount = Accounts.FirstOrDefault(a => a.Id == pending.AccountId) ?? Accounts[0];
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == pending.CategoryId) ?? Categories[0];
                SelectedType = Types.First(t => t.Value == pending.Type); SelectedCurrency = pending.Currency ?? "Tutte le valute";
                SearchText = pending.Search ?? "";
            }
        }
        finally { loadingFilters = false; }
        await LoadLatestAsync();
    });
    private async Task LoadLatestAsync()
    {
        int version;
        do { version = filterVersion; await LoadCoreAsync(version); } while (version != filterVersion);
    }
    private async Task LoadCoreAsync(int version)
    {
        if (IsCustomPeriod && CustomFrom.Date > CustomTo.Date)
        { Groups.Clear(); Transactions.Clear(); SelectedTransaction = null; TotalsLabel = ""; FilterSummary = "Intervallo non valido"; HasMore = false; throw new DomainValidationException("La data iniziale deve precedere quella finale."); }
        var range = DateRangeFactory.Create(SelectedPeriod.Value, IsReferencePeriod ? ReferenceDate : DateTime.Now, CustomFrom, CustomTo);
        var filter = new TransactionFilter(range.FromUtc, range.ToUtcExclusive, SelectedAccount?.Id, SelectedType.Value, SelectedCategory?.Id,
            SearchText, SelectedCurrency == "Tutte le valute" ? null : SelectedCurrency, visibleCount + 1);
        var items = await transactionService.GetTransactionsAsync(filter);
        if (version != filterVersion) return;
        HasMore = items.Count > visibleCount;
        var displayed = items.Take(visibleCount).ToArray();
        var selectedId = SelectedTransaction?.Transaction.Id;
        Transactions.Clear(); foreach (var item in displayed) Transactions.Add(item);
        Groups.Clear(); foreach (var group in displayed.GroupBy(i => i.LocalDate.Date).OrderByDescending(g => g.Key)) Groups.Add(new(group.Key, group));
        SelectedTransaction = displayed.FirstOrDefault(i => i.Transaction.Id == selectedId);
        FilterSummary = $"{range.FromUtc.ToLocalTime():dd/MM/yyyy} – {range.ToUtcExclusive.ToLocalTime().AddDays(-1):dd/MM/yyyy} · {SelectedAccount?.Name} · {SelectedType.Label} · {SelectedCategory?.Name} · {SelectedCurrency}";
        TotalsLabel = $"{displayed.Length} movimenti visualizzati" + (HasMore ? " · altri disponibili" : "") + "\n" +
            string.Join(" · ", displayed.Where(i => i.Transaction.TransferId is null).GroupBy(i => i.Transaction.Currency)
                .Select(g => $"{g.Key}: entrate {MoneyFormatter.Format(g.Where(i => i.SignedAmount > 0).Sum(i => i.SignedAmount), g.Key)}, spese {MoneyFormatter.Format(-g.Where(i => i.SignedAmount < 0).Sum(i => i.SignedAmount), g.Key)}"));
    }
    [RelayCommand] private Task DeleteAsync(TransactionListItem item) => RunAsync(async () =>
    {
        var message = item.Transaction.TransferId.HasValue ? "Eliminare entrambe le registrazioni del trasferimento?" : $"Eliminare il movimento da {MoneyFormatter.Format(item.Transaction.Amount, item.Transaction.Currency)}?";
        if (!await dialogs.ConfirmAsync("Elimina movimento", message, "Elimina", "Annulla")) return;
        await transactionService.DeleteAsync(item.Transaction.Id); SelectedTransaction = null; await LoadLatestAsync();
    });
}
public sealed class TransactionDayGroup(DateTime date, IEnumerable<TransactionListItem> items) : ObservableCollection<TransactionListItem>(items)
{
    public string Title => date == DateTime.Today ? "Oggi" : date == DateTime.Today.AddDays(-1) ? "Ieri" : date.ToString("dddd d MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
}
