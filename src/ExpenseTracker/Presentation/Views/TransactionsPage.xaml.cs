using ExpenseTracker.Presentation.ViewModels;

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
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
