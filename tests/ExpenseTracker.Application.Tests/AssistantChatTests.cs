using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ExpenseTracker.AssistantServer;
using ExpenseTracker.Domain;
using ExpenseTracker.Infrastructure;
using ExpenseTracker.Presentation.Services;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Application.Tests;

public sealed class AssistantChatTests
{
    private static async Task Database(Func<SqliteExpenseRepository, Task> action)
    {
        var path = Path.Combine(Path.GetTempPath(), $"chat-{Guid.NewGuid():N}.db3");
        try { await using var repo = new SqliteExpenseRepository(path); await action(repo); }
        finally { File.Delete(path); }
    }

    private static async Task<(Account Account, Category Expense, Category Income)> Setup(SqliteExpenseRepository repo)
    {
        var account = new Account(Guid.NewGuid(), "Conto privato", 0, isPrimary: true);
        await repo.SaveAccountsAsync([account]);
        var expense = new Category(Guid.NewGuid(), "Cibo test", TransactionType.Expense, "#123456", isActive: false);
        var income = new Category(Guid.NewGuid(), "Stipendio test", TransactionType.Income, "#123456");
        await repo.SaveCategoryAsync(expense); await repo.SaveCategoryAsync(income);
        return (account, expense, income);
    }
    private static Task Add(SqliteExpenseRepository repo, Account account, Category category, DateTime date, decimal amount,
        string currency = "EUR", Guid? transfer = null) => repo.SaveTransactionAsync(new(Guid.NewGuid(),
            DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime(), category.Type, account.Id, category.Id,
            amount, currency, "descrizione privata", "nota privata", transferId: transfer));

    [Fact] public Task Queries_RespectCalendarCurrencyTransfersAndPrivacy() => Database(async repo =>
    {
        var (account, expense, income) = await Setup(repo);
        await Add(repo, account, expense, new(2026, 10, 10), 360);
        await Add(repo, account, income, new(2026, 10, 9), 1000);
        await Add(repo, account, expense, new(2026, 9, 10), 300);
        await Add(repo, account, expense, new(2026, 9, 30), 90);
        await Add(repo, account, expense, new(2026, 10, 11), 900);
        await Add(repo, account, expense, new(2026, 10, 10), 900, "USD");
        await Add(repo, account, expense, new(2026, 10, 10), 900, transfer: Guid.NewGuid());
        var service = new AssistantChatService(repo);
        var plan = new AssistantChatPlan(AssistantChatPeriod.Comparison, AssistantChatOperation.Total, AssistantChatMeasure.Expenses);
        var evidence = await service.ExecuteAsync("Confronta le spese", plan, "EUR", new(2026, 10, 10));
        Assert.Contains("360,00 EUR", evidence.Facts); Assert.Contains("300,00 EUR", evidence.Facts);
        Assert.Contains("60,00 EUR", evidence.Facts); Assert.Contains("20,00%", evidence.Facts);
        Assert.DoesNotContain("390,00", evidence.Facts); Assert.DoesNotContain("900,00", evidence.Facts);
        Assert.DoesNotContain("privat", JsonSerializer.Serialize(evidence));
        Assert.True(ReportNumberGuard.UsesProvidedNumbers(evidence.CalculatedAnswer, evidence.Facts));
        var previous = await service.ExecuteAsync("Mese scorso", plan with { Period = AssistantChatPeriod.Previous }, "EUR", new(2026, 10, 10));
        Assert.Contains("30/09/2026", previous.Facts); Assert.Contains("390,00 EUR", previous.Facts);
        var full = await service.ExecuteAsync("Confronta col mese completo", plan with { Comparison = AssistantChatComparison.FullPreviousMonth }, "EUR", new(2026, 10, 10));
        Assert.Contains("390,00 EUR", full.Facts); Assert.Contains("fewer days", full.CalculatedAnswer);
        Assert.Contains(expense.Name, await service.GetCategoryNamesAsync());
    });

