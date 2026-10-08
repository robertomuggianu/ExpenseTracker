using System.Collections.ObjectModel;

namespace ExpenseTracker.Presentation.Controls;

public sealed record ChoiceSelectionItem(object Value, string Label, bool IsSelected)
{
    public string Indicator => IsSelected ? "●" : "○";
}

public partial class ChoiceSelectionPage : ContentPage
{
    private static int _openCount;
    private readonly TaskCompletionSource<(bool Accepted, object? Value)> _completion = new();

    public static bool IsOpen => Volatile.Read(ref _openCount) > 0;

    private ChoiceSelectionPage(string title, IReadOnlyList<ChoiceSelectionItem> items)
    {
        InitializeComponent();
        BindingContext = new ChoiceSelectionModel(title, items);
    }

    public static async Task<(bool Accepted, object? Value)> ShowAsync(string title, IReadOnlyList<ChoiceSelectionItem> items)
    {
        var page = new ChoiceSelectionPage(title, items);
        Interlocked.Increment(ref _openCount);
        try
        {
            await Shell.Current.Navigation.PushModalAsync(page, false);
            return await page._completion.Task;
        }
        finally
        {
            Interlocked.Decrement(ref _openCount);
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(false, null);
        return true;
    }

    private void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is ChoiceSelectionItem item) _ = CloseAsync(true, item.Value);
    }

    private void OnCancelClicked(object? sender, EventArgs e) => _ = CloseAsync(false, null);

    private async Task CloseAsync(bool accepted, object? value)
    {
        if (_completion.Task.IsCompleted) return;
        await Shell.Current.Navigation.PopModalAsync(false);
        _completion.TrySetResult((accepted, value));
    }

    private sealed record ChoiceSelectionModel(string Title, IReadOnlyList<ChoiceSelectionItem> Source)
    {
        public ObservableCollection<ChoiceSelectionItem> Items { get; } = new(Source);
    }
}
