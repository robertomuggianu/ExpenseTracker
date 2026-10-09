using ExpenseTracker.Presentation.ViewModels;
using ExpenseTracker.Presentation.Controls;

namespace ExpenseTracker.Presentation.Views;

public partial class TransactionEditorPage : ContentPage, IQueryAttributable
{
    private readonly TransactionEditorViewModel _viewModel;
    private bool _queryApplied;

    public TransactionEditorPage(TransactionEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _queryApplied = true;
        var transactionId = query.TryGetValue("transactionId", out var value)
            && Guid.TryParse(value?.ToString(), out var parsed)
            ? parsed
            : (Guid?)null;
        var duplicate = query.TryGetValue("duplicate", out var flag) && flag?.ToString() == "true";
        _ = _viewModel.LoadAsync(transactionId, duplicate);
    }

    protected override void OnDisappearing() { _viewModel.StoreDraft(); base.OnDisappearing(); }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (ChoiceSelectionPage.IsOpen) return;
        if (!_queryApplied)
        {
            await _viewModel.LoadAsync();
        }

        _queryApplied = false;
    }
}
