namespace ExpenseTracker.Presentation.Views;
public partial class MorePage : ContentPage
{
    public MorePage() => InitializeComponent();
    private async void OnPlanning(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//PlanningRoot/Planning");
    private async void OnCategories(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//CategoriesRoot/Categories");
    private async void OnAccounts(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//AccountsRoot/Accounts");
    private async void OnExport(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//ExportRoot/Export");
}