    [Theory] [InlineData(AssistantChatOperation.AverageDaily, "36,00 EUR")]
    [InlineData(AssistantChatOperation.AverageTransaction, "180,00 EUR")]
    [InlineData(AssistantChatOperation.Largest, "240,00 EUR")]
    [InlineData(AssistantChatOperation.Count, "2")]
    public Task Aggregates_AreComputedInCode(AssistantChatOperation operation, string expected) => Database(async repo =>
    {
        var (account, expense, _) = await Setup(repo);
        await Add(repo, account, expense, new(2026, 10, 1), 120);
        await Add(repo, account, expense, new(2026, 10, 10), 240);
        var result = await new AssistantChatService(repo).ExecuteAsync("Domanda",
            new(AssistantChatPeriod.Current, operation, AssistantChatMeasure.Expenses, expense.Name.ToUpperInvariant()), "EUR", new(2026, 10, 10));
        Assert.Equal(expense.Name, result.Plan.Category);
        Assert.Contains(expected, result.CalculatedAnswer);
        Assert.True(ReportNumberGuard.UsesProvidedNumbers(result.CalculatedAnswer, result.Facts));
    });

    [Theory] [InlineData(2024, 3, 31, "29/02/2024")] [InlineData(2026, 3, 31, "28/02/2026")]
    [InlineData(2026, 1, 10, "10/12/2025")]
    public Task EmptyComparison_HandlesMonthLengthAndZeroBase(int year, int month, int day, string end) => Database(async repo =>
    {
        var result = await new AssistantChatService(repo).ExecuteAsync("Confronta",
            new(AssistantChatPeriod.Comparison, AssistantChatOperation.Total, AssistantChatMeasure.Expenses), "EUR", new(year, month, day));
        Assert.Contains(end, result.Facts); Assert.Contains("Percentage change cannot be calculated", result.Facts);
        Assert.Contains("No transactions recorded", result.CalculatedAnswer);
        Assert.DoesNotContain("NaN", result.Facts);
    });

    [Fact] public Task CategoryComparison_UsesAllCategoriesBeforeLimitingDisplay() => Database(async repo =>
    {
        var (account, _, _) = await Setup(repo);
        for (var i = 0; i < 11; i++)
        {
            var cat = new Category(Guid.NewGuid(), $"Gruppo {i}", TransactionType.Expense, "#123456");
            await repo.SaveCategoryAsync(cat);
            await Add(repo, account, cat, new(2026, 10, 1), 100 + i);
            await Add(repo, account, cat, new(2026, 9, 1), 100 + i);
        }
        var low = new Category(Guid.NewGuid(), "Piccola novità", TransactionType.Expense, "#123456");
        await repo.SaveCategoryAsync(low); await Add(repo, account, low, new(2026, 10, 1), 20);
        var result = await new AssistantChatService(repo).ExecuteAsync("Categorie in aumento",
            new(AssistantChatPeriod.Comparison, AssistantChatOperation.Categories, AssistantChatMeasure.Expenses), "EUR", new(2026, 10, 10));
        Assert.Contains("Other categories", result.Facts);
        Assert.Contains("Largest increase: \"Piccola novità\", 20,00 EUR", result.Facts);
        Assert.True(ReportNumberGuard.UsesProvidedNumbers(result.CalculatedAnswer, result.Facts));
    });

