using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Infrastructure;

public sealed class AssistantClient(HttpClient http) : IAssistantClient
{
    public Task<AssistantHealth> CheckAsync(AssistantConnection connection, CancellationToken cancellationToken = default) =>
        SendAsync(connection, "health", null, AssistantJsonContext.Default.AssistantHealth, TimeSpan.FromSeconds(10), cancellationToken);

    public Task<AssistantReportResponse> GenerateAsync(AssistantConnection connection, AssistantReportRequest report, CancellationToken cancellationToken = default) =>
        SendAsync(connection, "reports", report, AssistantJsonContext.Default.AssistantReportResponse, TimeSpan.FromSeconds(150), cancellationToken);

    private async Task<T> SendAsync<T>(AssistantConnection connection, string path, AssistantReportRequest? body, JsonTypeInfo<T> typeInfo,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        var baseUri = connection.GetBaseUri();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, new Uri(baseUri, path));
        request.Headers.Add("X-Assistant-Key", connection.AccessKey);
        if (body is not null) request.Content = JsonContent.Create(body, AssistantJsonContext.Default.AssistantReportRequest);
        try
        {
            using var response = await http.SendAsync(request, deadline.Token);
            if (!response.IsSuccessStatusCode)
                throw new DomainValidationException(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "La chiave di accesso non è corretta. Controlla quella mostrata sul PC.",
                    HttpStatusCode.TooManyRequests => "L'assistente sta già preparando un resoconto. Riprova tra poco.",
                    HttpStatusCode.ServiceUnavailable => "Ollama non è pronto: avvialo sul PC e verifica che qwen2.5:3b sia installato.",
                    HttpStatusCode.BadGateway => "Il modello non ha restituito un resoconto completo. Riprova.",
                    HttpStatusCode.GatewayTimeout => "Il modello ha impiegato troppo tempo. Riprova con il PC libero da altri lavori.",
                    _ => "Il servizio Assistente non ha completato la richiesta. Controlla il terminale sul PC."
                });
            return await response.Content.ReadFromJsonAsync(typeInfo, deadline.Token)
                ?? throw new DomainValidationException("Il servizio ha restituito una risposta vuota.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new DomainValidationException("L'assistente non ha risposto in tempo. Controlla la connessione e riprova."); }
        catch (HttpRequestException)
        { throw new DomainValidationException("Assistente offline. Verifica indirizzo, PC acceso, stessa rete Wi-Fi e firewall."); }
        catch (JsonException)
        { throw new DomainValidationException("Risposta del servizio non valida. Verifica l'indirizzo configurato."); }
    }
}
