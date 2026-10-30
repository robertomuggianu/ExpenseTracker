namespace ExpenseTracker.Presentation.Controls;

public partial class DashboardSummary : ContentView
{
    public static readonly BindableProperty IsCompactProperty = BindableProperty.Create(
        nameof(IsCompact), typeof(bool), typeof(DashboardSummary), false,
        propertyChanged: (bindable, _, _) => ((DashboardSummary)bindable).OnPropertyChanged(nameof(IsExpanded)));
    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }
    public bool IsExpanded => !IsCompact;

    public DashboardSummary() => InitializeComponent();
}
