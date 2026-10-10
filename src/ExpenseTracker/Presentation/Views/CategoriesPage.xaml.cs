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
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(CategoriesViewModel.IsEditorVisible) && _viewModel.IsEditorVisible)
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), ScrollToEditor);
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (ChoiceSelectionPage.IsOpen || Shell.Current is AppShell { IsAssistantOpen: true }) return;
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnEditCategory(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not ExpenseTracker.Domain.Category category) return;
        _viewModel.EditCategoryCommand.Execute(category);
        await CategoryScroll.ScrollToAsync(CategoryEditor, ScrollToPosition.Start, true);
    }

    private async void ScrollToEditor() => await CategoryScroll.ScrollToAsync(CategoryEditor, ScrollToPosition.Start, true);
}
