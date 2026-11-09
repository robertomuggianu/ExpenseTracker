using ExpenseTracker.Presentation.ViewModels;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Presentation.Views;

public partial class TransactionEditorPage : ContentPage
{
    private readonly TransactionEditorViewModel _viewModel;

    public TransactionEditorPage(TransactionEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    private void OnQuickCategoryRowSizeChanged(object? sender, EventArgs e)
    {
        if (sender is VisualElement { Width: > 0 } row && BindingContext is TransactionEditorViewModel viewModel)
            viewModel.QuickCategoryCapacity = Math.Max(1, (int)(row.Width / 76));
    }

    internal Task OpenAsync(Guid? transactionId, bool duplicate, bool returnToHome, TransactionType? initialType)
    {
        _viewModel.ReturnToHome = returnToHome;
        return _viewModel.LoadAsync(transactionId, duplicate, initialType);
    }

    protected override void OnDisappearing()
    {
        _viewModel.StoreDraft();
        base.OnDisappearing();
    }
}
