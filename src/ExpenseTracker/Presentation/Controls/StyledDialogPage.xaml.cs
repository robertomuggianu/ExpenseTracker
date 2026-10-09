namespace ExpenseTracker.Presentation.Controls;

public partial class StyledDialogPage : ContentPage
{
    private bool closing;
    private readonly TaskCompletionSource<bool> _completion = new();

    private StyledDialogPage(string title, string message, string accept, string? cancel)
    {
        InitializeComponent();
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        AcceptButton.Text = accept;
        CancelButton.IsVisible = !string.IsNullOrWhiteSpace(cancel);
        CancelButton.Text = cancel ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cancel))
        {
            Grid.SetColumn(AcceptButton, 0);
            Grid.SetColumnSpan(AcceptButton, 2);
        }
    }

    public static async Task<bool> ShowAsync(string title, string message, string accept, string? cancel = null)
    {
        var page = new StyledDialogPage(title, message, accept, cancel);
        await Shell.Current.Navigation.PushModalAsync(page, false);
        return await page._completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(false);
        return true;
    }

    private void OnAcceptClicked(object? sender, EventArgs e) => _ = CloseAsync(true);
    private void OnCancelClicked(object? sender, EventArgs e) => _ = CloseAsync(false);

    private async Task CloseAsync(bool result)
    {
        if (_completion.Task.IsCompleted) return;
        if (closing) return;
        closing = true;
        await Shell.Current.Navigation.PopModalAsync(false);
        _completion.TrySetResult(result);
    }
}
