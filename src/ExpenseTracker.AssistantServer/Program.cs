using System.Security.Cryptography;
using System.Text;
using ExpenseTracker.Application;
using ExpenseTracker.AssistantServer;

var builder = WebApplication.CreateBuilder(args);
var key = builder.Configuration["Assistant:AccessKey"] ?? "";
if (key.Length is < 32 or > 256 || key.Any(c => !char.IsAsciiLetterOrDigit(c)))
    throw new InvalidOperationException("Imposta Assistant__AccessKey con una chiave alfanumerica di almeno 32 caratteri, oppure usa scripts/start-assistant.ps1.");
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 32 * 1024);
builder.Services.AddSingleton(new OllamaGateway(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
{
    BaseAddress = new Uri(builder.Configuration["Assistant:OllamaAddress"] ?? "http://127.0.0.1:11434/"),
    Timeout = TimeSpan.FromSeconds(180)
}, builder.Configuration["Assistant:Model"] ?? "qwen3.5:9b"));
builder.Services.AddSingleton<InferenceGate>();
var app = builder.Build();
var expectedKey = SHA256.HashData(Encoding.UTF8.GetBytes(key));
app.Use(async (context, next) =>
{
    var supplied = context.Request.Headers["X-Assistant-Key"].ToString();
    if (supplied.Length > 256 || !CryptographicOperations.FixedTimeEquals(expectedKey, SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))
    { context.Response.StatusCode = 401; return; }
    await next(context);
});
app.MapGet("/health", async (OllamaGateway ollama, CancellationToken token) =>
{
    try { return await ollama.IsReadyAsync(token) ? Results.Ok(new AssistantHealth(ollama.Model)) : Results.StatusCode(503); }
    catch (HttpRequestException) { return Results.StatusCode(503); }
    catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Results.StatusCode(503); }
    catch (System.Text.Json.JsonException) { return Results.StatusCode(503); }
});
app.MapPost("/reports", async (AssistantReportRequest report, OllamaGateway ollama, InferenceGate gate, CancellationToken token) =>
{
    if (!ReportValidation.IsValid(report)) return Results.BadRequest();
    if (!await gate.Semaphore.WaitAsync(0, token)) return Results.StatusCode(429);
    try { return Results.Ok(await ollama.GenerateAsync(report, token)); }
    catch (InvalidDataException) { return Results.StatusCode(502); }
    catch (System.Text.Json.JsonException) { return Results.StatusCode(502); }
    catch (HttpRequestException) { return Results.StatusCode(503); }
    catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Results.StatusCode(504); }
    finally { gate.Semaphore.Release(); }
});
app.MapPost("/chat/interpret", async (AssistantQuestionRequest question, OllamaGateway ollama, InferenceGate gate, CancellationToken token) =>
{
    if (!AssistantChatPolicy.ValidQuestionRequest(question)) return Results.BadRequest();
    if (!await gate.Semaphore.WaitAsync(0, token)) return Results.StatusCode(429);
    try { return Results.Ok(await ollama.InterpretAsync(question, token)); }
    catch (InvalidDataException) { return Results.StatusCode(502); }
    catch (HttpRequestException) { return Results.StatusCode(503); }
    catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Results.StatusCode(504); }
    finally { gate.Semaphore.Release(); }
});
app.MapPost("/chat/answer", async (AssistantChatEvidence evidence, OllamaGateway ollama, InferenceGate gate, CancellationToken token) =>
{
    if (evidence.Periods is null) return Results.StatusCode(426);
    if (!AssistantChatPolicy.ValidEvidence(evidence) || !ReportNumberGuard.UsesProvidedNumbers(evidence.CalculatedAnswer, evidence.Facts)) return Results.BadRequest();
    if (!await gate.Semaphore.WaitAsync(0, token)) return Results.StatusCode(429);
    try { return Results.Ok(await ollama.AnswerAsync(evidence, token)); }
    catch (HttpRequestException) { return Results.StatusCode(503); }
    catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Results.StatusCode(504); }
    finally { gate.Semaphore.Release(); }
});
app.Run();

public sealed class InferenceGate : IDisposable
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public void Dispose() => Semaphore.Dispose();
}
