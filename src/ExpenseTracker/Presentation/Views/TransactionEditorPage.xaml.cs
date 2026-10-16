using ExpenseTracker.Presentation.ViewModels;
using ExpenseTracker.Presentation.Controls;

namespace ExpenseTracker.Presentation.Views;

public partial class TransactionEditorPage : ContentPage, IQueryAttributable
{
    private readonly TransactionEditorViewModel _viewModel;
    private bool _queryApplied;
    private bool _resumeAfterAssistant;
    private bool _resumeAfterChoice;

    public TransactionEditorPage(TransactionEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _queryApplied = true;
        _viewModel.ReturnToHome = query.TryGetValue("returnToHome", out var origin) && origin?.ToString() == "true";
        var transactionId = query.TryGetValue("transactionId", out var value)
            && Guid.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : (Guid?)null;
        var duplicate = query.TryGetValue("duplicate", out var flag) && flag?.ToString() == "true";
        _ = _viewModel.LoadAsync(transactionId, duplicate);
    }

    protected override void OnDisappearing()
    {
        _resumeAfterChoice = ChoiceSelectionPage.IsOpen;
        _resumeAfterAssistant = Shell.Current is AppShell { IsAssistantOpen: true };
        _viewModel.StoreDraft();
        base.OnDisappearing();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_resumeAfterChoice) { _resumeAfterChoice = false; return; }
        if (_resumeAfterAssistant) { _resumeAfterAssistant = false; return; }
        if (ChoiceSelectionPage.IsOpen || Shell.Current is AppShell { IsAssistantOpen: true }) return;
        if (!_queryApplied)
        {
            await _viewModel.LoadAsync();
        }

        _queryApplied = false;
    }
}
