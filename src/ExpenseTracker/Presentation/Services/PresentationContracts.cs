using ExpenseTracker.Application;
namespace ExpenseTracker.Presentation.Services;

public interface IUserDialogService
{
    Task ShowMessageAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}

public interface IAppNavigationService
{
    Task GoToTransactionEditorAsync(Guid? transactionId = null, bool duplicate = false, bool returnToHome = false);

    Task GoToDashboardAsync();

    Task GoToTransactionsAsync(TransactionFilter? filter = null, string? message = null);
}

public interface IFileShareService
{
    Task ShareAsync(CsvExportResult export, CancellationToken cancellationToken = default);
}

public interface IImportFilePickerService
{
    Task<SelectedImportFile?> PickAsync();
}

public sealed class SelectedImportFile(string fileName, Stream content) : IAsyncDisposable
{
    public string FileName { get; } = fileName;

    public Stream Content { get; } = content;

    public ValueTask DisposeAsync() => Content.DisposeAsync();
}


public interface IAppPreferences
{
    string Get(string key, string fallback);
    bool Get(string key, bool fallback);
    void Set(string key, string value);
    void Set(string key, bool value);
    void Remove(string key);
}

public sealed class NavigationRequests { public TransactionFilter? Filter { get; set; } public string? Message { get; set; } }

public interface IAssistantSettings
{
    Task<AssistantConnection> LoadAsync();
    Task SaveAsync(AssistantConnection connection);
}
