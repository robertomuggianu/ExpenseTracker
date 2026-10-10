using ExpenseTracker.Presentation.ViewModels;
using ExpenseTracker.Presentation.Controls;

namespace ExpenseTracker.Presentation.Views;

public partial class TransactionsPage : ContentPage
{
    private Guid? firstVisibleId;
    private readonly TransactionsViewModel _viewModel;

    public TransactionsPage(TransactionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    private void OnListScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        if (e.FirstVisibleItemIndex >= 0 && e.FirstVisibleItemIndex < _viewModel.Transactions.Count)
            firstVisibleId = _viewModel.Transactions[e.FirstVisibleItemIndex].Transaction.Id;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (ChoiceSelectionPage.IsOpen || Shell.Current is AppShell { IsAssistantOpen: true }) return;
        var restoreId = firstVisibleId;
        var previousFilter = _viewModel.FilterSummary;
        await _viewModel.LoadCommand.ExecuteAsync(null);
        if (previousFilter == _viewModel.FilterSummary && restoreId.HasValue)
        {
            var item = _viewModel.Transactions.FirstOrDefault(t => t.Transaction.Id == restoreId);
            var group = _viewModel.Groups.FirstOrDefault(g => item is not null && g.Contains(item));
            if (item is not null && group is not null)
                Dispatcher.Dispatch(() => TransactionsList.ScrollTo(item, group, ScrollToPosition.Start, false));
        }
    }
}
