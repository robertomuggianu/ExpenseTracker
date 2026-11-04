using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Presentation.Services;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Application.Tests;

public sealed class ExportWorkflowTests
{
    [Fact]
    public Task ReloadPreservesSelectedIdsAndUsesRefreshedNamesInExportChoices() => WithDatabase(async repo =>
    {
        var csv = new CsvExport();
        var share = new FileShare();
        var vm = Editor(repo, csv, share);
        await vm.LoadAsync();
        var account = Assert.Single(await repo.GetAccountsAsync());
        var category = (await repo.GetCategoriesAsync()).First();
        vm.SelectedAccount = vm.Accounts.Single(a => a.Id == account.Id);
        vm.SelectedCategory = vm.Categories.Single(c => c.Id == category.Id);
        account.Update("Conto rinominato", account.OpeningBalance, account.Currency);
        await repo.SaveAccountsAsync([account]);
        await new CategoryService(repo).SaveCategoryAsync(new(category.Id, "Categoria rinominata", category.Type,
            category.Color, category.IsActive, category.Icon));

        await vm.LoadAsync();

        Assert.Same(vm.Accounts.Single(a => a.Id == account.Id), vm.SelectedAccount);
        Assert.Same(vm.Categories.Single(c => c.Id == category.Id), vm.SelectedCategory);
        Assert.Equal("Conto rinominato", vm.SelectedAccount!.Name);
        Assert.Equal("Categoria rinominata", vm.SelectedCategory!.Name);
        vm.ExportAll = false;
        await vm.ExportCommand.ExecuteAsync(null);
        Assert.Null(vm.ErrorMessage);
        Assert.Equal(account.Id, csv.Filter?.AccountId);
        Assert.Equal(category.Id, csv.Filter?.CategoryId);
        Assert.Equal(1, csv.Calls);
        Assert.Equal(1, share.Calls);
    });

    [Fact]
    public Task ReloadFallsBackToAllWhenSelectedEntitiesNoLongerExist() => WithDatabase(async repo =>
    {
        var vm = Editor(repo);
        await vm.LoadAsync();
        vm.SelectedAccount = new(Guid.NewGuid(), "Conto rimosso");
        vm.SelectedCategory = new(Guid.NewGuid(), "Categoria rimossa");

        await vm.LoadAsync();

        Assert.Same(vm.Accounts[0], vm.SelectedAccount);
        Assert.Same(vm.Categories[0], vm.SelectedCategory);
        Assert.Null(vm.SelectedAccount!.Id);
        Assert.Null(vm.SelectedCategory!.Id);
    });

    [Fact]
    public Task PeriodChangesNotifyCustomDateVisibility() => WithDatabase(async repo =>
    {
        var vm = Editor(repo);
        await vm.LoadAsync();
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        Assert.False(vm.IsCustomPeriod);

        vm.SelectedPeriod = vm.Periods.Single(p => p.Value == DateRangePreset.Custom);

        Assert.True(vm.IsCustomPeriod);
        Assert.Contains(nameof(vm.IsCustomPeriod), changes);
        changes.Clear();
        vm.SelectedPeriod = vm.Periods.Single(p => p.Value == DateRangePreset.CurrentYear);
        Assert.False(vm.IsCustomPeriod);
        Assert.Contains(nameof(vm.IsCustomPeriod), changes);
    });

    [Fact]
    public Task InvalidCustomIntervalShowsErrorWithoutExportOrShare() => WithDatabase(async repo =>
    {
        var csv = new CsvExport();
        var share = new FileShare();
        var dialogs = new Dialogs();
        var vm = Editor(repo, csv, share, dialogs);
        await vm.LoadAsync();
        vm.ExportAll = false;
        vm.SelectedPeriod = vm.Periods.Single(p => p.Value == DateRangePreset.Custom);
        vm.CustomFrom = new(2026, 11, 10);
        vm.CustomTo = new(2026, 11, 9);

        await vm.ExportCommand.ExecuteAsync(null);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("data iniziale", vm.ErrorMessage);
        Assert.Equal(0, csv.Calls);
        Assert.Equal(0, share.Calls);
        Assert.Equal(0, dialogs.Messages);
        Assert.False(vm.IsBusy);
    });

    [Fact]
    public Task SuccessfulImportRefreshesChoicesAndPreservesExistingSelections() => WithDatabase(async repo =>
    {
        var parser = new ImportParser(new ImportedTransactionRow("export-refresh", DateTime.Today, TransactionType.Expense,
            "Conto importato", "Categoria importata", 11m, "EUR", null));
        var vm = Editor(repo, parser: parser, picker: new ImportPicker());
        await vm.LoadAsync();
        vm.SelectedAccount = vm.Accounts.First(a => a.Id.HasValue);
        vm.SelectedCategory = vm.Categories.First(c => c.Id.HasValue);
        var accountId = vm.SelectedAccount.Id;
        var categoryId = vm.SelectedCategory.Id;

        await vm.ImportCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorMessage);
        Assert.NotNull(vm.ImportSummary);
        Assert.Contains(vm.Accounts, a => a.Name == "Conto importato" && a.Id.HasValue);
        Assert.Contains(vm.Categories, c => c.Name == "Categoria importata" && c.Id.HasValue);
        Assert.Equal(accountId, vm.SelectedAccount?.Id);
        Assert.Equal(categoryId, vm.SelectedCategory?.Id);
    });

    private static ExportViewModel Editor(SqliteExpenseRepository repo, CsvExport? csv = null, FileShare? share = null,
        Dialogs? dialogs = null, ImportParser? parser = null, ImportPicker? picker = null) =>
        new(csv ?? new(), share ?? new(), new(repo, parser ?? new()), picker ?? new(), new(repo), new(repo), dialogs ?? new());

    private sealed class CsvExport : ICsvExportService
    {
        public int Calls { get; private set; }
        public TransactionFilter? Filter { get; private set; }
        public Task<CsvExportResult> ExportAsync(TransactionFilter? filter = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            Filter = filter;
            return Task.FromResult(new CsvExportResult("movimenti.csv", "csv"));
        }
    }

    private sealed class FileShare : IFileShareService
    {
        public int Calls { get; private set; }
        public Task ShareAsync(CsvExportResult export, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class Dialogs : IUserDialogService
    {
        public int Messages { get; private set; }
        public Task ShowMessageAsync(string title, string message) { Messages++; return Task.CompletedTask; }
        public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) => Task.FromResult(true);
    }

    private sealed class ImportPicker : IImportFilePickerService
    {
        public Task<SelectedImportFile?> PickAsync() => Task.FromResult<SelectedImportFile?>(new("import.csv", new MemoryStream()));
    }

    private sealed class ImportParser(ImportedTransactionRow? row = null) : ITransactionImportParser
    {
        public Task<ImportParseResult> ParseAsync(Stream stream, string fileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ImportParseResult(row is null ? [] : [row], 0, []));
    }

    private static async Task WithDatabase(Func<SqliteExpenseRepository, Task> test)
    {
        var path = Path.Combine(Path.GetTempPath(), $"export-workflow-{Guid.NewGuid():N}.db3");
        try
        {
            await using var repo = new SqliteExpenseRepository(path);
            await repo.SaveAccountsAsync([new Account(Guid.NewGuid(), "Principale", 100m, isPrimary: true)]);
            await test(repo);
        }
        finally { File.Delete(path); }
    }
}
