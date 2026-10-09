using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.Services;

namespace ExpenseTracker.Presentation.ViewModels;

public sealed partial class AssistantViewModel(AssistantReportService reports, AccountService accounts,
    IAssistantClient client, IAssistantSettings settings) : ViewModelBase
{
    private CancellationTokenSource? pending;
    private bool settingsLoaded;
    public ObservableCollection<string> Currencies { get; } = [];
    public ObservableCollection<AssistantMessage> Messages { get; } = [];
    public bool CanInteract => !IsBusy;
    [ObservableProperty] public partial string Address { get; set; } = "";
    [ObservableProperty] public partial string AccessKey { get; set; } = "";
    [ObservableProperty] public partial string SelectedCurrency { get; set; } = "EUR";
    [ObservableProperty] public partial string StatusMessage { get; set; } = "Configura il collegamento al PC per iniziare.";

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy)) OnPropertyChanged(nameof(CanInteract));
    }

    [RelayCommand]
    private Task LoadAsync() => ExecuteAsync(async token =>
    {
        if (!settingsLoaded)
        {
            var connection = await settings.LoadAsync();
            Address = connection.Address; AccessKey = connection.AccessKey; settingsLoaded = true;
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
        StatusMessage = "Verifica della connessione…";
        var connection = Connection();
        await settings.SaveAsync(connection);
        var health = await client.CheckAsync(connection, token);
        StatusMessage = $"Connesso · {health.Model}. Impostazioni salvate.";
    });

    [RelayCommand] private Task SummarizeMonthAsync() => GenerateAsync(AssistantReportKind.MonthlySummary);
    [RelayCommand] private Task CompareMonthsAsync() => GenerateAsync(AssistantReportKind.MonthComparison);
    [RelayCommand] private void Cancel() => pending?.Cancel();
    [RelayCommand] private void ClearConversation() { if (!IsBusy) Messages.Clear(); }
    public void CancelPending() => pending?.Cancel();

    private Task GenerateAsync(AssistantReportKind kind) => ExecuteAsync(async token =>
    {
        var connection = Connection();
        if (!Currencies.Contains(SelectedCurrency)) throw new DomainValidationException("Seleziona una valuta disponibile.");
        await settings.SaveAsync(connection);
        var report = await reports.CreateAsync(kind, SelectedCurrency, DateTime.Now, token);
        if (report.Current.MovementCount == 0 && (report.Previous is null || report.Previous.MovementCount == 0))
        {
            Add(new("Assistente", "Non ci sono movimenti registrati nei periodi selezionati. Aggiungi o importa i movimenti per preparare un resoconto.", Facts(report), ""));
            StatusMessage = "Nessun dato da inviare."; return;
        }
        Add(new("Tu", kind == AssistantReportKind.MonthComparison ? "Confronta questo mese con il precedente." : "Riassumi questo mese.", "", ""));
        StatusMessage = "Preparazione del resoconto…";
        var response = await client.GenerateAsync(connection, report, token);
        token.ThrowIfCancellationRequested();
        Add(new("Assistente", response.Text, Facts(report), $"{response.Model} · {DateTime.Now:HH:mm}"));
        StatusMessage = "Resoconto pronto. Verifica i dati riportati sotto la risposta.";
    });

    private AssistantConnection Connection() => new(Address.Trim(), AccessKey.Trim());
    private void Add(AssistantMessage message)
    {
        Messages.Add(message);
        while (Messages.Count > 20) Messages.RemoveAt(0);
    }
    private async Task ExecuteAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; ErrorMessage = null;
        using var source = new CancellationTokenSource(); pending = source;
        try { await action(source.Token); }
        catch (OperationCanceledException) { StatusMessage = "Richiesta annullata."; }
        catch (DomainValidationException e) { ErrorMessage = e.Message; StatusMessage = "Operazione non completata."; }
        catch (Exception) { ErrorMessage = "Impossibile completare l'operazione. Riprova."; StatusMessage = "Operazione non completata."; }
        finally { pending = null; IsBusy = false; }
    }

    private static string Facts(AssistantReportRequest report)
    {
        var culture = CultureInfo.GetCultureInfo("it-IT");
        string Money(decimal amount) => $"{amount.ToString("N2", culture)} {report.Currency}";
        string Period(AssistantPeriod p) => $"{p.From:dd/MM/yyyy} – {p.Through:dd/MM/yyyy}: entrate {Money(p.Income)}, spese {Money(p.Expense)}, differenza entrate/spese {Money(p.Net)} · {p.MovementCount} movimenti.";
        var text = "Dati calcolati dall’app\n" + Period(report.Current);
        if (report.Previous is { } previous)
        {
            text += "\n" + Period(previous) + "\nVariazione spese: " + Money(report.ExpenseChange!.Value)
                + (report.ExpenseChangePercent is { } percent ? $" ({percent.ToString("N2", culture)}%)." : ". Percentuale non calcolabile: spese precedenti pari a zero.");
        }
        if (report.Current.Categories.Count > 0)
            text += "\nCategorie del periodo corrente: " + string.Join("; ", report.Current.Categories.Select(c => $"{c.Name}: {Money(c.Expense)}"));
        return text + "\nTrasferimenti esclusi. Periodi fino al giorno indicato incluso.";
    }
}

public sealed record AssistantMessage(string Role, string Text, string Facts, string Model)
{
    public bool HasFacts => !string.IsNullOrEmpty(Facts);
    public bool HasModel => !string.IsNullOrEmpty(Model);
}
