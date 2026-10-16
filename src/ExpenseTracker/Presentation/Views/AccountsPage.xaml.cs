using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Presentation.Views;

public partial class AccountsPage : ContentPage
{
    private readonly AccountsViewModel _viewModel;

    public AccountsPage(AccountsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Shell.Current is AppShell { IsAssistantOpen: true }) return;
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_viewModel.IsEditorVisible) return base.OnBackButtonPressed();
        if (!_viewModel.IsBusy) _viewModel.CancelEditCommand.Execute(null);
        return true;
    }
}
