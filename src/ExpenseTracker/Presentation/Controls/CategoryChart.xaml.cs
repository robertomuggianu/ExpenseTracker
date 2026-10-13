namespace ExpenseTracker.Presentation.Controls;
public partial class CategoryChart : ContentView
{
    public static readonly BindableProperty PointCommandProperty = BindableProperty.Create(
        nameof(PointCommand), typeof(System.Windows.Input.ICommand), typeof(CategoryChart));
    public System.Windows.Input.ICommand? PointCommand
    {
        get => (System.Windows.Input.ICommand?)GetValue(PointCommandProperty);
        set => SetValue(PointCommandProperty, value);
    }
    public CategoryChart() => InitializeComponent();
}
