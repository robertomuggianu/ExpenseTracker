using System.Text;
using ExpenseTracker.Application;
using ExpenseTracker.Presentation.Controls;

namespace ExpenseTracker.Presentation.Services;

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

public sealed class AppNavigationService(NavigationRequests requests) : IAppNavigationService
{
    public Task GoToTransactionEditorAsync(Guid? transactionId = null, bool duplicate = false)
    {
        var route = transactionId.HasValue
            ? $"//NewTransactionRoot/NewTransaction?transactionId={Uri.EscapeDataString(transactionId.Value.ToString("D"))}"
            : "//NewTransactionRoot/NewTransaction";
        if (duplicate) route += "&duplicate=true";
        return Shell.Current.GoToAsync(route);
    }

    public Task GoToTransactionsAsync(TransactionFilter? filter = null, string? message = null)
    {
        requests.Filter = filter;
        requests.Message = message;
        return Shell.Current.GoToAsync("//TransactionsRoot/Transactions");
    }
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

public sealed class AppPreferences : IAppPreferences
{
    public string Get(string key, string fallback) => Preferences.Default.Get(key, fallback);
    public bool Get(string key, bool fallback) => Preferences.Default.Get(key, fallback);
    public void Set(string key, string value) => Preferences.Default.Set(key, value);
    public void Set(string key, bool value) => Preferences.Default.Set(key, value);
    public void Remove(string key) => Preferences.Default.Remove(key);
}
