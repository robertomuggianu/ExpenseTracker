using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.Services;

namespace ExpenseTracker.Presentation.ViewModels;

public sealed record FrequencyOption(RepeatFrequency Value, string Label);
public sealed record RuleItem(RecurringRule Rule)
{
    public string Name => Rule.Name;
    public string Detail => $"{MoneyFormatter.Format(Rule.Amount, Rule.Currency)} · {Rule.NextDate:dd/MM/yyyy} · {(Rule.IsActive ? "Da confermare" : "Sospesa")}";
    public bool CanConfirm => Rule.IsActive && Rule.NextDate <= DateTime.Today;
    public string PauseLabel => Rule.IsActive ? "Sospendi" : "Riprendi";
}
public sealed record BudgetItem(BudgetProgress Item)
{
    public string Name => Item.CategoryName;
    public string Detail => $"Speso {MoneyFormatter.Format(Item.Spent, Item.Budget.Currency)} su {MoneyFormatter.Format(Item.Budget.Limit, Item.Budget.Currency)}";
    public string Remaining => Item.Remaining >= 0 ? $"Restano {MoneyFormatter.Format(Item.Remaining, Item.Budget.Currency)}" : $"Superato di {MoneyFormatter.Format(-Item.Remaining, Item.Budget.Currency)}";
    public double Progress => Item.Progress;
}
public partial class PlanningViewModel(PlanningService planning, TransferService transfers, AccountService accounts,
    CategoryService categories, IUserDialogService dialogs) : ViewModelBase
{
    public ObservableCollection<AccountSummary> Accounts { get; } = [];
    public ObservableCollection<Category> Categories { get; } = [];
    public ObservableCollection<Category> ExpenseCategories { get; } = [];
    public ObservableCollection<RuleItem> Rules { get; } = [];
    public ObservableCollection<BudgetItem> Budgets { get; } = [];
    public ObservableCollection<string> Currencies { get; } = [];
    public IReadOnlyList<TransactionTypeOption> Types => TransactionTypeOption.Required;
    public IReadOnlyList<FrequencyOption> Frequencies { get; } = [new(RepeatFrequency.Monthly, "Ogni mese"), new(RepeatFrequency.Weekly, "Ogni settimana"), new(RepeatFrequency.Yearly, "Ogni anno")];
    private IReadOnlyList<Category> allCategories = [];
    private RecurringRule? editingRule;
    [ObservableProperty] public partial bool ShowBudgets { get; set; } = true;
    [ObservableProperty] public partial bool ShowRecurring { get; set; }
    [ObservableProperty] public partial bool ShowTransfer { get; set; }
    [ObservableProperty] public partial string? StatusMessage { get; set; }
    [ObservableProperty] public partial DateTime Month { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public string MonthLabel => Month.ToString("MMMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
    [ObservableProperty] public partial Category? BudgetCategory { get; set; }
    [ObservableProperty] public partial string BudgetCurrency { get; set; } = "EUR";
    [ObservableProperty] public partial string BudgetAmount { get; set; } = "";
    [ObservableProperty] public partial string RuleName { get; set; } = "";
    [ObservableProperty] public partial string RuleAmount { get; set; } = "";
    [ObservableProperty] public partial AccountSummary? RuleAccount { get; set; }
    [ObservableProperty] public partial Category? RuleCategory { get; set; }
    [ObservableProperty] public partial TransactionTypeOption RuleType { get; set; } = TransactionTypeOption.Required[1];
    [ObservableProperty] public partial FrequencyOption? Frequency { get; set; }
    [ObservableProperty] public partial DateTime StartDate { get; set; } = DateTime.Today;
    [ObservableProperty] public partial bool IsRuleEditorVisible { get; set; }
    public bool CanChangeSchedule => editingRule is null;
    [ObservableProperty] public partial AccountSummary? SourceAccount { get; set; }
    [ObservableProperty] public partial AccountSummary? DestinationAccount { get; set; }
    [ObservableProperty] public partial string TransferAmount { get; set; } = "";
    [ObservableProperty] public partial DateTime TransferDate { get; set; } = DateTime.Today;
    [ObservableProperty] public partial string? TransferNotes { get; set; }

    [RelayCommand] private void SelectBudgets() { ShowBudgets = true; ShowRecurring = ShowTransfer = false; }
    [RelayCommand] private void SelectRecurring() { ShowRecurring = true; ShowBudgets = ShowTransfer = false; }
    [RelayCommand] private void SelectTransfer() { ShowTransfer = true; ShowBudgets = ShowRecurring = false; }
    [RelayCommand] public Task LoadAsync() => RunAsync(async () =>
    {
        var accountId = RuleAccount?.Account.Id;
        Accounts.Clear(); foreach (var a in await accounts.GetAccountsAsync()) if (a.Account.IsActive) Accounts.Add(a);
        RuleAccount = Accounts.FirstOrDefault(a => a.Account.Id == accountId) ?? Accounts.FirstOrDefault();
        SourceAccount ??= RuleAccount;
        Currencies.Clear(); foreach (var code in Accounts.Select(a => a.Account.Currency).Distinct()) Currencies.Add(code);
        if (!Currencies.Contains(BudgetCurrency)) BudgetCurrency = Currencies.FirstOrDefault() ?? "EUR";
        var categoryId = RuleCategory?.Id; var budgetCategoryId = BudgetCategory?.Id;
        allCategories = await categories.GetCategoriesAsync(activeOnly: true); RefreshCategories();
        RuleCategory = Categories.FirstOrDefault(c => c.Id == categoryId);
        ExpenseCategories.Clear(); foreach (var c in allCategories.Where(c => c.Type == TransactionType.Expense)) ExpenseCategories.Add(c);
        BudgetCategory = ExpenseCategories.FirstOrDefault(c => c.Id == budgetCategoryId);
        Frequency ??= Frequencies[0];
        await RefreshAsync();
    });
    private async Task RefreshAsync()
    {
        Rules.Clear(); foreach (var rule in (await planning.GetAsync()).Rules.OrderBy(r => !r.IsActive).ThenBy(r => r.NextDate)) Rules.Add(new(rule));
        Budgets.Clear(); foreach (var item in await planning.GetBudgetsAsync(Month)) Budgets.Add(new(item));
    }
    partial void OnRuleTypeChanged(TransactionTypeOption value) => RefreshCategories();
    private void RefreshCategories() { Categories.Clear(); foreach (var c in allCategories.Where(c => c.Type == RuleType.Value)) Categories.Add(c); RuleCategory = null; }
    [RelayCommand] private Task PreviousMonthAsync() => RunAsync(async () => { Month = Month.AddMonths(-1); OnPropertyChanged(nameof(MonthLabel)); await RefreshAsync(); });
    [RelayCommand] private Task NextMonthAsync() => RunAsync(async () => { Month = Month.AddMonths(1); OnPropertyChanged(nameof(MonthLabel)); await RefreshAsync(); });
    [RelayCommand] private Task SaveBudgetAsync() => RunAsync(async () =>
    {
        if (BudgetCategory is null || !AmountParser.TryParse(BudgetAmount, out var amount)) throw new DomainValidationException("Scegli categoria e importo del budget.");
        await planning.SaveBudgetAsync(BudgetCategory.Id, BudgetCurrency, Month, amount); BudgetAmount = ""; StatusMessage = "Budget salvato."; await RefreshAsync();
    });
    [RelayCommand] private void EditBudget(BudgetItem item) { BudgetCategory = ExpenseCategories.FirstOrDefault(c => c.Id == item.Item.Budget.CategoryId); BudgetCurrency = item.Item.Budget.Currency; BudgetAmount = item.Item.Budget.Limit.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("it-IT")); }
    [RelayCommand] private Task RemoveBudgetAsync(BudgetItem item) => RunAsync(async () => { if (await dialogs.ConfirmAsync("Elimina budget", "I movimenti resteranno invariati.", "Elimina", "Annulla")) { await planning.RemoveBudgetAsync(item.Item.Budget.Id); await RefreshAsync(); } });
    [RelayCommand] private void NewRule() { editingRule = null; RuleName = RuleAmount = ""; StartDate = DateTime.Today; Frequency = Frequencies[0]; IsRuleEditorVisible = true; OnPropertyChanged(nameof(CanChangeSchedule)); }
    [RelayCommand] private void EditRule(RuleItem item)
    {
        editingRule = item.Rule; RuleType = Types.Single(t => t.Value == item.Rule.Type); RuleName = item.Rule.Name;
        RuleAmount = item.Rule.Amount.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
        RuleAccount = Accounts.FirstOrDefault(a => a.Account.Id == item.Rule.AccountId); RuleCategory = Categories.FirstOrDefault(c => c.Id == item.Rule.CategoryId);
        StartDate = item.Rule.StartDate; Frequency = Frequencies.Single(f => f.Value == item.Rule.Frequency); IsRuleEditorVisible = true; OnPropertyChanged(nameof(CanChangeSchedule));
    }
    [RelayCommand] private void CancelRule() => IsRuleEditorVisible = false;
    [RelayCommand] private Task SaveRuleAsync() => RunAsync(async () =>
    {
        if (RuleAccount is null || RuleCategory is null || Frequency is null || !AmountParser.TryParse(RuleAmount, out var amount)) throw new DomainValidationException("Completa importo, conto, categoria e frequenza.");
        await planning.SaveRuleAsync(new(editingRule?.Id ?? Guid.NewGuid(), RuleName.Trim(), RuleAccount.Account.Id, RuleCategory.Id,
            RuleType.Value!.Value, amount, RuleAccount.Account.Currency, StartDate.Date, Frequency.Value, editingRule?.NextIndex ?? 0, editingRule?.IsActive ?? true));
        IsRuleEditorVisible = false; StatusMessage = "Ricorrenza salvata. Conferma ogni scadenza per registrarla."; await RefreshAsync();
    });
    [RelayCommand] private Task ConfirmRuleAsync(RuleItem item) => RunAsync(async () => { await planning.ConfirmAsync(item.Rule.Id, item.Rule.NextDate, DateTime.Today); StatusMessage = "Movimento registrato."; await RefreshAsync(); });
    [RelayCommand] private Task SkipRuleAsync(RuleItem item) => RunAsync(async () => { if (await dialogs.ConfirmAsync("Salta scadenza", $"Saltare il {item.Rule.NextDate:dd/MM/yyyy} senza registrare movimenti?", "Salta", "Annulla")) { await planning.SkipAsync(item.Rule.Id, item.Rule.NextDate); await RefreshAsync(); } });
    [RelayCommand] private Task PauseRuleAsync(RuleItem item) => RunAsync(async () => { await planning.SetRuleActiveAsync(item.Rule.Id, !item.Rule.IsActive); await RefreshAsync(); });
    [RelayCommand] private Task RemoveRuleAsync(RuleItem item) => RunAsync(async () => { if (await dialogs.ConfirmAsync("Elimina ricorrenza", "I movimenti già confermati resteranno nello storico.", "Elimina", "Annulla")) { await planning.RemoveRuleAsync(item.Rule.Id); await RefreshAsync(); } });
    [RelayCommand] private Task SaveTransferAsync() => RunAsync(async () =>
    {
        if (SourceAccount is null || DestinationAccount is null || !AmountParser.TryParse(TransferAmount, out var amount)) throw new DomainValidationException("Completa conti e importo del trasferimento.");
        await transfers.SaveAsync(SourceAccount.Account.Id, DestinationAccount.Account.Id, amount, TransferDate, TransferNotes);
        TransferAmount = ""; TransferNotes = null; StatusMessage = "Trasferimento registrato su entrambi i conti.";
    });
}
