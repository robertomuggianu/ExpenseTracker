using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Presentation.Services;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Application.Tests;

public sealed class ViewModelWorkflowTests
{
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
