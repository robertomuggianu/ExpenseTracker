using System.Collections.ObjectModel;
using ExpenseTracker.Domain;
using ExpenseTracker.Application;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Presentation.Controls;

public sealed record ChoiceSelectionItem(object Value, string Label, bool IsSelected)
{
    public string SelectionHint => IsSelected ? "Selezionato" : "Tocca per scegliere";
    public string? IconSource => Value switch
    {
        Category category => category.IconSource,
        CategoryIcon icon => icon.ImageSource,
        AccountSummary account => account.Account.IconSource,
        AccountFilterOption account => account.IconSource,
        _ => null
    };
    public string IconColor => Value is Category category ? category.Color : "#222A3C";
    public bool HasIcon => IconSource is not null;
    public string? Subtitle => Value switch
    {
        AccountSummary account => AccountSubtitle(account.Account.Currency, account.Account.IsPrimary),
        AccountFilterOption { Id: null } => "Vista complessiva",
        AccountFilterOption account => AccountSubtitle(account.Currency, account.IsPrimary),
        _ => null
    };
    public bool HasSubtitle => !string.IsNullOrEmpty(Subtitle);
    public double RowHeight => HasSubtitle ? 76 : 58;
    private static string? AccountSubtitle(string? currency, bool primary) => primary ? $"{currency} · Principale" : currency;
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
    }

    private void OnViewportSizeChanged(object? sender, EventArgs e)
    {
        if (sender is not VisualElement { Height: > 0 } viewport || BindingContext is not ChoiceSelectionModel model) return;
        // Keep the search field and dismissal action reachable on short phones and in landscape.
        ChoiceSheet.MaximumHeightRequest = Math.Min(660, viewport.Height - 28);
        ChoicesList.HeightRequest = Math.Min(model.ListHeight, Math.Max(64, viewport.Height - 260));
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
        public double ListHeight => Math.Clamp(Source.Sum(item => item.RowHeight + 6), 64, 384);
    }
}
