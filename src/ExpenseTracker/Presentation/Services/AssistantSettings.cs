using ExpenseTracker.Application;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Presentation.Services;

public sealed class AssistantSettings(IAppPreferences preferences) : IAssistantSettings
{
    private const string AddressKey = "assistant.address";
    private const string SecretKey = "assistant.access-key";
    public async Task<AssistantConnection> LoadAsync()
    {
        string? key;
        try { key = await SecureStorage.Default.GetAsync(SecretKey); }
        catch { SecureStorage.Default.Remove(SecretKey); key = null; }
        return new(preferences.Get(AddressKey, ""), key ?? "");
    }
    public async Task SaveAsync(AssistantConnection connection)
    {
        var uri = connection.GetBaseUri();
        try { await SecureStorage.Default.SetAsync(SecretKey, connection.AccessKey); }
        catch { throw new DomainValidationException("Impossibile conservare la chiave in modo sicuro sul dispositivo. Riprova."); }
        preferences.Set(AddressKey, uri.AbsoluteUri.TrimEnd('/'));
    }
}
