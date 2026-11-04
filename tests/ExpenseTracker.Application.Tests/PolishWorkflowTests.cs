using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Presentation.Services;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Application.Tests;

public sealed class PolishWorkflowTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public Task Planning_ChangingMonthDiscardsBudgetEditInsteadOfCopyingIt(int direction) => WithDatabase(async repo =>
    {
        var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
        var planning = new PlanningService(repo);
        var month = new DateTime(2026, 11, 1);
        await planning.SaveBudgetAsync(category.Id, "EUR", month, 150m);
        var vm = Planning(repo, planning);
        vm.Month = month;
        await vm.LoadAsync();
        vm.EditBudgetCommand.Execute(Assert.Single(vm.Budgets));
        Assert.Equal("150,00", vm.BudgetAmount);

        await (direction < 0 ? vm.PreviousMonthCommand : vm.NextMonthCommand).ExecuteAsync(null);

        Assert.Equal(month.AddMonths(direction), vm.Month);
        Assert.Null(vm.BudgetCategory);
        Assert.Empty(vm.BudgetAmount);
        Assert.Empty(vm.Budgets);
        await vm.SaveBudgetCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ErrorMessage);
        var saved = Assert.Single((await planning.GetAsync()).Budgets);
        Assert.Equal(month, saved.Month);
        Assert.Equal(150m, saved.Limit);
    });

    [Fact]
    public Task Planning_NewRuleAfterEditUsesFreshDefaultsAndCreatesSeparateRule() => WithDatabase(async repo =>
    {
        var primary = Assert.Single(await repo.GetAccountsAsync());
        var extra = new Account(Guid.NewGuid(), "Secondario", 0m);
        await repo.SaveAccountsAsync([extra]);
        var income = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Income);
        var planning = new PlanningService(repo);
        var original = new RecurringRule(Guid.NewGuid(), "Stipendio", extra.Id, income.Id,
            TransactionType.Income, 1200m, "EUR", DateTime.Today.AddYears(-1), RepeatFrequency.Yearly);
        await planning.SaveRuleAsync(original);
        var vm = Planning(repo, planning);
        await vm.LoadAsync();
        vm.EditRuleCommand.Execute(Assert.Single(vm.Rules));
        vm.RuleAmount = "";
        await vm.SaveRuleCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ErrorMessage);

        vm.NewRuleCommand.Execute(null);

        Assert.True(vm.IsRuleEditorVisible);
        Assert.True(vm.CanChangeSchedule);
        Assert.Null(vm.ErrorMessage);
        Assert.Empty(vm.RuleName);
        Assert.Empty(vm.RuleAmount);
        Assert.Equal(TransactionType.Expense, vm.RuleType.Value);
        Assert.Equal(primary.Id, vm.RuleAccount?.Account.Id);
        Assert.Null(vm.RuleCategory);
        Assert.Equal(DateTime.Today, vm.StartDate);
        Assert.Equal(RepeatFrequency.Monthly, vm.Frequency?.Value);
        vm.RuleName = "Abbonamento";
        vm.RuleAmount = "12,50";
        vm.RuleCategory = vm.Categories.First();
        await vm.SaveRuleCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsRuleEditorVisible);
        Assert.True(vm.CanChangeSchedule);
        Assert.Null(vm.RuleCategory);
        Assert.Empty(vm.RuleAmount);
        Assert.NotNull(vm.StatusMessage);
        var rules = (await planning.GetAsync()).Rules;
        Assert.Equal(2, rules.Count);
        Assert.Equal(original, rules.Single(r => r.Id == original.Id));
        Assert.Equal(12.50m, rules.Single(r => r.Id != original.Id).Amount);
    });

    [Fact]
    public Task Planning_CancelRuleDiscardsChangesAndClearsValidation() => WithDatabase(async repo =>
    {
        var account = Assert.Single(await repo.GetAccountsAsync());
        var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
        var planning = new PlanningService(repo);
        var original = new RecurringRule(Guid.NewGuid(), "Affitto", account.Id, category.Id,
            TransactionType.Expense, 600m, "EUR", DateTime.Today.AddMonths(-2), RepeatFrequency.Monthly);
        await planning.SaveRuleAsync(original);
        var vm = Planning(repo, planning);
        await vm.LoadAsync();
        vm.EditRuleCommand.Execute(Assert.Single(vm.Rules));
        vm.RuleName = "Modifica da scartare";
        vm.RuleAmount = "";
        await vm.SaveRuleCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ErrorMessage);

        vm.CancelRuleCommand.Execute(null);

        Assert.False(vm.IsRuleEditorVisible);
        Assert.True(vm.CanChangeSchedule);
        Assert.Null(vm.ErrorMessage);
        Assert.Empty(vm.RuleName);
        Assert.Empty(vm.RuleAmount);
        Assert.Null(vm.RuleCategory);
        Assert.Equal(original, Assert.Single((await planning.GetAsync()).Rules));
        vm.EditRuleCommand.Execute(Assert.Single(vm.Rules));
        Assert.False(vm.CanChangeSchedule);
        Assert.Equal("Affitto", vm.RuleName);
        Assert.Equal("600,00", vm.RuleAmount);
    });

    [Fact]
    public Task Categories_CancelDiscardsIdentityAppearanceAndValidation() => WithDatabase(async repo =>
    {
        var service = new CategoryService(repo);
        var category = await service.SaveCategoryAsync(new(null, "Viaggi", TransactionType.Expense, "#123456", false, "luggage"));
        var vm = new CategoriesViewModel(service);
        await vm.LoadAsync();
        vm.EditCategoryCommand.Execute(vm.Categories.Single(c => c.Id == category.Id));
        vm.CategoryName = "";
        await vm.SaveCategoryCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ErrorMessage);

        vm.CancelEditCommand.Execute(null);

        Assert.False(vm.IsEditorVisible);
        Assert.Null(vm.EditingCategoryId);
        Assert.Null(vm.ErrorMessage);
        Assert.Empty(vm.CategoryName);
        Assert.True(vm.CategoryIsActive);
        Assert.Equal(CategoryAppearance.InferIcon(""), vm.SelectedIcon.Key);
        var saved = await repo.GetCategoryAsync(category.Id);
        Assert.Equal("Viaggi", saved!.Name);
        Assert.Equal("luggage", saved.Icon);
        Assert.False(saved.IsActive);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Categories_StartingAnotherEditorClearsPreviousValidation(bool editExisting) => WithDatabase(async repo =>
    {
        var vm = new CategoriesViewModel(new(repo));
        await vm.LoadAsync();
        vm.NewCategoryCommand.Execute(null);
        await vm.SaveCategoryCommand.ExecuteAsync(null);
        Assert.NotNull(vm.ErrorMessage);

        if (editExisting)
            vm.EditCategoryCommand.Execute(vm.Categories.First(c => c.Type == TransactionType.Expense));
        else
            vm.NewCategoryCommand.Execute(null);

        Assert.True(vm.IsEditorVisible);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(editExisting, vm.EditingCategoryId.HasValue);
    });

    private static PlanningViewModel Planning(SqliteExpenseRepository repo, PlanningService service) =>
        new(service, new(repo), new(repo), new(repo), new Dialogs());

    private sealed class Dialogs : IUserDialogService
    {
        public Task ShowMessageAsync(string title, string message) => Task.CompletedTask;
        public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) => Task.FromResult(true);
    }

    private static async Task WithDatabase(Func<SqliteExpenseRepository, Task> test)
    {
        var path = Path.Combine(Path.GetTempPath(), $"polish-{Guid.NewGuid():N}.db3");
        try
        {
            await using var repo = new SqliteExpenseRepository(path);
            await repo.SaveAccountsAsync([new Account(Guid.NewGuid(), "Principale", 100m, isPrimary: true)]);
            await test(repo);
        }
        finally { File.Delete(path); }
    }
}
