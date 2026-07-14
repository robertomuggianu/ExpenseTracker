using ExpenseTracker.Presentation.ViewModels;

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
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
