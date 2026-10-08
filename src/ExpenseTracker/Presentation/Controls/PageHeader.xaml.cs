namespace ExpenseTracker.Presentation.Controls;

public partial class PageHeader : ContentView
{
    public static readonly BindableProperty HeadingProperty = BindableProperty.Create(nameof(Heading), typeof(string), typeof(PageHeader), string.Empty);
    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(nameof(Subtitle), typeof(string), typeof(PageHeader), string.Empty);
    public string Heading { get => (string)GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public PageHeader() => InitializeComponent();
    private void OnMenu(object? sender, EventArgs e) => Shell.Current.FlyoutIsPresented = true;
}
