namespace ExpenseTracker.Presentation.Views;
public partial class MorePage : ContentPage
{
    public MorePage() => InitializeComponent();
    private async void OnCategories(object? sender, EventArgs e) => await Shell.Current.GoToAsync("categories");
    private async void OnAccounts(object? sender, EventArgs e) => await Shell.Current.GoToAsync("accounts");
    private async void OnExport(object? sender, EventArgs e) => await Shell.Current.GoToAsync("export");
}
