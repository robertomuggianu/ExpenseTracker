using ExpenseTracker.Presentation.ViewModels;
using ExpenseTracker.Presentation.Controls;

namespace ExpenseTracker.Presentation.Views;

public partial class CategoriesPage : ContentPage
{
    private readonly CategoriesViewModel _viewModel;

    public CategoriesPage(CategoriesViewModel viewModel)
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
