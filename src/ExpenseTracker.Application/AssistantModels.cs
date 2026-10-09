using System.Net;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public enum AssistantReportKind { MonthlySummary, MonthComparison }
public sealed record AssistantCategory(string Name, decimal Expense);
public sealed record AssistantPeriod(DateOnly From, DateOnly Through, decimal Income, decimal Expense,
    int MovementCount, IReadOnlyList<AssistantCategory> Categories)
{
    public decimal Net => Income - Expense;
}
public sealed record AssistantReportRequest(AssistantReportKind Kind, string Currency,
    AssistantPeriod Current, AssistantPeriod? Previous, decimal? ExpenseChange, decimal? ExpenseChangePercent);
public sealed record AssistantReportResponse(string Text, string Model);
public sealed record AssistantHealth(string Model);
public sealed record AssistantConnection(string Address, string AccessKey)
{
    public Uri GetBaseUri()
    {
        if (!Uri.TryCreate(Address.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
            throw new DomainValidationException("Inserisci l'indirizzo del PC, ad esempio http://192.168.1.20:5188.");
        if (uri.Scheme == "http" && !IsLocalHost(uri.Host))
            throw new DomainValidationException("Per HTTP usa un indirizzo IP della rete locale oppure un nome .local.");
        if (AccessKey.Length is < 32 or > 256 || AccessKey.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new DomainValidationException("Inserisci la chiave di accesso mostrata dal servizio sul PC.");
        return uri;
    }

    private static bool IsLocalHost(string host)
    {
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)) return true;
        if (!IPAddress.TryParse(host.Trim('[', ']'), out var ip)) return false;
        if (IPAddress.IsLoopback(ip)) return true;
        var b = ip.GetAddressBytes();
        return b.Length == 4 ? b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] is >= 16 and <= 31)
            : ip.IsIPv6LinkLocal || (b[0] & 0xfe) == 0xfc;
    }
}
public interface IAssistantClient
{
    Task<AssistantHealth> CheckAsync(AssistantConnection connection, CancellationToken cancellationToken = default);
    Task<AssistantReportResponse> GenerateAsync(AssistantConnection connection, AssistantReportRequest report, CancellationToken cancellationToken = default);
}
