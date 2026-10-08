using System.Text;
using ExpenseTracker.Application;
using ExpenseTracker.Presentation.Controls;

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

public sealed class UserDialogService : IUserDialogService
{
    public async Task ShowMessageAsync(string title, string message) =>
        _ = await StyledDialogPage.ShowAsync(title, message, "OK");

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        StyledDialogPage.ShowAsync(title, message, accept, cancel);
}

public sealed class ImportFilePickerService : IImportFilePickerService
{
    public async Task<SelectedImportFile?> PickAsync()
    {
        var fileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
        {
            [DevicePlatform.Android] =
            [
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "text/csv",
                "text/comma-separated-values"
            ],
            [DevicePlatform.WinUI] = [".xlsx", ".csv"],
            [DevicePlatform.iOS] =
            [
                "org.openxmlformats.spreadsheetml.sheet",
                "public.comma-separated-values-text"
            ],
            [DevicePlatform.MacCatalyst] =
            [
                "org.openxmlformats.spreadsheetml.sheet",
                "public.comma-separated-values-text"
            ]
        });
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Scegli il file da importare",
            FileTypes = fileTypes
        });
        return result is null
            ? null
            : new SelectedImportFile(result.FileName, await result.OpenReadAsync());
    }
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
