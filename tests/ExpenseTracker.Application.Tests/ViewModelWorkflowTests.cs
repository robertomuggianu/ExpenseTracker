using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Presentation.Services;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Application.Tests;

public sealed class ViewModelWorkflowTests
{
    [Fact] public Task Transactions_NavigationFilterArrivingDuringQueryIsApplied() => WithDatabase(async repo =>
    {
        var account = Assert.Single(await repo.GetAccountsAsync());
        var categories = (await repo.GetCategoriesAsync()).Where(c => c.Type == TransactionType.Expense).Take(2).ToArray();
        foreach (var category in categories)
            await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, account.Id, category.Id, 10m));
        var delayed = new DelayedRepository(repo); var requests = new NavigationRequests();
        var vm = new TransactionsViewModel(new(delayed), new(repo), new(repo), new Dialogs(), new Navigation(), requests);
        await vm.LoadAsync();
        delayed.DelayNextQuery = true;
        var loading = vm.LoadAsync();
        await delayed.QueryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var range = MonthlyPeriods.FullMonth(DateTime.Today);
        requests.Filter = new(range.FromUtc, range.ToUtcExclusive, CategoryId: categories[1].Id);
        await vm.LoadAsync(); // Page appears while a previous query is still running.
        delayed.ContinueQuery.SetResult();
        await loading.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(requests.Filter); Assert.Equal(categories[1].Id, vm.SelectedCategory?.Id);
        Assert.Equal(categories[1].Id, Assert.Single(vm.Transactions).Transaction.CategoryId);
    });

    [Fact] public Task Transactions_TotalsSeparateCurrenciesExcludeTransfersAndResetFilters() => WithDatabase(async repo =>
    {
        var account = Assert.Single(await repo.GetAccountsAsync());
        var usd = new Account(Guid.NewGuid(), "Dollari", 0m, "USD");
        var cash = new Account(Guid.NewGuid(), "Contanti", 0m);
        await repo.SaveAccountsAsync([usd, cash]);
        var categories = await repo.GetCategoriesAsync();
        var expense = categories.First(c => c.Type == TransactionType.Expense);
        var income = categories.First(c => c.Type == TransactionType.Income);
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, account.Id, expense.Id, 6.50m));
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Income, account.Id, income.Id, 100m));
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, usd.Id, expense.Id, 9m, "USD"));
        await new TransferService(repo).SaveAsync(account.Id, cash.Id, 50m, DateTime.Today, null);
        var vm = new TransactionsViewModel(new(repo), new(repo), new(repo), new Dialogs(), new Navigation(), new NavigationRequests());
        await vm.LoadAsync();
        Assert.Equal(5, vm.Transactions.Count);
        Assert.Equal(new VisibleCurrencyTotals("EUR", 100m, 6.50m), vm.VisibleTotals.Single(t => t.Currency == "EUR"));
        Assert.Equal(new VisibleCurrencyTotals("USD", 0m, 9m), vm.VisibleTotals.Single(t => t.Currency == "USD"));
        vm.SelectedCurrency = "USD"; await SettledAsync(vm); Assert.Single(vm.Transactions);
        await vm.ResetFiltersCommand.ExecuteAsync(null); await SettledAsync(vm);
        Assert.Equal("Tutte le valute", vm.SelectedCurrency); Assert.Equal(5, vm.Transactions.Count);
        vm.SelectedPeriod = vm.Periods.Single(p => p.Value == DateRangePreset.Custom);
        vm.CustomFrom = DateTime.Today.AddDays(1); vm.CustomTo = DateTime.Today;
        await SettledAsync(vm);
        Assert.Empty(vm.Transactions); Assert.Empty(vm.VisibleTotals); Assert.False(vm.HasMore); Assert.NotNull(vm.ErrorMessage);
    });

    [Fact] public Task Planning_AccountSelectionsRefreshAndArchivedAccountIsCleared() => WithDatabase(async repo =>
    {
        var account = Assert.Single(await repo.GetAccountsAsync());
        var destination = new Account(Guid.NewGuid(), "Contanti", 0m); await repo.SaveAccountsAsync([destination]);
        var vm = new PlanningViewModel(new(repo), new(repo), new(repo), new(repo), new Dialogs());
        await vm.LoadAsync(); vm.DestinationAccount = vm.Accounts.Single(a => a.Account.Id == destination.Id);
        account.Update("Nome aggiornato", 100m, "EUR"); destination.SetActive(false);
        await repo.SaveAccountsAsync([account, destination]); await vm.LoadAsync();
        Assert.Equal("Nome aggiornato", vm.SourceAccount?.Account.Name);
        Assert.Null(vm.DestinationAccount); Assert.DoesNotContain(vm.Accounts, a => a.Account.Id == destination.Id);
        Assert.True(vm.CanInteract);
    });

    [Fact] public Task Planning_DeletingEditedRecurrenceClosesEditor() => WithDatabase(async repo =>
    {
        var account = Assert.Single(await repo.GetAccountsAsync());
        var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
        var planning = new PlanningService(repo);
        await planning.SaveRuleAsync(new(Guid.NewGuid(), "Rata", account.Id, category.Id, TransactionType.Expense, 10m,
            "EUR", DateTime.Today, RepeatFrequency.Monthly));
        var vm = new PlanningViewModel(planning, new(repo), new(repo), new(repo), new Dialogs());
        await vm.LoadAsync(); var item = Assert.Single(vm.Rules);
        vm.EditRuleCommand.Execute(item); Assert.True(vm.IsRuleEditorVisible);
        await vm.RemoveRuleCommand.ExecuteAsync(item);
        Assert.False(vm.IsRuleEditorVisible); Assert.Empty(vm.Rules); Assert.True(vm.CanChangeSchedule);
    });

    private sealed class DelayedRepository(IExpenseRepository inner) : IExpenseRepository
    {
        public bool DelayNextQuery { get; set; }
        public TaskCompletionSource QueryStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ContinueQuery { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyList<Transaction>> GetTransactionsAsync(TransactionFilter? filter = null, CancellationToken cancellationToken = default)
        {
            if (DelayNextQuery)
            {
                DelayNextQuery = false; QueryStarted.SetResult(); await ContinueQuery.Task;
            }
            return await inner.GetTransactionsAsync(filter, cancellationToken);
        }
        public Task InitializeAsync(CancellationToken cancellationToken = default) => inner.InitializeAsync(cancellationToken);
        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default) => inner.GetAccountsAsync(cancellationToken);
        public Task<Account?> GetAccountAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetAccountAsync(id, cancellationToken);
        public Task SaveAccountsAsync(IReadOnlyCollection<Account> accounts, CancellationToken cancellationToken = default) => inner.SaveAccountsAsync(accounts, cancellationToken);
        public Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default) => inner.GetCategoriesAsync(cancellationToken);
        public Task<Category?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetCategoryAsync(id, cancellationToken);
        public Task SaveCategoryAsync(Category category, CancellationToken cancellationToken = default) => inner.SaveCategoryAsync(category, cancellationToken);
        public Task<Transaction?> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default) => inner.GetTransactionAsync(id, cancellationToken);
        public Task SaveTransactionAsync(Transaction transaction, CancellationToken cancellationToken = default) => inner.SaveTransactionAsync(transaction, cancellationToken);
        public Task DeleteTransactionAsync(Guid id, CancellationToken cancellationToken = default) => inner.DeleteTransactionAsync(id, cancellationToken);
        public Task SaveBatchAsync(IReadOnlyCollection<Account> accounts, IReadOnlyCollection<Category> categories, IReadOnlyCollection<Transaction> transactions, string? stateKey = null, string? stateValue = null, CancellationToken cancellationToken = default) => inner.SaveBatchAsync(accounts, categories, transactions, stateKey, stateValue, cancellationToken);
        public Task<IReadOnlyList<AccountSummary>> GetAccountSummariesAsync(CancellationToken cancellationToken = default) => inner.GetAccountSummariesAsync(cancellationToken);
        public Task<string?> GetStateAsync(string key) => inner.GetStateAsync(key);
        public Task SaveStateAsync(string key, string value) => inner.SaveStateAsync(key, value);
        public Task<IReadOnlyDictionary<Guid, int>> GetCategoryUsageAsync() => inner.GetCategoryUsageAsync();
    }

    private sealed class PreferencesStore : IAppPreferences
    {
        private readonly Dictionary<string, object> values = [];
        public string Get(string key, string fallback) => values.TryGetValue(key, out var v) ? (string)v : fallback;
        public bool Get(string key, bool fallback) => values.TryGetValue(key, out var v) ? (bool)v : fallback;
        public void Set(string key, string value) => values[key] = value;
        public void Set(string key, bool value) => values[key] = value;
        public void Remove(string key) => values.Remove(key);
    }
    private sealed class Navigation : IAppNavigationService
    {
        public TransactionFilter? LastFilter { get; private set; }
        public Task GoToTransactionEditorAsync(Guid? transactionId = null, bool duplicate = false) => Task.CompletedTask;
        public Task GoToTransactionsAsync(TransactionFilter? filter = null, string? message = null) { LastFilter = filter; return Task.CompletedTask; }
    }
    private sealed class Dialogs : IUserDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) => Task.FromResult(true);
    }
    private static async Task WithDatabase(Func<SqliteExpenseRepository, Task> test)
    {
        var path = Path.Combine(Path.GetTempPath(), $"vm-{Guid.NewGuid():N}.db3");
        try
        {
            await using var repo = new SqliteExpenseRepository(path);
            await repo.SaveAccountsAsync([new Account(Guid.NewGuid(), "Principale", 100m, isPrimary: true)]);
            await test(repo);
        }
        finally { File.Delete(path); }
    }
    private static TransactionEditorViewModel Editor(SqliteExpenseRepository repo, IAppPreferences prefs, Navigation? nav = null) =>
        new(new(repo), new(repo), new(repo), nav ?? new(), new Dialogs(), prefs);
    private static async Task SettledAsync(ViewModelBase model)
    {
        for (var i = 0; i < 200; i++) { if (!model.IsBusy) return; await Task.Delay(10); }
        Assert.False(model.IsBusy);
    }
    [Fact] public Task Draft_SurvivesTabReturnAndNewViewModel() => WithDatabase(async repo =>
    {
        var prefs = new PreferencesStore(); var editor = Editor(repo, prefs);
        await editor.LoadAsync(); editor.Amount = "6,50"; editor.Description = "Pranzo"; editor.SelectedCategory = editor.Categories[0];
        var categoryId = editor.SelectedCategory.Id;
        await editor.LoadAsync(); Assert.Equal("6,50", editor.Amount); Assert.Equal(categoryId, editor.SelectedCategory?.Id);
        var restored = Editor(repo, prefs); await restored.LoadAsync();
        Assert.Equal("6,50", restored.Amount); Assert.Equal("Pranzo", restored.Description); Assert.Equal(categoryId, restored.SelectedCategory?.Id);
    });
    [Fact] public Task Editor_RefreshesNewAccountsWithoutDiscardingDraft() => WithDatabase(async repo =>
    {
        var editor = Editor(repo, new PreferencesStore()); await editor.LoadAsync(); editor.Amount = "5";
        var extra = new Account(Guid.NewGuid(), "Nuovo", 0m); await repo.SaveAccountsAsync([extra]);
        await editor.LoadAsync(); Assert.Equal("5", editor.Amount); Assert.Contains(editor.Accounts, a => a.Account.Id == extra.Id);
    });
    [Fact] public Task SaveAndAdd_ClearsDraftAndDoesNotDoubleSave() => WithDatabase(async repo =>
    {
        var prefs = new PreferencesStore(); var editor = Editor(repo, prefs);
        await editor.LoadAsync(); editor.SelectedCategory = editor.Categories[0]; editor.Amount = "6,50";
        await Task.WhenAll(editor.SaveAndAddCommand.ExecuteAsync(null), editor.SaveCommand.ExecuteAsync(null));
        Assert.Single(await repo.GetTransactionsAsync()); Assert.Equal("", editor.Amount); Assert.Null(editor.EditingId);
        Assert.Equal("Movimento salvato.", editor.StatusMessage);
        var restored = Editor(repo, prefs); await restored.LoadAsync(); Assert.Equal("", restored.Amount);
    });
    [Fact] public Task Duplicate_CreatesNewIdWithoutChangingOriginal() => WithDatabase(async repo =>
    {
        var account = Assert.Single(await repo.GetAccountsAsync()); var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
        var original = new Transaction(Guid.NewGuid(), DateTime.UtcNow.AddDays(-1), TransactionType.Expense, account.Id, category.Id, 6.50m, description: "Pranzo");
        await repo.SaveTransactionAsync(original);
        var editor = Editor(repo, new PreferencesStore()); await editor.LoadAsync(original.Id, true);
        Assert.Null(editor.EditingId); Assert.Equal(DateTime.Today, editor.Date); Assert.Equal("Pranzo", editor.Description);
        await editor.SaveCommand.ExecuteAsync(null);
        Assert.Equal(2, (await repo.GetTransactionsAsync()).Count); Assert.Equal(original.OccurredAtUtc, (await repo.GetTransactionAsync(original.Id))!.OccurredAtUtc);
    });
    [Fact] public Task Statistics_PeriodAndCurrencyUpdateImmediately() => WithDatabase(async repo =>
    {
        var account = Assert.Single(await repo.GetAccountsAsync()); var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
        var year = DateTime.Today.Year;
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), new DateTime(year, 1, 15, 12, 0, 0).ToUniversalTime(), TransactionType.Expense, account.Id, category.Id, 10m));
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), new DateTime(year, 2, 15, 12, 0, 0).ToUniversalTime(), TransactionType.Expense, account.Id, category.Id, 20m));
        var vm = new StatisticsViewModel(new(repo), new(repo), new Navigation()); await vm.LoadAsync();
        vm.SelectedPeriod = vm.Periods.Single(p => p.Value == DateRangePreset.CurrentYear); await SettledAsync(vm); Assert.Equal(30m, vm.Expense);
        vm.SelectedPeriod = vm.Periods.Single(p => p.Value == DateRangePreset.SelectedMonth); vm.ReferenceDate = new(year, 1, 1); await SettledAsync(vm); Assert.Equal(10m, vm.Expense);
        vm.SelectedCurrency = "USD"; await SettledAsync(vm); Assert.Equal(0m, vm.Expense); Assert.False(vm.HasChartData);
    });
    [Fact] public Task Transactions_AutomaticFiltersAndDetailRefresh() => WithDatabase(async repo =>
    {
        var account = Assert.Single(await repo.GetAccountsAsync()); var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
        var transaction = new Transaction(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, account.Id, category.Id, 10m, description: "Pane"); await repo.SaveTransactionAsync(transaction);
        var vm = new TransactionsViewModel(new(repo), new(repo), new(repo), new Dialogs(), new Navigation(), new NavigationRequests());
        await vm.LoadAsync(); vm.ShowDetailCommand.Execute(Assert.Single(vm.Transactions));
        transaction.Update(transaction.OccurredAtUtc, transaction.Type, account.Id, category.Id, 10m, "EUR", "Pane", "Nota nuova"); await repo.SaveTransactionAsync(transaction);
        await vm.LoadAsync(); Assert.Equal("Nota nuova", vm.SelectedTransaction?.Transaction.Notes);
        vm.SelectedType = vm.Types.Single(t => t.Value == TransactionType.Income); await SettledAsync(vm);
        Assert.Empty(vm.Transactions); Assert.Null(vm.SelectedTransaction);
    });
}
