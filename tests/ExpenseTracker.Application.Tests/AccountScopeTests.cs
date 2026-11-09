using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Presentation.Services;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Application.Tests;

public sealed class AccountScopeTests
{
    [Fact]
    public void ExistingBudgetJsonKeepsTheAllAccountsScope()
    {
        var id = Guid.NewGuid(); var category = Guid.NewGuid();
        var legacy = $$"""{"Rules":[],"Budgets":[{"Id":"{{id}}","CategoryId":"{{category}}","Currency":"EUR","Month":"2026-11-01T00:00:00","Limit":100}]}""";
        var budget = Assert.Single(PlanningState.Deserialize(legacy).Budgets);
        Assert.Null(budget.AccountId); Assert.Equal(100m, budget.Limit);
        var state = new PlanningState([], [budget with { AccountId = Guid.NewGuid() }]);
        Assert.Equal(state.Budgets[0], Assert.Single(PlanningState.Deserialize(state.Serialize()).Budgets));
    }

    [Fact]
    public Task AccountScopeIsPersistedAndMissingAccountFallsBackToAll() => Database(async (repo, first, second, category) =>
    {
        var preferences = new Preferences(); var scope = new AccountSelection(preferences);
        var summaries = await new AccountService(repo).GetAccountsAsync(); scope.UpdateAccounts(summaries); scope.Select(second.Id);
        var restored = new AccountSelection(preferences); restored.UpdateAccounts(summaries);
        Assert.Equal(second.Id, restored.AccountId); Assert.Equal(second.Name, restored.Name);
        restored.UpdateAccounts(summaries.Where(a => a.Account.Id != second.Id).ToArray());
        Assert.Null(restored.AccountId); Assert.Equal("Tutti i conti", restored.Name);
        Assert.Null(new AccountSelection(preferences).AccountId);
    });

    [Fact]
    public Task DashboardBalancesReportsAndDrillDownUseOnlyTheSelectedAccount() => Database(async (repo, first, second, category) =>
    {
        var preferences = new Preferences(); var scope = new AccountSelection(preferences); var nav = new Navigation();
        var home = new DashboardViewModel(new(repo), nav, new(repo), preferences, scope);
        var movements = new TransactionsViewModel(new(repo), new(repo), new(repo), new Dialogs(), nav, new(), scope);
        var stats = new StatisticsViewModel(new(repo), new(repo), nav, scope);
        await home.LoadAsync(); await movements.LoadAsync(); await stats.LoadAsync();
        Assert.Equal(40m, home.Expense);
        scope.Select(second.Id);
        await Settled(home, movements, stats);
        Assert.Equal(30m, home.Expense); Assert.Equal(170m, home.TotalBalance); Assert.Equal(30m, stats.Expense);
        Assert.All(movements.Transactions, t => Assert.Equal(second.Id, t.Transaction.AccountId));
        await movements.ResetFiltersCommand.ExecuteAsync(null);
        Assert.Equal(second.Id, scope.AccountId);
        Assert.All(movements.Transactions, t => Assert.Equal(second.Id, t.Transaction.AccountId));
        await home.OpenCategoryCommand.ExecuteAsync(Assert.Single(home.CategoryBreakdown));
        Assert.Equal(second.Id, nav.LastFilter?.AccountId);
        scope.Select(first.Id); scope.Select(second.Id); scope.Select(first.Id);
        await Settled(home, movements, stats);
        Assert.Equal(10m, home.Expense); Assert.Equal(10m, stats.Expense);
        Assert.Equal(first.Id, Assert.Single(movements.Transactions).Transaction.AccountId);
        scope.Select(null); await Settled(home, movements, stats);
        Assert.Equal(40m, home.Expense); Assert.Equal(40m, stats.Expense); Assert.Equal(2, movements.Transactions.Count);
    });

