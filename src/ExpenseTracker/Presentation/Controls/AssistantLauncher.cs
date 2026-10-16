namespace ExpenseTracker.Presentation.Controls;

/// <summary>Shared floating actions layered over the full page viewport.</summary>
public static class AssistantLauncher
{
    public static readonly BindableProperty IsVisibleProperty = BindableProperty.CreateAttached(
        "IsVisible", typeof(bool), typeof(AssistantLauncher), true,
        propertyChanged: (bindable, _, value) =>
        {
            if (bindable is ContentPage { Content: Grid layout })
                foreach (var action in layout.Children.OfType<ImageButton>().Where(button => button.StyleId == "AssistantLauncher"))
                    action.IsVisible = (bool)value;
        });

    public static bool GetIsVisible(BindableObject target) => (bool)target.GetValue(IsVisibleProperty);
    public static void SetIsVisible(BindableObject target, bool value) => target.SetValue(IsVisibleProperty, value);

    public static void Attach(ContentPage page)
    {
        // A selector displayed inside the chat is already in the assistant context.
        if (Shell.Current is AppShell { IsAssistantOpen: true }) return;
        var content = page.Content;
        page.Content = null;
        var layout = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star) } };
        layout.Add(content);
        var button = new ImageButton
        {
            StyleId = "AssistantLauncher", IsVisible = GetIsVisible(page),
            Source = "ui_assistant.png", WidthRequest = 54, HeightRequest = 54,
            Padding = 13, CornerRadius = 27, BackgroundColor = Color.FromArgb("#42C6A5"),
            HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(18, 0, 0, 24), ZIndex = 10
        };
        SemanticProperties.SetDescription(button, "Apri assistente AI");
        button.Clicked += async (_, _) =>
        {
            button.IsEnabled = false;
            try { if (Shell.Current is AppShell shell) await shell.OpenAssistantAsync(); }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine(exception);
                await page.DisplayAlertAsync("Assistente", "Impossibile aprire la chat. Riprova.", "OK");
            }
            finally { button.IsEnabled = true; }
        };
        // Only the buttons intercept taps; there is no footer or full-screen action layer.
        layout.Add(button);
        if (content is Grid grid && grid.Children.OfType<Button>().FirstOrDefault(b => SemanticProperties.GetDescription(b) == "Aggiungi un movimento") is Button add)
        {
            grid.Children.Remove(add);
            Grid.SetRow(add, 0);
            Grid.SetColumn(add, 0);
            add.HorizontalOptions = LayoutOptions.End;
            add.VerticalOptions = LayoutOptions.End;
            add.Margin = new Thickness(0, 0, 18, 24);
            add.ZIndex = 10;
            layout.Add(add);
        }
        page.Content = layout;
    }
}
