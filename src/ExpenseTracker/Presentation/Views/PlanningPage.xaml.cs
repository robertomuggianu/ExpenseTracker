using ExpenseTracker.Presentation.Controls;
using ExpenseTracker.Presentation.ViewModels;
namespace ExpenseTracker.Presentation.Views;
public partial class PlanningPage : ContentPage
{
    private readonly PlanningViewModel viewModel;
    public PlanningPage(PlanningViewModel model) { InitializeComponent(); BindingContext = viewModel = model; }
    protected override async void OnAppearing() { base.OnAppearing(); if (!ChoiceSelectionPage.IsOpen) await viewModel.LoadCommand.ExecuteAsync(null); }
}