    [Fact]
    public Task CurrencyFollowsSelectedAccountWithoutMixingBalances() => Database(async (repo, first, second, category) =>
    {
        var usd = new Account(Guid.NewGuid(), "Dollari", 500m, "USD");
        await repo.SaveAccountsAsync([first, second, usd]);
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.Today.AddHours(12).ToUniversalTime(), TransactionType.Expense, usd.Id, category.Id, 8m, "USD"));
        var prefs = new Preferences(); var scope = new AccountSelection(prefs); var nav = new Navigation();
        var home = new DashboardViewModel(new(repo), nav, new(repo), prefs, scope);
        var stats = new StatisticsViewModel(new(repo), new(repo), nav, scope);
        await home.LoadAsync(); await stats.LoadAsync(); scope.Select(usd.Id); await Settled(home, stats);
        Assert.Equal("USD", home.ReportCurrency); Assert.Equal("USD", stats.ReportCurrency);
        Assert.Equal(8m, home.Expense); Assert.Equal(8m, stats.Expense); Assert.Equal(492m, home.TotalBalance);
        scope.Select(null); await Settled(home, stats);
        Assert.Equal("EUR", stats.ReportCurrency); Assert.Equal(40m, stats.Expense); Assert.Equal(260m, home.TotalBalance);
    });

    [Fact]
    public Task ReferenceDateChangesRefreshMonthWithoutAnApplyButton() => Database(async (repo, first, second, category) =>
    {
        var stats = new StatisticsViewModel(new(repo), new(repo), new Navigation());
        var tx = new TransactionsViewModel(new(repo), new(repo), new(repo), new Dialogs(), new Navigation(), new());
        await stats.LoadAsync(); await tx.LoadAsync();
        stats.ReferenceDate = DateTime.Today.AddMonths(-3); tx.ReferenceDate = stats.ReferenceDate;
        await Settled(stats, tx); Assert.Equal(0m, stats.Expense); Assert.Empty(tx.Transactions);
        stats.ReferenceDate = DateTime.Today; tx.ReferenceDate = DateTime.Today;
        await Settled(stats, tx); Assert.Equal(40m, stats.Expense); Assert.Equal(2, tx.Transactions.Count);
    });

    [Fact]
    public Task NewMovementDefaultsToHomeAccountButCanChooseAnotherWithoutChangingScope() => Database(async (repo, first, second, category) =>
    {
        var prefs = new Preferences(); var scope = new AccountSelection(prefs);
        scope.UpdateAccounts(await new AccountService(repo).GetAccountsAsync()); scope.Select(second.Id);
        var editor = new TransactionEditorViewModel(new(repo), new(repo), new(repo), new Navigation(), new Dialogs(), prefs, scope);
        await editor.LoadAsync(); Assert.Equal(second.Id, editor.SelectedAccount?.Account.Id);
        editor.SelectedAccount = editor.Accounts.Single(a => a.Account.Id == first.Id);
        editor.SelectedCategory = editor.Categories.Single(c => c.Id == category.Id); editor.Amount = "7";
        await editor.SaveCommand.ExecuteAsync(null); Assert.Null(editor.ErrorMessage);
        Assert.Equal(second.Id, scope.AccountId);
        Assert.Equal(first.Id, (await repo.GetTransactionsAsync()).Single(t => t.Amount == 7m).AccountId);
        var existing = (await repo.GetTransactionsAsync()).Single(t => t.Amount == 10m);
        await editor.LoadAsync(existing.Id); Assert.Equal(first.Id, editor.SelectedAccount?.Account.Id);
    });

    [Fact]
    public Task ExportAllUsesSelectedAccountAndAllAccountsStillProvidesFullBackup() => Database(async (repo, first, second, category) =>
    {
        var scope = new AccountSelection(); var csv = new CaptureCsv();
        var export = new ExportViewModel(csv, new Share(), new(repo, new TransactionImportParser()), new Picker(), new(repo), new(repo), new Dialogs(), scope);
        await export.LoadAsync(); scope.Select(second.Id);
        await export.ExportCommand.ExecuteAsync(null);
        Assert.Equal(second.Id, csv.Filter?.AccountId); Assert.Null(csv.Filter?.FromUtc);
        scope.Select(null); await export.ExportCommand.ExecuteAsync(null); Assert.Null(csv.Filter);
    });

    [Fact]
    public Task AssistantFactsAndReportsNeverIncludeOtherAccounts() => Database(async (repo, first, second, category) =>
    {
        var report = await new AssistantReportService(repo).CreateAsync(AssistantReportKind.MonthlySummary, "EUR", DateTime.Today, accountId: second.Id);
        Assert.Equal(30m, report.Current.Expense); Assert.Equal(1, report.Current.MovementCount);
        var chat = await new AssistantChatService(repo).ExecuteAsync("How much did I spend this month?",
            new(AssistantChatPeriod.Current, AssistantChatOperation.Total, AssistantChatMeasure.Expenses), "EUR", DateTime.Today, accountId: second.Id);
        Assert.Contains("selected account", chat.Facts); Assert.Contains("30,00", chat.CalculatedAnswer);
        Assert.DoesNotContain(first.Name, chat.Facts); Assert.DoesNotContain(second.Name, chat.Facts);
    });

    [Fact]
    public Task ScopedBudgetsAreSeparateAndRecurringListFollowsHome() => Database(async (repo, first, second, category) =>
    {
        var service = new PlanningService(repo);
        await service.SaveBudgetAsync(category.Id, "EUR", DateTime.Today, 100m);
        await service.SaveBudgetAsync(category.Id, "EUR", DateTime.Today, 50m, second.Id);
        Assert.Equal(40m, Assert.Single(await service.GetBudgetsAsync(DateTime.Today)).Spent);
        Assert.Equal(30m, Assert.Single(await service.GetBudgetsAsync(DateTime.Today, second.Id)).Spent);
        Assert.Equal(2, (await service.GetAsync()).Budgets.Count);
        await service.SaveRuleAsync(new(Guid.NewGuid(), "Primo", first.Id, category.Id, TransactionType.Expense, 5m, "EUR", DateTime.Today, RepeatFrequency.Monthly));
        await service.SaveRuleAsync(new(Guid.NewGuid(), "Secondo", second.Id, category.Id, TransactionType.Expense, 9m, "EUR", DateTime.Today, RepeatFrequency.Monthly));
        var scope = new AccountSelection();
        var planning = new PlanningViewModel(service, new(repo), new(repo), new(repo), new Dialogs(), scope);
        await planning.LoadAsync(); scope.Select(second.Id); await Settled(planning);
        Assert.Equal(second.Id, Assert.Single(planning.Rules).Rule.AccountId);
        Assert.Equal(50m, Assert.Single(planning.Budgets).Item.Budget.Limit);
        planning.NewRuleCommand.Execute(null); Assert.Equal(second.Id, planning.RuleAccount?.Account.Id);
        Assert.Equal(second.Id, planning.SourceAccount?.Account.Id);
    });

    private static async Task Database(Func<SqliteExpenseRepository, Account, Account, Category, Task> action)
    {
        var path = Path.Combine(Path.GetTempPath(), $"scope-{Guid.NewGuid():N}.db3");
        try
        {
            await using var repo = new SqliteExpenseRepository(path);
            var first = new Account(Guid.NewGuid(), "Principale", 100m, isPrimary: true);
            var second = new Account(Guid.NewGuid(), "Viaggi", 200m);
            await repo.SaveAccountsAsync([first, second]);
            var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
            await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.Today.AddHours(12).ToUniversalTime(), TransactionType.Expense, first.Id, category.Id, 10m));
            await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.Today.AddHours(12).ToUniversalTime(), TransactionType.Expense, second.Id, category.Id, 30m));
            await action(repo, first, second, category);
        }
        finally { File.Delete(path); }
    }

    private static async Task Settled(params ViewModelBase[] models)
    {
        for (var i = 0; i < 300 && models.Any(m => m.IsBusy); i++) await Task.Delay(10);
        Assert.All(models, m => { Assert.False(m.IsBusy); Assert.Null(m.ErrorMessage); });
    }
    private sealed class Preferences : IAppPreferences
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
        public Task GoToTransactionEditorAsync(Guid? transactionId = null, bool duplicate = false, bool returnToHome = false, TransactionType? initialType = null) => Task.CompletedTask;
        public Task GoToDashboardAsync() => Task.CompletedTask;
        public Task GoToTransactionsAsync(TransactionFilter? filter = null, string? message = null) { LastFilter = filter; return Task.CompletedTask; }
    }
    private sealed class Dialogs : IUserDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) => Task.FromResult(true);
        public Task<Category?> ChooseCategoryAsync(string title, IReadOnlyList<Category> categories) => Task.FromResult(categories.FirstOrDefault());
    }
    private sealed class CaptureCsv : ICsvExportService
    {
        public TransactionFilter? Filter { get; private set; }
        public Task<CsvExportResult> ExportAsync(TransactionFilter? filter = null, CancellationToken cancellationToken = default) { Filter = filter; return Task.FromResult(new CsvExportResult("test.csv", "")); }
    }
    private sealed class Share : IFileShareService { public Task ShareAsync(CsvExportResult export, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class Picker : IImportFilePickerService { public Task<SelectedImportFile?> PickAsync() => Task.FromResult<SelectedImportFile?>(null); }
}
