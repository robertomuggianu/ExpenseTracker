using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExpenseTracker.AssistantServer;
using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Presentation.Services;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Application.Tests;

public sealed class AssistantTests
{
    private const string Key = "0123456789abcdef0123456789abcdef";
    private static AssistantReportRequest Report() => new(AssistantReportKind.MonthComparison, "EUR",
        new(new(2026, 10, 1), new(2026, 10, 9), 1000m, 360m, 2, [new("Cibo", 360m)]),
        new(new(2026, 9, 1), new(2026, 9, 9), 0, 300m, 1, [new("Cibo", 300m)]), 60m, 20m);

    private static async Task WithDatabase(Func<SqliteExpenseRepository, Task> test)
    {
        var path = Path.Combine(Path.GetTempPath(), $"assistant-{Guid.NewGuid():N}.db3");
        try { await using var repo = new SqliteExpenseRepository(path); await test(repo); }
        finally { File.Delete(path); }
    }

    [Fact] public Task Snapshot_UsesSameDaysCurrencyAndExcludesTransfersFutureAndPrivateDetails() => WithDatabase(async repo =>
    {
        var account = new Account(Guid.NewGuid(), "Conto privato", 0m, isPrimary: true);
        await repo.SaveAccountsAsync([account]);
        var category = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
        async Task Add(DateTime date, decimal amount, string currency = "EUR", Guid? transfer = null) =>
            await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime(),
                TransactionType.Expense, account.Id, category.Id, amount, currency, "descrizione privata", "nota privata", transferId: transfer));
        await Add(new(2026, 10, 9), 360m);
        await Add(new(2026, 9, 9), 300m);
        await Add(new(2026, 9, 10), 900m);
        await Add(new(2026, 10, 10), 900m);
        await Add(new(2026, 10, 9), 900m, "USD");
        await Add(new(2026, 10, 9), 900m, transfer: Guid.NewGuid());
        var report = await new AssistantReportService(repo).CreateAsync(AssistantReportKind.MonthComparison, "EUR", new(2026, 10, 9));
        Assert.Equal(360m, report.Current.Expense); Assert.Equal(300m, report.Previous!.Expense);
        Assert.Equal(60m, report.ExpenseChange); Assert.Equal(20m, report.ExpenseChangePercent);
        Assert.Equal(1, report.Current.MovementCount); Assert.True(ReportValidation.IsValid(report));
        var json = JsonSerializer.Serialize(report);
        Assert.DoesNotContain("privata", json); Assert.DoesNotContain("Conto privato", json);
    });

    [Theory] [InlineData(2026, 3, 31, 28)] [InlineData(2024, 3, 31, 29)] [InlineData(2026, 1, 9, 9)]
    public Task Snapshot_ClampsMonthEndAndHandlesYearBoundaryAndZeroBaseline(int year, int month, int day, int previousDay) => WithDatabase(async repo =>
    {
        var result = await new AssistantReportService(repo).CreateAsync(AssistantReportKind.MonthComparison, "EUR", new(year, month, day));
        Assert.Equal(previousDay, result.Previous!.Through.Day);
        Assert.Equal(DateOnly.FromDateTime(new DateTime(year, month, 1).AddMonths(-1)), result.Previous.From);
        Assert.Null(result.ExpenseChangePercent); Assert.True(ReportValidation.IsValid(result));
    });

    [Fact] public void Validation_RejectsMixedOrInconsistentFactsAndInvalidKinds()
    {
        var r = Report(); Assert.True(ReportValidation.IsValid(r));
        Assert.False(ReportValidation.IsValid(r with { ExpenseChangePercent = 99 }));
        Assert.False(ReportValidation.IsValid(r with { Currency = "EURO" }));
        Assert.False(ReportValidation.IsValid(r with { Current = r.Current with { Categories = [] } }));
        Assert.False(ReportValidation.IsValid(r with { Kind = (AssistantReportKind)999 }));
        Assert.False(ReportValidation.IsValid(r with { Previous = r.Previous! with { Through = new(2026, 9, 30) } }));
    }

    [Theory] [InlineData("http://192.168.1.20:5188", true)] [InlineData("http://localhost:5188", true)]
    [InlineData("http://10.0.0.20:5188", true)] [InlineData("http://pc.local:5188", true)]
    [InlineData("http://example.com:5188", false)] [InlineData("http://8.8.8.8:5188", false)]
    [InlineData("http://192.168.1.20:5188/reports", false)] [InlineData("http://user:pass@192.168.1.20", false)]
    public void Connection_RestrictsHttpAndRejectsUnexpectedPaths(string address, bool valid)
    {
        var c = new AssistantConnection(address, Key);
        if (valid) Assert.NotNull(c.GetBaseUri()); else Assert.Throws<DomainValidationException>(() => c.GetBaseUri());
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }

    [Fact] public async Task Client_SendsOnlyFactsAndKey_AndReadsResponse()
    {
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            Assert.Equal("http://192.168.1.20:5188/reports", request.RequestUri!.AbsoluteUri);
            Assert.Equal(Key, request.Headers.GetValues("X-Assistant-Key").Single());
            var sent = await request.Content!.ReadFromJsonAsync<AssistantReportRequest>(token);
            Assert.Equal(360m, sent!.Current.Expense);
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new AssistantReportResponse("Spese aumentate del 20%.", "qwen2.5:3b")) };
        }));
        var response = await new AssistantClient(http).GenerateAsync(new("http://192.168.1.20:5188", Key), Report());
        Assert.Equal("Spese aumentate del 20%.", response.Text);
    }

    [Theory] [InlineData(401, "key")] [InlineData(503, "Ollama")] [InlineData(429, "already")] [InlineData(426, "Update")]
    public async Task Client_TranslatesOperationalErrors(int status, string word)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))));
        var error = await Assert.ThrowsAsync<DomainValidationException>(() => new AssistantClient(http).CheckAsync(new("http://localhost:5188", Key)));
        Assert.Contains(word, error.Message);
    }

    [Fact] public async Task Gateway_UsesConfiguredModelAndRejectsTruncatedAnswers()
    {
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(token);
            Assert.Equal("qwen2.5:3b", body.GetProperty("model").GetString());
            Assert.False(body.GetProperty("stream").GetBoolean());
            Assert.False(body.GetProperty("think").GetBoolean());
            Assert.Contains("360", body.GetProperty("messages")[1].GetProperty("content").GetString());
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { done = true, done_reason = "length", message = new { content = "Testo troncato" } }) };
        })) { BaseAddress = new("http://localhost:11434/") };
        await Assert.ThrowsAsync<InvalidDataException>(() => new OllamaGateway(http, "qwen2.5:3b").GenerateAsync(Report(), default));
    }

    [Fact] public void NumberGuard_RejectsInventedPercentagesButAllowsItalianAmountsAndDates()
    {
        const string facts = "Dal 01/10/2026 al 09/10/2026. Entrate 1000,00 EUR. Spese 360,00 EUR. Differenza 640,00 EUR.";
        Assert.True(ReportNumberGuard.UsesProvidedNumbers("Le spese sono 360 euro, le entrate 1.000 euro.", facts));
        Assert.False(ReportNumberGuard.UsesProvidedNumbers("Il cibo rappresenta il 95% delle spese.", facts));
        Assert.False(ReportNumberGuard.UsesProvidedNumbers("Hai speso 450 euro.", facts));
        Assert.False(ReportNumberGuard.UsesProvidedNumbers("Hai speso 36000 euro.", facts));
    }

    private sealed class Settings : IAssistantSettings
    {
        public Task<AssistantConnection> LoadAsync() => Task.FromResult(new AssistantConnection("http://localhost:5188", Key));
        public Task SaveAsync(AssistantConnection connection) { connection.GetBaseUri(); return Task.CompletedTask; }
    }
    private sealed class PendingClient : IAssistantClient
    {
        public int Calls;
        public Task<AssistantInterpretation> InterpretAsync(AssistantConnection c, AssistantQuestionRequest r, CancellationToken token = default) => throw new NotImplementedException();
        public Task<AssistantChatAnswer> AnswerAsync(AssistantConnection c, AssistantChatEvidence r, CancellationToken token = default) => throw new NotImplementedException();
        public Task<AssistantHealth> CheckAsync(AssistantConnection c, CancellationToken token = default) => Task.FromResult(new AssistantHealth("qwen2.5:3b"));
        public async Task<AssistantReportResponse> GenerateAsync(AssistantConnection c, AssistantReportRequest r, CancellationToken token = default)
        { Calls++; await Task.Delay(Timeout.Infinite, token); return new("Mai", "test"); }
    }

    [Fact] public Task ViewModel_CancelsPendingRequestAndPreventsDoubleGeneration() => WithDatabase(async repo =>
    {
        var account = new Account(Guid.NewGuid(), "Conto", 0, isPrimary: true); await repo.SaveAccountsAsync([account]);
        var cat = (await repo.GetCategoriesAsync()).First(c => c.Type == TransactionType.Expense);
        await repo.SaveTransactionAsync(new(Guid.NewGuid(), DateTime.UtcNow, TransactionType.Expense, account.Id, cat.Id, 10m));
        var client = new PendingClient(); var vm = new AssistantViewModel(new(repo), new(repo), client, new Settings(), new(repo));
        await vm.LoadCommand.ExecuteAsync(null);
        var task = vm.SummarizeMonthCommand.ExecuteAsync(null);
        for (var i = 0; i < 100 && client.Calls == 0; i++) await Task.Delay(10);
        Assert.Equal(1, client.Calls); Assert.False(vm.CanInteract);
        await vm.CompareMonthsCommand.ExecuteAsync(null); Assert.Equal(1, client.Calls);
        vm.CancelCommand.Execute(null); await task;
        Assert.False(vm.IsBusy); Assert.Equal("Request cancelled.", vm.StatusMessage);
        Assert.Single(vm.Messages); Assert.Null(vm.ErrorMessage);
    });

    [Fact] public Task ViewModel_EmptyDatabaseDoesNotSendToModel() => WithDatabase(async repo =>
    {
        var client = new PendingClient(); var vm = new AssistantViewModel(new(repo), new(repo), client, new Settings(), new(repo));
        await vm.LoadCommand.ExecuteAsync(null); await vm.SummarizeMonthCommand.ExecuteAsync(null);
        Assert.Equal(0, client.Calls); Assert.Contains("No transactions", Assert.Single(vm.Messages).Text);
    });
}
