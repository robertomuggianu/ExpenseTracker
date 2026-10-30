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

    private async void OnCategoryTileTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not CategoryTile tile) return;
        if (tile.Category is { } category) _viewModel.EditCategoryCommand.Execute(category);
        else _viewModel.NewCategoryCommand.Execute(null);
        await CategoryScroll.ScrollToAsync(CategoryEditor, ScrollToPosition.Start, true);
    }

    private async void ScrollToEditor() => await CategoryScroll.ScrollToAsync(CategoryEditor, ScrollToPosition.Start, true);

    private void OnCategoryTileSizeChanged(object? sender, EventArgs e)
    {
        // FlexLayout measures percentage-width items before assigning their width.
        // Remeasure the label at its actual width so longer names can wrap.
        if (sender is Grid tile && tile.Width > tile.Padding.HorizontalThickness
            && tile.Children.OfType<Label>().FirstOrDefault() is { } name)
            name.WidthRequest = tile.Width - tile.Padding.HorizontalThickness;
    }
}
