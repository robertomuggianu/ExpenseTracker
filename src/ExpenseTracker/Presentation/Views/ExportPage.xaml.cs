using ExpenseTracker.Presentation.ViewModels;
using ExpenseTracker.Presentation.Controls;

namespace ExpenseTracker.Presentation.Views;

public partial class ExportPage : ContentPage
{
    private readonly ExportViewModel _viewModel;

    public ExportPage(ExportViewModel viewModel)
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
