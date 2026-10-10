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
        SendAsync(connection, "reports", JsonContent.Create(report, AssistantJsonContext.Default.AssistantReportRequest), AssistantJsonContext.Default.AssistantReportResponse, TimeSpan.FromSeconds(210), cancellationToken);

    public Task<AssistantInterpretation> InterpretAsync(AssistantConnection connection, AssistantQuestionRequest question, CancellationToken cancellationToken = default) =>
        SendAsync(connection, "chat/interpret", JsonContent.Create(question, AssistantJsonContext.Default.AssistantQuestionRequest), AssistantJsonContext.Default.AssistantInterpretation, TimeSpan.FromSeconds(210), cancellationToken);

    public Task<AssistantChatAnswer> AnswerAsync(AssistantConnection connection, AssistantChatEvidence evidence, CancellationToken cancellationToken = default) =>
        SendAsync(connection, "chat/answer", JsonContent.Create(evidence, AssistantJsonContext.Default.AssistantChatEvidence), AssistantJsonContext.Default.AssistantChatAnswer, TimeSpan.FromSeconds(210), cancellationToken);

    private async Task<T> SendAsync<T>(AssistantConnection connection, string path, HttpContent? body, JsonTypeInfo<T> typeInfo,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var payload = body;
        var baseUri = connection.GetBaseUri();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var request = new HttpRequestMessage(body is null ? HttpMethod.Get : HttpMethod.Post, new Uri(baseUri, path));
        request.Headers.Add("X-Assistant-Key", connection.AccessKey);
        request.Content = body;
        try
        {
            using var response = await http.SendAsync(request, deadline.Token);
            if (!response.IsSuccessStatusCode)
                throw new DomainValidationException(response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "The access key is incorrect. Check the key shown on your PC.",
                    HttpStatusCode.TooManyRequests => "The assistant is already processing a request. Please try again shortly.",
                    HttpStatusCode.ServiceUnavailable => "Ollama is not ready. Start it on your PC and check that the model configured in the assistant server is installed.",
                    HttpStatusCode.BadGateway => "The model did not return a complete response. Please try again.",
                    HttpStatusCode.GatewayTimeout => "The model took too long. Try again with fewer tasks running on your PC.",
                    HttpStatusCode.NotFound => "The PC service does not support this feature. Restart it from the updated project.",
                    HttpStatusCode.UpgradeRequired => "Update the app and restart the assistant service from the updated project.",
                    HttpStatusCode.BadRequest => "This request is not valid for this chat. Please clarify it.",
                    _ => "The assistant service could not complete the request. Check the terminal on your PC."
                });
            return await response.Content.ReadFromJsonAsync(typeInfo, deadline.Token)
                ?? throw new DomainValidationException("The service returned an empty response.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new DomainValidationException("The assistant did not respond in time. Check the connection and try again."); }
        catch (HttpRequestException)
        { throw new DomainValidationException("Assistant offline. Check the address, that your PC is running, the same Wi-Fi network and the firewall."); }
        // Android's native HTTP handler can expose WebException instead of HttpRequestException.
        catch (WebException) when (cancellationToken.IsCancellationRequested)
        { throw new OperationCanceledException(cancellationToken); }
        catch (WebException e) when (e.Status == WebExceptionStatus.Timeout || deadline.IsCancellationRequested)
        { throw new DomainValidationException("The assistant did not respond in time. Check the connection and try again."); }
        catch (WebException)
        { throw new DomainValidationException("The connection to the assistant was interrupted. Check your PC and network, then try again."); }
        catch (JsonException)
        { throw new DomainValidationException("Invalid service response. Check the configured address."); }
    }
}
