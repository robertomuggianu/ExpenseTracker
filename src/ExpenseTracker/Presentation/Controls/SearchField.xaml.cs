namespace ExpenseTracker.Presentation.Controls;

public partial class SearchField : ContentView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(SearchField), string.Empty, BindingMode.TwoWay,
        propertyChanged: (owner, _, _) => ((SearchField)owner).OnPropertyChanged(nameof(HasText)));
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(SearchField), "Cerca");
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public bool HasText => !string.IsNullOrEmpty(Text);
    public event EventHandler<TextChangedEventArgs>? TextChanged;
    public SearchField()
    {
        InitializeComponent();
        SearchEntry.HandlerChanged += (_, _) => RemoveNativeChrome();
        RemoveNativeChrome();
    }
    private void RemoveNativeChrome()
    {
#if ANDROID
        if (SearchEntry.Handler?.PlatformView is Android.Widget.EditText input) input.Background = null;
#elif IOS || MACCATALYST
        if (SearchEntry.Handler?.PlatformView is UIKit.UITextField input)
        {
            input.BorderStyle = UIKit.UITextBorderStyle.None;
            input.BackgroundColor = UIKit.UIColor.Clear;
        }
#elif WINDOWS
        if (SearchEntry.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox input)
            input.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
#endif
    }
    private void OnTextChanged(object? sender, TextChangedEventArgs e) => TextChanged?.Invoke(this, e);
    private void OnClear(object? sender, EventArgs e) => Text = string.Empty;
}
