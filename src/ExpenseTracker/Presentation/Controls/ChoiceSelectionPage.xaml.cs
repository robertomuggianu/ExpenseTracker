using System.Collections.ObjectModel;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Presentation.Controls;

public sealed record ChoiceSelectionItem(object Value, string Label, bool IsSelected)
{
    public string Indicator => IsSelected ? "●" : "○";
    public string? IconSource => Value is Category category ? category.IconSource : null;
    public string IconColor => Value is Category category ? category.Color : "#222A3C";
    public bool HasIcon => IconSource is not null;
}

public partial class ChoiceSelectionPage : ContentPage
{
    private static int _openCount;
    private bool closing;
    private readonly TaskCompletionSource<(bool Accepted, object? Value)> _completion = new();

    public static bool IsOpen => Volatile.Read(ref _openCount) > 0;

    private ChoiceSelectionPage(string title, IReadOnlyList<ChoiceSelectionItem> items)
    {
        InitializeComponent();
        BindingContext = new ChoiceSelectionModel(title, items);
        AssistantLauncher.Attach(this);
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

    private void OnItemClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: ChoiceSelectionItem item }) _ = CloseAsync(true, item.Value);
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (BindingContext is not ChoiceSelectionModel model) return;
        model.Items.Clear();
        foreach (var item in model.Source.Where(i => i.Label.Contains(e.NewTextValue ?? "", StringComparison.OrdinalIgnoreCase))) model.Items.Add(item);
    }
    private void OnCancelClicked(object? sender, EventArgs e) => _ = CloseAsync(false, null);

    private async Task CloseAsync(bool accepted, object? value)
    {
        if (closing || _completion.Task.IsCompleted) return;
        closing = true;
        await Shell.Current.Navigation.PopModalAsync(false);
        _completion.TrySetResult((accepted, value));
    }

    public sealed record ChoiceSelectionModel(string Title, IReadOnlyList<ChoiceSelectionItem> Source)
    {
        public ObservableCollection<ChoiceSelectionItem> Items { get; } = new(Source);
        // CollectionView needs a finite height inside the bottom-aligned sheet.
        public double ListHeight => Math.Clamp(Source.Count * 64, 64, 384);
    }
}
