namespace ExpenseTracker.Presentation.Views;
public partial class MorePage : ContentPage
{
    public MorePage() => InitializeComponent();
    private async void OnAssistant(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//Main/Assistant");
    private async void OnPlanning(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//Main/Planning");
    private async void OnCategories(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//Main/Categories");
    private async void OnAccounts(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//Main/Accounts");
    private async void OnExport(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//Main/Export");
}
