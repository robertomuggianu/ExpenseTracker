using System.Text;
using ExpenseTracker.Application;

namespace ExpenseTracker.Presentation.Services;

public interface IUserDialogService
{
    Task ShowMessageAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);
}

public interface IAppNavigationService
{
    Task GoToTransactionEditorAsync(Guid? transactionId = null);

    Task GoToTransactionsAsync();
}

public interface IFileShareService
{
    Task ShareAsync(CsvExportResult export, CancellationToken cancellationToken = default);
}

public sealed class UserDialogService : IUserDialogService
{
    public Task ShowMessageAsync(string title, string message) =>
        Shell.Current.DisplayAlertAsync(title, message, "OK");

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
}

public sealed class AppNavigationService : IAppNavigationService
{
    public Task GoToTransactionEditorAsync(Guid? transactionId = null)
    {
        var route = transactionId.HasValue
            ? $"//NewTransaction?transactionId={Uri.EscapeDataString(transactionId.Value.ToString("D"))}"
            : "//NewTransaction";
        return Shell.Current.GoToAsync(route);
    }

    public Task GoToTransactionsAsync() => Shell.Current.GoToAsync("//Transactions");
}

public sealed class FileShareService : IFileShareService
{
    public async Task ShareAsync(CsvExportResult export, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, export.FileName);
        await File.WriteAllTextAsync(path, export.Content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Esporta movimenti",
            File = new ShareFile(path, "text/csv")
        });
    }
}
