using System.Collections.Specialized;
using ExpenseTracker.Presentation.Controls;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Presentation.Views;

public partial class AssistantPage : ContentPage
{
    private readonly AssistantViewModel viewModel;
    public AssistantPage(AssistantViewModel model)
    {
        InitializeComponent(); BindingContext = viewModel = model;
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        viewModel.Messages.CollectionChanged -= OnMessagesChanged;
        viewModel.Messages.CollectionChanged += OnMessagesChanged;
        if (!ChoiceSelectionPage.IsOpen) await viewModel.LoadCommand.ExecuteAsync(null);
    }
    protected override void OnDisappearing()
    {
        viewModel.Messages.CollectionChanged -= OnMessagesChanged;
        if (!ChoiceSelectionPage.IsOpen) viewModel.CancelPending();
        base.OnDisappearing();
    }
    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace)
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), async () =>
            {
                if (MessagesLayout.Children.LastOrDefault() is View message)
                    await ConversationScroll.ScrollToAsync(message, ScrollToPosition.Start, true);
            });
    }
}
