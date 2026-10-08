using ExpenseTracker.Presentation.ViewModels;
using ExpenseTracker.Presentation.Controls;

namespace ExpenseTracker.Presentation.Views;

public partial class TransactionsPage : ContentPage
{
    private readonly TransactionsViewModel _viewModel;

    public TransactionsPage(TransactionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (ChoiceSelectionPage.IsOpen) return;
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