    [Fact] public Task RejectsUnknownCategoryAndUnsafePlan() => Database(async repo =>
    {
        var service = new AssistantChatService(repo);
        var plan = new AssistantChatPlan(AssistantChatPeriod.Current, AssistantChatOperation.Total, AssistantChatMeasure.Expenses, "inesistente");
        await Assert.ThrowsAsync<DomainValidationException>(() => service.ExecuteAsync("Spese", plan, "EUR", DateTime.Now));
        Assert.False(AssistantChatPolicy.ValidPlan(plan with { Operation = (AssistantChatOperation)999 }));
        Assert.False(AssistantChatPolicy.ValidPlan(plan with { Measure = AssistantChatMeasure.Net, Operation = AssistantChatOperation.Largest }));
        Assert.False(AssistantChatPolicy.ValidQuestion(new string('x', 1001)));
    });

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => action(request, token); }

    [Theory]
    [InlineData("controlla I dati dello scorso mese ?", AssistantChatPeriod.Previous)]
    [InlineData("Riassumi il mese scorso", AssistantChatPeriod.Previous)]
    [InlineData("Come è andato il mese passato?", AssistantChatPeriod.Previous)]
    [InlineData("E il mese precedente?", AssistantChatPeriod.Previous)]
    [InlineData("Quanto ho speso questo mese?", AssistantChatPeriod.Current)]
    [InlineData("Dove ho speso di più rispetto al mese scorso?", AssistantChatPeriod.Comparison)]
    [InlineData("Confronta questo mese con il precedente", AssistantChatPeriod.Comparison)]
    [InlineData("How much did I spend last month?", AssistantChatPeriod.Previous)]
    [InlineData("Summarise the previous month", AssistantChatPeriod.Previous)]
    [InlineData("What about this month?", AssistantChatPeriod.Current)]
    [InlineData("Compare this month with the previous one", AssistantChatPeriod.Comparison)]
    [InlineData("Which categories increased most compared with last month?", AssistantChatPeriod.Comparison)]
    public async Task ExplicitMonth_OverridesWrongModelAndPreviousContext(string question, AssistantChatPeriod expected)
    {
        Assert.Equal(expected, AssistantQuestionPeriod.Resolve(question));
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            var payload = await request.Content!.ReadFromJsonAsync<JsonElement>(token);
            var periods = payload.GetProperty("format").GetProperty("properties").GetProperty("period").GetProperty("enum");
            Assert.Equal(1, periods.GetArrayLength()); Assert.Equal(expected.ToString().ToLowerInvariant(), periods[0].GetString());
            var plan = JsonSerializer.Serialize(new { status = "answer", period = "current", operation = "total", measure = "expenses", category = "", comparison = "sameDays" });
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { done = true, done_reason = "stop", message = new { content = plan } }) };
        })) { BaseAddress = new("http://localhost:11434/") };
        var interpretation = await new OllamaGateway(http, "test").InterpretAsync(new(question, new(2026,10,10), "EUR", [],
            new(AssistantChatPeriod.Current, AssistantChatOperation.Total, AssistantChatMeasure.Expenses), "Quanto ho speso questo mese?"), default);
        Assert.Equal(expected, interpretation.Plan!.Period);
    }

    [Fact] public Task PreviousMonth_ActuallyReadsSeptemberDespiteWrongIncomingPlan() => Database(async repo =>
    {
        var (account, expense, _) = await Setup(repo);
        await Add(repo, account, expense, new(2026, 9, 1), 100);
        await Add(repo, account, expense, new(2026, 9, 30), 200);
        await Add(repo, account, expense, new(2026, 10, 10), 207);
        var evidence = await new AssistantChatService(repo).ExecuteAsync("controlla I dati dello scorso mese ?",
            new(AssistantChatPeriod.Current, AssistantChatOperation.Total, AssistantChatMeasure.Expenses), "EUR", new(2026,10,10));
        Assert.Equal(AssistantChatPeriod.Previous, evidence.Plan.Period);
        Assert.Contains("01/09/2026 – 30/09/2026", evidence.Facts);
        Assert.Contains("300,00 EUR", evidence.Facts); Assert.DoesNotContain("207,00 EUR", evidence.Facts);
        var period = Assert.Single(evidence.Periods!); Assert.Equal(2, period.MovementCount); Assert.Equal(2, period.MeasureMovementCount);
        Assert.True(AssistantChatPolicy.ValidEvidence(evidence));
    });

    [Theory]
    [InlineData("Mese corrente: spese 207,00 EUR. Zero movimenti significa assenza di registrazioni, non assenza di spese reali.", 3, true)]
    [InlineData("No transactions recorded.", 3, true)]
    [InlineData("Spese: 207,00 EUR.", 3, false)]
    [InlineData("Nessun movimento registrato nel periodo.", 0, false)]
    public async Task Answer_RejectsFalseAbsenceClaimsUsingActualCounts(string generated, int count, bool fallback)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(new { done = true, done_reason = "stop", message = new { content = generated } }) })))
        { BaseAddress = new("http://localhost:11434/") };
        var amount = count == 0 ? "0,00" : "207,00";
        var evidence = new AssistantChatEvidence("Spese?", new(AssistantChatPeriod.Current, AssistantChatOperation.Total, AssistantChatMeasure.Expenses),
            "EUR", $"Spese: {amount} EUR. {count} movimenti.", $"Spese: {amount} EUR.", [new(new(2026,10,1), new(2026,10,10), count, count)]);
        var answer = await new OllamaGateway(http, "test").AnswerAsync(evidence, default);
        Assert.Equal(fallback, answer.UsedCalculatedAnswer);
        Assert.Equal(fallback ? evidence.CalculatedAnswer : generated, answer.Text);
        Assert.False(AssistantChatPolicy.ValidEvidence(evidence with { Periods = null }));
        Assert.False(AssistantChatPolicy.ValidEvidence(evidence with { Periods = [new(new(2026,10,1), new(2026,10,10), 1, 2)] }));
    }

    [Theory] [InlineData("Hai speso 999 EUR.", "stop")] [InlineData("Risposta troncata", "length")]
    public async Task Gateway_FallsBackToCalculatedAnswer(string text, string reason)
    {
        using var http = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = JsonContent.Create(new { done = true, done_reason = reason, message = new { content = text } }) })))
        { BaseAddress = new("http://localhost:11434/") };
        var evidence = new AssistantChatEvidence("Spese?", new(AssistantChatPeriod.Current, AssistantChatOperation.Total, AssistantChatMeasure.Expenses),
            "EUR", "Spese: 360,00 EUR.", "Hai speso 360,00 EUR.");
        var answer = await new OllamaGateway(http, "test").AnswerAsync(evidence, default);
        Assert.True(answer.UsedCalculatedAnswer); Assert.Equal(evidence.CalculatedAnswer, answer.Text);
    }

    [Theory]
    [InlineData(WebExceptionStatus.Timeout, "did not respond in time")]
    [InlineData(WebExceptionStatus.ConnectFailure, "connection to the assistant was interrupted")]
    [InlineData(WebExceptionStatus.ReceiveFailure, "connection to the assistant was interrupted")]
    public async Task Client_HandlesNativeAndroidNetworkErrors(WebExceptionStatus status, string expected)
    {
        using var http = new HttpClient(new Handler((_, _) => throw new WebException("private connection detail", status)));
        var error = await Assert.ThrowsAsync<DomainValidationException>(() => new AssistantClient(http).AnswerAsync(
            new("http://localhost:5188", "0123456789abcdef0123456789abcdef"),
            new("How much did I spend this month?", new(AssistantChatPeriod.Current, AssistantChatOperation.Total, AssistantChatMeasure.Expenses),
                "EUR", "Expenses: 360,00 EUR.", "Expenses: 360,00 EUR.")));
        Assert.Contains(expected, error.Message);
        Assert.DoesNotContain("private", error.Message);
    }

    [Fact] public Task Chat_RestoresQuestionAfterNativeAndroidAnswerFailure() => Database(async repo =>
    {
        var calls = 0;
        using var http = new HttpClient(new Handler((_, _) =>
        {
            if (++calls == 1)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new AssistantInterpretation(AssistantInterpretationStatus.Answer,
                        new(AssistantChatPeriod.Current, AssistantChatOperation.Total, AssistantChatMeasure.Expenses), "", "test"))
                });
            throw new WebException("private connection detail", WebExceptionStatus.ReceiveFailure);
        }));
        var vm = new AssistantViewModel(new(repo), new(repo), new AssistantClient(http), new Settings(), new(repo));
        await vm.LoadCommand.ExecuteAsync(null);
        vm.Question = "How much did I spend this month?";
        await vm.SendCommand.ExecuteAsync(null);
        Assert.Equal(2, calls);
        Assert.Equal("How much did I spend this month?", vm.Question);
        Assert.Contains("connection to the assistant was interrupted", vm.ErrorMessage);
        Assert.DoesNotContain("private", vm.ErrorMessage);
        Assert.Single(vm.Messages);
        Assert.DoesNotContain(vm.Messages, m => m.IsPending);
        Assert.False(vm.IsBusy); Assert.False(vm.IsAwaitingResponse); Assert.True(vm.CanSend);
    });

    [Fact] public async Task Client_TreatsNativeAndroidCancellationAsCancellation()
    {
        using var cancelled = new CancellationTokenSource();
        using var http = new HttpClient(new Handler((_, _) =>
        {
            cancelled.Cancel();
            throw new WebException("Socket closed", WebExceptionStatus.RequestCanceled);
        }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AssistantClient(http).InterpretAsync(
            new("http://localhost:5188", "0123456789abcdef0123456789abcdef"),
            new("How much did I spend this month?", new(2026,10,10), "EUR", []), cancelled.Token));
    }

    [Fact] public async Task Client_UsesAotJsonForBothChatPhases()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            Assert.Equal("0123456789abcdef0123456789abcdef", request.Headers.GetValues("X-Assistant-Key").Single());
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            calls++;
            if (calls == 1)
            {
                Assert.EndsWith("/chat/interpret", request.RequestUri!.AbsoluteUri);
                Assert.Equal("2026-10-10", json.RootElement.GetProperty("today").GetString());
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new AssistantInterpretation(AssistantInterpretationStatus.Answer,
                    new(AssistantChatPeriod.Previous, AssistantChatOperation.Total, AssistantChatMeasure.Income), "", "test")) };
            }
            Assert.EndsWith("/chat/answer", request.RequestUri!.AbsoluteUri);
            Assert.Equal("2026-09-01", json.RootElement.GetProperty("periods")[0].GetProperty("from").GetString());
            Assert.Equal(0, json.RootElement.GetProperty("periods")[0].GetProperty("measureMovementCount").GetInt32());
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new AssistantChatAnswer("Risposta", "test", true)) };
        }));
        var client = new AssistantClient(http); var connection = new AssistantConnection("http://localhost:5188", "0123456789abcdef0123456789abcdef");
        var interpretation = await client.InterpretAsync(connection, new("Entrate mese scorso", new(2026,10,10), "EUR", []));
        var result = await client.AnswerAsync(connection, new("Entrate mese scorso", interpretation.Plan!, "EUR", "Entrate 0 EUR.", "Entrate 0 EUR.",
            [new(new(2026,9,1), new(2026,9,30), 0, 0)]));
        Assert.Equal(AssistantChatPeriod.Previous, interpretation.Plan!.Period); Assert.True(result.UsedCalculatedAnswer);
    }

    [Theory]
    [InlineData("Quanto ho speso?", false, AssistantInterpretationStatus.Clarify)]
    [InlineData("Quanto ho speso per Cibo?", false, AssistantInterpretationStatus.Answer)]
    [InlineData("E il mese scorso?", true, AssistantInterpretationStatus.Answer)]
    [InlineData("Quanto ho speso questo mese?", true, AssistantInterpretationStatus.Clarify)]
    [InlineData("And last month?", true, AssistantInterpretationStatus.Answer)]
    [InlineData("What about that category?", true, AssistantInterpretationStatus.Answer)]
    [InlineData("How much did I spend this month?", true, AssistantInterpretationStatus.Clarify)]
    public async Task Planner_RequiresAnExplicitCategoryOrGroundedFollowUp(string question, bool context, AssistantInterpretationStatus expected)
    {
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            var payload = await request.Content!.ReadFromJsonAsync<JsonElement>(token);
            Assert.Equal("object", payload.GetProperty("format").GetProperty("type").GetString());
            var choices = payload.GetProperty("format").GetProperty("properties").GetProperty("category").GetProperty("enum");
            Assert.Equal(expected == AssistantInterpretationStatus.Answer ? 2 : 1, choices.GetArrayLength());
            Assert.Equal(0, payload.GetProperty("options").GetProperty("temperature").GetInt32());
            var plan = JsonSerializer.Serialize(new { status = "answer", period = "current", operation = "total", measure = "expenses", category = "Cibo", comparison = "sameDays" });
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { done = true, done_reason = "stop", message = new { content = plan } }) };
        })) { BaseAddress = new("http://localhost:11434/") };
        var result = await new OllamaGateway(http, "test").InterpretAsync(new(question, new(2026,10,10), "EUR", ["Cibo"],
            context ? new(AssistantChatPeriod.Current, AssistantChatOperation.Total, AssistantChatMeasure.Expenses, "Cibo") : null), default);
        Assert.Equal(expected, result.Status);
    }

    private sealed class Settings : IAssistantSettings
    {
        public Task<AssistantConnection> LoadAsync() => Task.FromResult(new AssistantConnection("http://localhost:5188", "0123456789abcdef0123456789abcdef"));
        public Task SaveAsync(AssistantConnection c) => Task.CompletedTask;
    }

    [Theory]
    [InlineData("Compare my spending this month with last month.", AssistantChatComparison.SameDays)]
    [InlineData("Compare with the full previous month.", AssistantChatComparison.FullPreviousMonth)]
    [InlineData("Compare with last month in full.", AssistantChatComparison.FullPreviousMonth)]
    public async Task Comparison_OverridesAnIncorrectModelChoice(string question, AssistantChatComparison expected)
    {
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(token);
            Assert.False(body.GetProperty("think").GetBoolean());
            Assert.Contains("Classify a question", body.GetProperty("messages")[0].GetProperty("content").GetString());
            var choices = body.GetProperty("format").GetProperty("properties").GetProperty("comparison").GetProperty("enum");
            Assert.Equal(1, choices.GetArrayLength());
            Assert.Equal(expected == AssistantChatComparison.SameDays ? "sameDays" : "fullPreviousMonth", choices[0].GetString());
            var wrongChoice = expected == AssistantChatComparison.SameDays ? "fullPreviousMonth" : "sameDays";
            var plan = JsonSerializer.Serialize(new { status = "answer", period = "comparison", operation = "total", measure = "expenses", category = "", comparison = wrongChoice });
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { done = true, done_reason = "stop", message = new { content = plan } }) };
        })) { BaseAddress = new("http://localhost:11434/") };
        var result = await new OllamaGateway(http, "qwen3.5:9b").InterpretAsync(new(question, new(2026,10,10), "EUR", []), default);
        Assert.Equal(expected, result.Plan!.Comparison);
    }

    [Fact] public Task Comparison_LocalExecutionAlsoRejectsAnUnrequestedFullMonth() => Database(async repo =>
    {
        var (account, expense, _) = await Setup(repo);
        await Add(repo, account, expense, new(2026, 9, 1), 100);
        await Add(repo, account, expense, new(2026, 9, 30), 900);
        await Add(repo, account, expense, new(2026, 10, 1), 150);
        var result = await new AssistantChatService(repo).ExecuteAsync("Compare this month with last month",
            new(AssistantChatPeriod.Comparison, AssistantChatOperation.Total, AssistantChatMeasure.Expenses,
                Comparison: AssistantChatComparison.FullPreviousMonth), "EUR", new(2026,10,10));
        Assert.Equal(AssistantChatComparison.SameDays, result.Plan.Comparison);
        Assert.Equal(new DateOnly(2026,9,10), result.Periods![1].Through);
        Assert.Contains("50,00%", result.Facts); Assert.DoesNotContain("1.000,00", result.Facts);
    });

    [Theory] [InlineData("Quante entrate ho registrato questo mese?", true)]
    [InlineData("Quanti movimenti ci sono?", true)] [InlineData("Quanti euro ho speso?", false)]
    [InlineData("How many income transactions this month?", true)]
    [InlineData("How many expenses last month?", true)]
    [InlineData("Number of recorded transactions", true)]
    [InlineData("How much did I spend this month?", false)]
    public async Task Planner_ConstrainsExplicitCountsWithoutTreatingEurosAsMovements(string question, bool count)
    {
        using var http = new HttpClient(new Handler(async (request, token) =>
        {
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(token);
            var choices = body.GetProperty("format").GetProperty("properties").GetProperty("operation").GetProperty("enum");
            Assert.Equal(count ? 1 : 7, choices.GetArrayLength());
            if (count) Assert.Equal("count", choices[0].GetString());
            var plan = JsonSerializer.Serialize(new { status = "answer", period = "current", operation = count ? "count" : "total", measure = "income", category = "", comparison = "sameDays" });
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { done = true, done_reason = "stop", message = new { content = plan } }) };
        })) { BaseAddress = new("http://localhost:11434/") };
        var result = await new OllamaGateway(http, "test").InterpretAsync(new(question, new(2026,10,10), "EUR", []), default);
        Assert.Equal(count ? AssistantChatOperation.Count : AssistantChatOperation.Total, result.Plan!.Operation);
    }

    [Theory] [InlineData("Quanto spenderò a novembre?")] [InlineData("Simula una spesa di 3000 euro.")]
    [InlineData("Forecast my spending next month.")]
    [InlineData("How much will I spend in November?")]
    [InlineData("Simulate a trip to Japan.")]
    public async Task Planner_RejectsExplicitPredictionsWithoutGeneratingHistoricalFacts(string question)
    {
        using var http = new HttpClient(new Handler((_, _) => throw new InvalidOperationException("No inference expected")));
        var result = await new OllamaGateway(http, "test").InterpretAsync(new(question, new(2026,10,10), "EUR", []), default);
        Assert.Equal(AssistantInterpretationStatus.Unsupported, result.Status); Assert.Null(result.Plan);
    }
    private sealed class ChatClient : IAssistantClient
    {
        public List<AssistantQuestionRequest> Questions { get; } = [];
        public bool Hold, Clarify;
        public int Answers;
        public TaskCompletionSource? AnswerGate;
        public bool FailAnswer;
        public Task<AssistantHealth> CheckAsync(AssistantConnection c, CancellationToken token = default) => Task.FromResult(new AssistantHealth("test"));
        public Task<AssistantReportResponse> GenerateAsync(AssistantConnection c, AssistantReportRequest r, CancellationToken token = default) => throw new NotImplementedException();
        public async Task<AssistantInterpretation> InterpretAsync(AssistantConnection c, AssistantQuestionRequest q, CancellationToken token = default)
        {
            Questions.Add(q);
            if (Hold) await Task.Delay(Timeout.Infinite, token);
            return Clarify ? new(AssistantInterpretationStatus.Clarify, null, "Precisare?", "test")
                : new(AssistantInterpretationStatus.Answer, new(AssistantChatPeriod.Current, AssistantChatOperation.Total, AssistantChatMeasure.Expenses), "", "test");
        }
        public async Task<AssistantChatAnswer> AnswerAsync(AssistantConnection c, AssistantChatEvidence e, CancellationToken token = default)
        {
            Answers++;
            if (AnswerGate is not null) await AnswerGate.Task.WaitAsync(token);
            if (FailAnswer) throw new DomainValidationException("Service unavailable.");
            return new AssistantChatAnswer(e.CalculatedAnswer, "test");
        }
    }

    [Fact] public Task Chat_ContextResetAndEvidenceRemainAttachedToEachResponse() => Database(async repo =>
    {
        await repo.SaveAccountsAsync([new Account(Guid.NewGuid(), "Euro", 0, "EUR", isPrimary: true), new Account(Guid.NewGuid(), "Dollari", 0, "USD")]);
        var client = new ChatClient(); var vm = new AssistantViewModel(new(repo), new(repo), client, new Settings(), new(repo));
        await vm.LoadCommand.ExecuteAsync(null);
        vm.Question = "Spese questo mese?"; await vm.SendCommand.ExecuteAsync(null);
        Assert.Null(client.Questions[0].Context);
        Assert.Contains("Currency: EUR", vm.Messages[1].Facts);
        vm.Question = "E il mese scorso?"; await vm.SendCommand.ExecuteAsync(null);
        Assert.NotNull(client.Questions[1].Context);
        Assert.Equal("Spese questo mese?", client.Questions[1].PreviousQuestion);
        vm.SelectedCurrency = "USD"; vm.Question = "Spese?"; await vm.SendCommand.ExecuteAsync(null);
        Assert.Null(client.Questions[2].Context); Assert.Equal("USD", client.Questions[2].Currency);
        Assert.Contains("Currency: EUR", vm.Messages[1].Facts);
        vm.ClearConversationCommand.Execute(null);
        Assert.Empty(vm.Messages);
        vm.Question = "Spese?"; await vm.SendCommand.ExecuteAsync(null);
        Assert.Null(client.Questions[3].Context);
        client.Clarify = true; vm.Question = "Quella cifra?"; await vm.SendCommand.ExecuteAsync(null);
        Assert.False(vm.Messages.Last().HasFacts); Assert.Equal(4, client.Answers);
    });

    [Fact] public Task Chat_CancelRestoresQuestionAndPreventsOverlappingRequests() => Database(async repo =>
    {
        var client = new ChatClient { Hold = true }; var vm = new AssistantViewModel(new(repo), new(repo), client, new Settings(), new(repo));
        await vm.LoadCommand.ExecuteAsync(null); vm.Question = "Quanto ho speso?";
        var pending = vm.SendCommand.ExecuteAsync(null);
        for (var i = 0; i < 100 && client.Questions.Count == 0; i++) await Task.Delay(10);
        Assert.False(vm.CanSend); await vm.SendCommand.ExecuteAsync(null); Assert.Single(client.Questions);
        Assert.True(vm.IsAwaitingResponse);
        Assert.Equal("You", vm.Messages[0].Role);
        Assert.True(vm.Messages[1].IsPending); Assert.False(vm.Messages[1].HasText);
        vm.CancelCommand.Execute(null); await pending;
        Assert.Equal("Quanto ho speso?", vm.Question); Assert.True(vm.CanSend);
        Assert.False(vm.IsBusy); Assert.Equal(0, client.Answers); Assert.Null(vm.ErrorMessage);
        Assert.False(vm.IsAwaitingResponse); Assert.Single(vm.Messages); Assert.DoesNotContain(vm.Messages, m => m.IsPending);
    });

    [Theory] [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public Task Chat_PendingBubbleRemainsDuringAnswerAndIsRemovedOnEveryOutcome(bool cancel, bool fail) => Database(async repo =>
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ChatClient { AnswerGate = gate, FailAnswer = fail };
        var vm = new AssistantViewModel(new(repo), new(repo), client, new Settings(), new(repo));
        await vm.LoadCommand.ExecuteAsync(null);
        vm.Question = "How much did I spend this month?";
        var task = vm.SendCommand.ExecuteAsync(null);
        for (var i = 0; i < 100 && client.Answers == 0; i++) await Task.Delay(10);
        Assert.Equal(1, client.Answers); Assert.True(vm.IsAwaitingResponse);
        Assert.Equal(2, vm.Messages.Count); Assert.True(vm.Messages.Last().IsPending);
        if (cancel) vm.CancelCommand.Execute(null); else gate.SetResult();
        await task;
        Assert.False(vm.IsAwaitingResponse); Assert.False(vm.IsBusy);
        Assert.DoesNotContain(vm.Messages, m => m.IsPending);
        Assert.Equal(cancel || fail ? 1 : 2, vm.Messages.Count);
        if (cancel || fail) Assert.Equal("How much did I spend this month?", vm.Question);
        else { Assert.Equal("Assistant", vm.Messages.Last().Role); Assert.True(vm.Messages.Last().HasFacts); }
        if (fail) Assert.Equal("Service unavailable.", vm.ErrorMessage);
    });
}
