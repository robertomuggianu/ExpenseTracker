using System.Text;
using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;

namespace ExpenseTracker.Application.Tests;

public sealed class PlanningAndIntegrityTests
{
    [Fact] public Task Transfer_RejectsFractionalCentsWithoutWriting() => WithDatabase(async repo =>
    {
        var (account, _) = await SetupAsync(repo);
        var destination = new Account(Guid.NewGuid(), "Destinazione", 0m);
        await repo.SaveAccountsAsync([destination]);
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            new TransferService(repo).SaveAsync(account.Id, destination.Id, 1.234m, DateTime.Today, null));
        Assert.Empty(await repo.GetTransactionsAsync());
    });

    [Fact] public Task Recurrence_StaleEditPreservesAdvancedScheduleAndPause() => WithDatabase(async repo =>
    {
        var (account, category) = await SetupAsync(repo); var service = new PlanningService(repo);
        var original = new RecurringRule(Guid.NewGuid(), "Rata", account.Id, category.Id, TransactionType.Expense,
            10m, "EUR", new(2026, 1, 31), RepeatFrequency.Monthly);
        await service.SaveRuleAsync(original);
        await service.ConfirmAsync(original.Id, original.NextDate, new(2026, 1, 31));
        await service.SetRuleActiveAsync(original.Id, false);
        await service.SaveRuleAsync(original with { Amount = 15m });
        var saved = Assert.Single((await service.GetAsync()).Rules);
        Assert.Equal(15m, saved.Amount); Assert.Equal(1, saved.NextIndex); Assert.False(saved.IsActive);
        Assert.Equal(new DateTime(2026, 2, 28), saved.NextDate);
        await service.SetRuleActiveAsync(original.Id, true);
        await Assert.ThrowsAsync<DomainValidationException>(() => service.ConfirmAsync(original.Id, original.NextDate, new(2026, 2, 28)));
        Assert.Single(await repo.GetTransactionsAsync());
    });

    [Fact] public Task Recurrence_ExistingCalendarCannotBeReplaced() => WithDatabase(async repo =>
    {
        var (account, category) = await SetupAsync(repo); var service = new PlanningService(repo);
        var rule = new RecurringRule(Guid.NewGuid(), "Rata", account.Id, category.Id, TransactionType.Expense,
            10m, "EUR", new(2026, 1, 31), RepeatFrequency.Monthly);
        await service.SaveRuleAsync(rule);
        await Assert.ThrowsAsync<DomainValidationException>(() => service.SaveRuleAsync(rule with { StartDate = rule.StartDate.AddDays(1) }));
        await Assert.ThrowsAsync<DomainValidationException>(() => service.SaveRuleAsync(rule with { Frequency = RepeatFrequency.Weekly }));
        Assert.Equal(rule, Assert.Single((await service.GetAsync()).Rules));
    });

    private static async Task<(Account Account, Category Category)> SetupAsync(SqliteExpenseRepository repo)
    {
        var account = new Account(Guid.NewGuid(), "Conto", 1000m, isPrimary: true);
        await repo.SaveAccountsAsync([account]);
        return (account, (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense));
    }
    private static async Task WithDatabase(Func<SqliteExpenseRepository, Task> test)
    {
        var path = Path.Combine(Path.GetTempPath(), $"integrity-{Guid.NewGuid():N}.db3");
        try { await using var repo = new SqliteExpenseRepository(path); await test(repo); }
        finally { File.Delete(path); }
    }
    [Fact] public Task Recurrence_ConfirmationIsAtomicAndCannotDoublePost() => WithDatabase(async repo =>
    {
        var (account, category) = await SetupAsync(repo); var service = new PlanningService(repo);
        var rule = new RecurringRule(Guid.NewGuid(), "Abbonamento", account.Id, category.Id, TransactionType.Expense, 12.50m, "EUR", new(2026, 1, 31), RepeatFrequency.Monthly);
        await service.SaveRuleAsync(rule);
        Assert.Empty(await repo.GetTransactionsAsync());
        await service.ConfirmAsync(rule.Id, rule.NextDate, new(2026, 2, 28));
        await Assert.ThrowsAsync<DomainValidationException>(() => service.ConfirmAsync(rule.Id, rule.NextDate, new(2026, 2, 28)));
        Assert.Single(await repo.GetTransactionsAsync());
        var next = Assert.Single((await service.GetAsync()).Rules);
        Assert.Equal(new DateTime(2026, 2, 28), next.NextDate);
        await service.ConfirmAsync(rule.Id, next.NextDate, new(2026, 2, 28));
        Assert.Equal(new DateTime(2026, 3, 31), Assert.Single((await service.GetAsync()).Rules).NextDate);
    });
    [Fact] public Task Recurrence_PauseSkipAndFutureDoNotAffectBalance() => WithDatabase(async repo =>
    {
        var (account, category) = await SetupAsync(repo); var service = new PlanningService(repo);
        var rule = new RecurringRule(Guid.NewGuid(), "Rata", account.Id, category.Id, TransactionType.Expense, 10m, "EUR", new(2026, 1, 1), RepeatFrequency.Weekly);
        await service.SaveRuleAsync(rule);
        await Assert.ThrowsAsync<DomainValidationException>(() => service.ConfirmAsync(rule.Id, rule.NextDate, new(2025, 12, 31)));
        await service.SetRuleActiveAsync(rule.Id, false);
        await Assert.ThrowsAsync<DomainValidationException>(() => service.ConfirmAsync(rule.Id, rule.NextDate, new(2026, 1, 1)));
        await service.SkipAsync(rule.Id, rule.NextDate);
        Assert.Equal(new DateTime(2026, 1, 8), Assert.Single((await service.GetAsync()).Rules).NextDate);
        Assert.Empty(await repo.GetTransactionsAsync());
    });
    [Fact] public Task Transfer_ChangesBothBalancesButNotIncomeOrExpenses_AndDeletesTogether() => WithDatabase(async repo =>
    {
        var (account, _) = await SetupAsync(repo); var destination = new Account(Guid.NewGuid(), "Contanti", 0);
        await repo.SaveAccountsAsync([destination]);
        await new TransferService(repo).SaveAsync(account.Id, destination.Id, 50m, new(2026, 10, 9), null);
        var transactions = await repo.GetTransactionsAsync(); Assert.Equal(2, transactions.Count);
        Assert.Equal(950, account.CalculateCurrentBalance(transactions)); Assert.Equal(50, destination.CalculateCurrentBalance(transactions));
        var totals = FinancialAggregator.CalculateTotals(transactions, MonthlyPeriods.FullMonth(new(2026, 10, 1)));
        Assert.Equal(0, totals.Income); Assert.Equal(0, totals.Expense);
        await repo.DeleteTransactionAsync(transactions[0].Id); Assert.Empty(await repo.GetTransactionsAsync());
    });
    [Fact] public Task Budget_IsMonthlyAndCurrencySpecific_AndExcludesTransfers() => WithDatabase(async repo =>
    {
        var (account, category) = await SetupAsync(repo); var service = new PlanningService(repo);
        var month = new DateTime(2026, 10, 1); var utc = DateTime.SpecifyKind(month.AddDays(8), DateTimeKind.Local).ToUniversalTime();
        await service.SaveBudgetAsync(category.Id, "EUR", month, 100m);
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), utc, TransactionType.Expense, account.Id, category.Id, 120m));
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), utc.AddMonths(-1), TransactionType.Expense, account.Id, category.Id, 300m));
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), utc, TransactionType.Expense, account.Id, category.Id, 400m, transferId: Guid.NewGuid()));
        var progress = Assert.Single(await service.GetBudgetsAsync(month));
        Assert.Equal(120, progress.Spent); Assert.Equal(-20, progress.Remaining); Assert.Equal(1, progress.Progress);
        Assert.Empty(await service.GetBudgetsAsync(month.AddMonths(1)));
        await service.SaveBudgetAsync(category.Id, "EUR", month, 150m);
        Assert.Equal(150, Assert.Single(await service.GetBudgetsAsync(month)).Budget.Limit);
    });
    [Fact] public Task InvalidImport_DoesNotLeavePartialWrites() => WithDatabase(async repo =>
    {
        await SetupAsync(repo); var count = (await repo.GetAccountsAsync()).Count;
        const string csv = "Data;Tipo;Conto;Categoria;Importo;Valuta\n09/10/2026;Uscita;Nuovo;Nuova;6,50;EUR\n09/10/2026;Uscita;Nuovo;Nuova;0;EUR";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        await Assert.ThrowsAsync<DomainValidationException>(() => new TransactionImportService(repo, new TransactionImportParser()).ImportAsync(stream, "test.csv"));
        Assert.Empty(await repo.GetTransactionsAsync()); Assert.Equal(count, (await repo.GetAccountsAsync()).Count);
        Assert.DoesNotContain(await repo.GetCategoriesAsync(), c => c.Name == "Nuova");
    });
    [Fact] public Task BatchFailure_RollsBackAccountsCategoriesAndTransactions() => WithDatabase(async repo =>
    {
        await repo.InitializeAsync(); var account = new Account(Guid.NewGuid(), "Nuovo", 0);
        var bad = new Transaction(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, account.Id, Guid.NewGuid(), 10m);
        await Assert.ThrowsAnyAsync<Exception>(() => repo.SaveBatchAsync([account], [], [bad], "probe", "saved"));
        Assert.Empty(await repo.GetAccountsAsync()); Assert.Null(await repo.GetStateAsync("probe"));
    });
    [Fact] public Task ArchivedReferences_CanBeKeptWhenEditingHistory() => WithDatabase(async repo =>
    {
        var (account, category) = await SetupAsync(repo); var service = new TransactionService(repo);
        var t = await service.SaveAsync(new(null, DateTime.UtcNow, TransactionType.Expense, account.Id, category.Id, 10m, "EUR", "Spesa", null));
        category.SetActive(false); await repo.SaveCategoryAsync(category);
        await service.SaveAsync(new(t.Id, t.OccurredAtUtc, t.Type, account.Id, category.Id, t.Amount, t.Currency, t.Description, "Nota corretta"));
        Assert.Equal("Nota corretta", (await repo.GetTransactionAsync(t.Id))!.Notes);
        await Assert.ThrowsAsync<DomainValidationException>(() => service.SaveAsync(new(null, DateTime.UtcNow, t.Type, account.Id, category.Id, 10m, "EUR", null, null)));
    });
    [Fact] public Task NativeCsv_PreservesIdDescriptionNotesAndInstant() => WithDatabase(async repo =>
    {
        var (account, category) = await SetupAsync(repo);
        var transaction = new Transaction(Guid.NewGuid(), new DateTime(2026, 10, 9, 23, 30, 0, DateTimeKind.Utc), TransactionType.Expense, account.Id, category.Id, 6.50m, description: "Pranzo; con amici", notes: "13");
        await repo.SaveTransactionAsync(transaction);
        var csv = await new CsvExportService(repo).ExportAsync(); using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv.Content));
        await new TransactionImportService(repo, new TransactionImportParser()).ImportAsync(stream, csv.FileName);
        var restored = Assert.Single(await repo.GetTransactionsAsync());
        Assert.Equal(transaction.Id, restored.Id); Assert.Equal(transaction.Description, restored.Description); Assert.Equal("13", restored.Notes); Assert.Equal(transaction.OccurredAtUtc, restored.OccurredAtUtc);
    });
    [Fact] public Task ItalianDate_IsDayFirst() => WithDatabase(async repo =>
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Data;Tipo;Conto;Categoria;Importo;Valuta\n04/05/2026;Uscita;Conto;Spesa;6,50;EUR"));
        var row = Assert.Single((await new TransactionImportParser().ParseAsync(stream, "test.csv")).Transactions);
        Assert.Equal(new DateTime(2026, 5, 4), row.OccurredAtLocal); Assert.Equal(6.50m, row.Amount);
    });
    [Fact] public Task AccountCurrencyCannotChangeWithHistory() => WithDatabase(async repo =>
    {
        var (account, category) = await SetupAsync(repo);
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, account.Id, category.Id, 10m));
        await Assert.ThrowsAsync<DomainValidationException>(() => new AccountService(repo).SaveAsync(new(account.Id, account.Name, account.OpeningBalance, "USD", true, true)));
        Assert.Equal("EUR", (await repo.GetAccountAsync(account.Id))!.Currency);
    });
    [Fact] public Task SearchAndPagination_UseLiteralTextAndStableOrder() => WithDatabase(async repo =>
    {
        var (account, category) = await SetupAsync(repo);
        foreach (var description in new[] { "Caffe 100%", "Altro", "Caffe 100%" })
            await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, account.Id, category.Id, 2m, description: description));
        var page1 = Assert.Single(await repo.GetTransactionsAsync(new(Search: "100%", Limit: 1)));
        var page2 = Assert.Single(await repo.GetTransactionsAsync(new(Search: "100%", Limit: 1, Offset: 1)));
        Assert.NotEqual(page1.Id, page2.Id); Assert.Equal(2, (await repo.GetTransactionsAsync(new(Search: "100%"))).Count);
    });
    [Fact] public async Task VersionFourUpgrade_PreservesHistoricalData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"v4-{Guid.NewGuid():N}.db3");
        Guid transactionId;
        try
        {
            await using (var repo = new SqliteExpenseRepository(path))
            {
                var (account, category) = await SetupAsync(repo);
                var transaction = new Transaction(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, account.Id, category.Id, 6.50m, description: "Storico");
                transactionId = transaction.Id; await repo.SaveTransactionAsync(transaction);
            }
            using (var old = new SQLite.SQLiteConnection(path))
            {
                old.Execute("DROP INDEX IX_Transactions_TransferId");
                old.Execute("ALTER TABLE Transactions DROP COLUMN TransferId");
                old.Execute("DROP TABLE AppState");
                old.Execute("PRAGMA user_version = 4");
            }
            await using var upgraded = new SqliteExpenseRepository(path);
            var saved = Assert.Single(await upgraded.GetTransactionsAsync());
            Assert.Equal(transactionId, saved.Id); Assert.Equal(6.50m, saved.Amount); Assert.Null(saved.TransferId);
            Assert.Equal(993.50m, Assert.Single(await upgraded.GetAccountSummariesAsync()).CurrentBalance);
            Assert.Empty((await new PlanningService(upgraded).GetAsync()).Rules);
        }
        finally { File.Delete(path); }
    }
    [Fact] public Task LegacyCurrencyMismatch_IsFlaggedAndNeverSummedIntoEuros() => WithDatabase(async repo =>
    {
        var (account, category) = await SetupAsync(repo);
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, account.Id, category.Id, 10m, "USD"));
        var balance = Assert.Single(await repo.GetAccountSummariesAsync());
        Assert.True(balance.HasCurrencyMismatch); Assert.Equal(1000m, balance.CurrentBalance);
        var report = await new ReportingService(repo).GetDashboardAsync(MonthlyPeriods.FullMonth(DateTime.Today), currency: "EUR");
        Assert.NotNull(report.BalanceWarning); Assert.Equal(0m, report.Totals.Expense);
    });

}
