using ExpenseTracker.Application;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Application.Tests;

public sealed class DomainAndAggregationTests
{
    private static readonly Guid AccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CategoryId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void AccountBalance_UsesOpeningBalanceIncomeAndExpenses()
    {
        var account = new Account(AccountId, "Principale", 100m);
        var transactions = new[]
        {
            CreateTransaction(TransactionType.Income, 50m, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(TransactionType.Expense, 20m, new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc))
        };

        Assert.Equal(130m, account.CalculateCurrentBalance(transactions));
    }

    [Fact]
    public void PeriodTotals_IncludeOnlyTransactionsInsideRange()
    {
        var range = new DateRange(
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        var transactions = new[]
        {
            CreateTransaction(TransactionType.Income, 1000m, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(TransactionType.Expense, 120m, new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc)),
            CreateTransaction(TransactionType.Expense, 999m, new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc))
        };

        var result = FinancialAggregator.CalculateTotals(transactions, range);

        Assert.Equal(1000m, result.Income);
        Assert.Equal(120m, result.Expense);
        Assert.Equal(880m, result.Difference);
    }

    [Fact]
    public void ExpenseGrouping_AggregatesByCategory()
    {
        var rentId = Guid.NewGuid();
        var groceriesId = Guid.NewGuid();
        var range = new DateRange(DateTime.UnixEpoch, DateTime.MaxValue);
        var categories = new[]
        {
            new Category(rentId, "Affitto", TransactionType.Expense, "#111111"),
            new Category(groceriesId, "Spesa", TransactionType.Expense, "#222222")
        };
        var transactions = new[]
        {
            CreateTransaction(TransactionType.Expense, 500m, DateTime.UtcNow, rentId),
            CreateTransaction(TransactionType.Expense, 100m, DateTime.UtcNow, groceriesId),
            CreateTransaction(TransactionType.Expense, 50m, DateTime.UtcNow, groceriesId)
        };

        var result = FinancialAggregator.GroupExpensesByCategory(transactions, categories, range);

        Assert.Collection(
            result,
            item => { Assert.Equal("Affitto", item.Name); Assert.Equal(500m, item.Total); },
            item => { Assert.Equal("Spesa", item.Name); Assert.Equal(150m, item.Total); });
    }

    [Fact]
    public void IncomeGrouping_ExcludesExpensesAndOutsidePeriodAndPreservesAppearance()
    {
        var salaryId = Guid.NewGuid();
        var extraId = Guid.NewGuid();
        var from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var range = new DateRange(from, from.AddMonths(1));
        var categories = new[]
        {
            new Category(salaryId, "Stipendio", TransactionType.Income, "#80D9BE", icon: "work"),
            new Category(extraId, "Extra", TransactionType.Income, "#F4C66A", icon: "gift")
        };
        var transactions = new[]
        {
            CreateTransaction(TransactionType.Income, 1000m, from, salaryId),
            CreateTransaction(TransactionType.Income, 50m, from.AddDays(1), extraId),
            CreateTransaction(TransactionType.Income, 25m, from.AddDays(2), extraId),
            CreateTransaction(TransactionType.Expense, 999m, from, salaryId),
            CreateTransaction(TransactionType.Income, 999m, range.ToUtcExclusive, extraId)
        };

        var result = FinancialAggregator.GroupByCategory(transactions, categories, range, TransactionType.Income);

        Assert.Collection(result,
            item => { Assert.Equal(1000m, item.Total); Assert.Equal("work", item.Icon); Assert.Equal("#80D9BE", item.Color); },
            item => { Assert.Equal(75m, item.Total); Assert.Equal("Extra", item.Name); });
    }

    [Fact]
    public void CategoryCompatibility_RejectsDifferentTransactionType()
    {
        var category = new Category(Guid.NewGuid(), "Stipendio", TransactionType.Income, "#00AA00");

        var error = Assert.Throws<DomainValidationException>(() =>
            TransactionRules.EnsureCategoryCompatible(TransactionType.Expense, category));

        Assert.Contains("compatibile", error.Message);
    }

    [Fact]
    public void SelectingPrimary_LeavesExactlyOnePrimaryAccount()
    {
        var first = new Account(Guid.NewGuid(), "Uno", 0m, isPrimary: true);
        var second = new Account(Guid.NewGuid(), "Due", 0m);
        var accounts = new[] { first, second };

        AccountRules.SelectPrimary(accounts, second.Id);

        Assert.False(first.IsPrimary);
        Assert.True(second.IsPrimary);
        Assert.Single(accounts, item => item.IsPrimary);
    }

    private static Transaction CreateTransaction(
        TransactionType type,
        decimal amount,
        DateTime date,
        Guid? categoryId = null) =>
        new(Guid.NewGuid(), date, type, AccountId, categoryId ?? CategoryId, amount);
}
