using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.Services;
using Microsoft.Extensions.Logging;

namespace ExpenseTracker.Presentation.ViewModels;

public sealed partial class AssistantViewModel(AssistantReportService reports, AccountService accounts,
    IAssistantClient client, IAssistantSettings settings, AssistantChatService chat, ILogger<AssistantViewModel>? logger = null) : ViewModelBase
{
    private string requestStage = "operation";
    private CancellationTokenSource? pending;
    private AssistantMessage? awaitingMessage;
    private bool settingsLoaded;
    private AssistantChatPlan? context;
    private string? previousQuestion;
    private DateOnly? contextMonth;
    public ObservableCollection<string> Currencies { get; } = [];
    public ObservableCollection<AssistantMessage> Messages { get; } = [];
    public bool CanInteract => !IsBusy;
    public bool CanSend => !IsBusy && AssistantChatPolicy.ValidQuestion(Question.Trim());
    [ObservableProperty] public partial string Question { get; set; } = "";
    [ObservableProperty] public partial bool IsAwaitingResponse { get; set; }
    [ObservableProperty] public partial bool ConnectionSettingsVisible { get; set; } = true;
    [ObservableProperty] public partial string Address { get; set; } = "";
    [ObservableProperty] public partial string AccessKey { get; set; } = "";
    [ObservableProperty] public partial string SelectedCurrency { get; set; } = "EUR";
    [ObservableProperty] public partial string StatusMessage { get; set; } = "Connect to your PC to get started.";

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy)) OnPropertyChanged(nameof(CanInteract));
        if (e.PropertyName is nameof(IsBusy) or nameof(Question))
        {
            OnPropertyChanged(nameof(CanSend));
            SendCommand.NotifyCanExecuteChanged();
        }
        if (e.PropertyName == nameof(SelectedCurrency)) ResetContext();
    }

    [RelayCommand]
    private Task LoadAsync() => ExecuteAsync(async token =>
    {
        if (!settingsLoaded)
        {
            var connection = await settings.LoadAsync();
            Address = connection.Address; AccessKey = connection.AccessKey; settingsLoaded = true;
            ConnectionSettingsVisible = string.IsNullOrWhiteSpace(Address) || string.IsNullOrWhiteSpace(AccessKey);
            if (!ConnectionSettingsVisible) StatusMessage = "Ask about this month or last month. Your PC must be running.";
        }
        var available = await accounts.GetAccountsAsync(token);
        var selected = SelectedCurrency;
        var codes = available.Select(a => a.Account.Currency).Distinct().Order().ToArray();
        Currencies.Clear(); foreach (var code in codes.DefaultIfEmpty("EUR")) Currencies.Add(code);
        SelectedCurrency = Currencies.Contains(selected) ? selected : available.FirstOrDefault(a => a.Account.IsPrimary)?.Account.Currency ?? Currencies[0];
    });

    [RelayCommand]
    private Task CheckConnectionAsync() => ExecuteAsync(async token =>
    {
        StatusMessage = "Checking connection…";
        var connection = Connection();
        await settings.SaveAsync(connection);
        var health = await client.CheckAsync(connection, token);
        StatusMessage = $"Connected · {health.Model}. Settings saved.";
        ConnectionSettingsVisible = false;
    });

    [RelayCommand] private Task SummarizeMonthAsync() => GenerateAsync(AssistantReportKind.MonthlySummary);
    [RelayCommand] private Task CompareMonthsAsync() => GenerateAsync(AssistantReportKind.MonthComparison);
    [RelayCommand] private void Cancel() => pending?.Cancel();
    [RelayCommand] private void ClearConversation()
    { if (!IsBusy) { Messages.Clear(); ResetContext(); Question = ""; } }
    [RelayCommand] private void ToggleConnectionSettings() => ConnectionSettingsVisible = !ConnectionSettingsVisible;
    [RelayCommand] private void SuggestQuestion(string question) { if (!IsBusy) Question = question; }
    public void CancelPending() => pending?.Cancel();

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task SendAsync() => ExecuteAsync(async token =>
    {
        var question = Question.Trim();
        var connection = Connection();
        connection.GetBaseUri();
        var currency = SelectedCurrency;
        if (!Currencies.Contains(currency)) throw new DomainValidationException("Select an available currency.");
        var today = DateTime.Now;
        var month = new DateOnly(today.Year, today.Month, 1);
        if (contextMonth is { } priorMonth && priorMonth != month) ResetContext();
        BeginResponse(question);
        Question = "";
        try
        {
            StatusMessage = "Interpreting your question…";
            requestStage = "read-categories";
            var categories = await chat.GetCategoryNamesAsync(token);
            requestStage = "save-connection";
            await settings.SaveAsync(connection);
            requestStage = "interpret-question";
            var interpretation = await client.InterpretAsync(connection,
                new(question, DateOnly.FromDateTime(today), currency, categories, context, previousQuestion), token);
            token.ThrowIfCancellationRequested();
            if (interpretation.Status != AssistantInterpretationStatus.Answer)
            {
                if (interpretation.Status == AssistantInterpretationStatus.Clarify) { previousQuestion = question; contextMonth = month; }
                Add(new("Assistant", interpretation.Message, "", interpretation.Model));
                StatusMessage = "You can clarify your question or ask about another figure from the two months.";
                return;
            }
            if (!AssistantChatPolicy.ValidPlan(interpretation.Plan))
                throw new DomainValidationException("Invalid interpretation. Please clarify your question.");
            StatusMessage = "Calculating the figures and preparing your answer…";
            requestStage = "calculate-evidence";
            var evidence = await chat.ExecuteAsync(question, interpretation.Plan!, currency, today, token);
            requestStage = "generate-answer";
            var answer = await client.AnswerAsync(connection, evidence, token);
            token.ThrowIfCancellationRequested();
            Add(new("Assistant", answer.Text, "Data used · calculated by the app\n" + evidence.Facts,
                (answer.UsedCalculatedAnswer ? "Answer calculated by the app" : answer.Model) + $" · {DateTime.Now:HH:mm}"));
            context = evidence.Plan; previousQuestion = question; contextMonth = month;
            StatusMessage = answer.UsedCalculatedAnswer
                ? "The model response could not be verified. Showing the result calculated by the app."
                : "Answer ready. The data used are shown below.";
        }
        catch
        {
            if (string.IsNullOrEmpty(Question)) Question = question;
            throw;
        }
    });

    private Task GenerateAsync(AssistantReportKind kind) => ExecuteAsync(async token =>
    {
        var connection = Connection();
        if (!Currencies.Contains(SelectedCurrency)) throw new DomainValidationException("Select an available currency.");
        await settings.SaveAsync(connection);
        var report = await reports.CreateAsync(kind, SelectedCurrency, DateTime.Now, token);
        if (report.Current.MovementCount == 0 && (report.Previous is null || report.Previous.MovementCount == 0))
        {
            Add(new("Assistant", "No transactions recorded in the selected periods. Add or import transactions to prepare a summary.", Facts(report), ""));
            StatusMessage = "No data to send."; return;
        }
        BeginResponse(kind == AssistantReportKind.MonthComparison ? "Compare this month with last month." : "Summarise this month.");
        StatusMessage = "Preparing your summary…";
        var response = await client.GenerateAsync(connection, report, token);
        token.ThrowIfCancellationRequested();
        Add(new("Assistant", response.Text, Facts(report), $"{response.Model} · {DateTime.Now:HH:mm}"));
        StatusMessage = "Summary ready. Check the data below the answer.";
    });

    private AssistantConnection Connection() => new(Address.Trim(), AccessKey.Trim());
    private void ResetContext() { context = null; previousQuestion = null; contextMonth = null; }
    private void Add(AssistantMessage message)
    {
        if (awaitingMessage is { } placeholder && message.Role == "Assistant")
        {
            var index = Messages.IndexOf(placeholder);
            awaitingMessage = null;
            IsAwaitingResponse = false;
            if (index >= 0) Messages[index] = message;
            else Messages.Add(message);
        }
        else Messages.Add(message);
        while (Messages.Count > 20) Messages.RemoveAt(0);
    }
    private void BeginResponse(string question)
    {
        Add(new("You", question, "", ""));
        var placeholder = new AssistantMessage("Assistant", "", "", "", IsPending: true);
        Add(placeholder);
        awaitingMessage = placeholder;
        IsAwaitingResponse = true;
    }
    private void RemovePendingMessage()
    {
        if (awaitingMessage is { } placeholder) Messages.Remove(placeholder);
        awaitingMessage = null;
        IsAwaitingResponse = false;
    }
    private async Task ExecuteAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null; requestStage = "operation";
        using var source = new CancellationTokenSource(); pending = source;
        try { await action(source.Token); }
        catch (OperationCanceledException) { StatusMessage = "Request cancelled."; }
        catch (DomainValidationException e) { ErrorMessage = e.Message; StatusMessage = "Operation could not be completed."; }
        catch (Exception e)
        {
            // Do not log exception messages or payloads: they may contain financial data or connection details.
            if (logger?.IsEnabled(LogLevel.Error) == true)
                logger.LogError("Unexpected assistant error at {Stage}. Type: {Type}. Stack: {Stack}",
                    requestStage, e.GetType().FullName, e.StackTrace);
            else Console.Error.WriteLine($"Unexpected assistant error at {requestStage}. Type: {e.GetType().FullName}. Stack: {e.StackTrace}");
            ErrorMessage = "Unable to complete the request. Please try again.";
            StatusMessage = "Operation could not be completed.";
        }
        finally { RemovePendingMessage(); pending = null; IsBusy = false; }
    }

    private static string Facts(AssistantReportRequest report)
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");
        string Money(decimal amount) => $"{amount.ToString("N2", culture)} {report.Currency}";
        string Period(AssistantPeriod p) => $"{p.From:dd/MM/yyyy} – {p.Through:dd/MM/yyyy}: income {Money(p.Income)}, expenses {Money(p.Expense)}, income minus expenses {Money(p.Net)} · {p.MovementCount} transactions.";
        var text = "Data calculated by the app\n" + Period(report.Current);
        if (report.Previous is { } previous)
        {
            text += "\n" + Period(previous) + "\nChange in expenses: " + Money(report.ExpenseChange!.Value)
                + (report.ExpenseChangePercent is { } percent ? $" ({percent.ToString("N2", culture)}%)." : ". Percentage change cannot be calculated: previous expenses were zero.");
        }
        if (report.Current.Categories.Count > 0)
            text += "\nCurrent-period categories: " + string.Join("; ", report.Current.Categories.Select(c => $"{c.Name}: {Money(c.Expense)}"));
        return text + "\nTransfers excluded. Period end dates are inclusive.";
    }
}

public sealed record AssistantMessage(string Role, string Text, string Facts, string Model, bool IsPending = false)
{
    public bool HasText => !IsPending && !string.IsNullOrEmpty(Text);
    public bool HasFacts => !string.IsNullOrEmpty(Facts);
    public bool HasModel => !string.IsNullOrEmpty(Model);
}
