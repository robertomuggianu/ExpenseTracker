namespace ExpenseTracker.Presentation.Controls;

public partial class PageHeader : ContentView
{
    public static readonly BindableProperty HeadingProperty = BindableProperty.Create(nameof(Heading), typeof(string), typeof(PageHeader), string.Empty);
    public static readonly BindableProperty ShowsBackProperty = BindableProperty.Create(nameof(ShowsBack), typeof(bool), typeof(PageHeader), false);
    public string Heading { get => (string)GetValue(HeadingProperty); set => SetValue(HeadingProperty, value); }
    public bool ShowsBack { get => (bool)GetValue(ShowsBackProperty); set => SetValue(ShowsBackProperty, value); }
    public string MenuText => ShowsBack ? "‹" : "≡";
    public string MenuDescription => ShowsBack ? "Chiudi assistente e torna alla pagina precedente" : "Apri menu";
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(ShowsBack)) { OnPropertyChanged(nameof(MenuText)); OnPropertyChanged(nameof(MenuDescription)); }
    }
    public PageHeader() => InitializeComponent();
    private async void OnMenu(object? sender, EventArgs e)
    {
        if (ShowsBack && Shell.Current is AppShell shell) await shell.CloseAssistantAsync();
        else Shell.Current.FlyoutIsPresented = true;
    }
}
