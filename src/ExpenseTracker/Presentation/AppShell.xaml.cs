using ExpenseTracker.Application;
using ExpenseTracker.Presentation.Services;
using ExpenseTracker.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Presentation;

public partial class AppShell : Shell
{
    private readonly AccountService accountService;
    private string menuBalance = "I tuoi conti, sempre a portata di mano";
    public string MenuBalance { get => menuBalance; private set { menuBalance = value; OnPropertyChanged(nameof(MenuBalance)); } }

    public AppShell(IServiceProvider services)
    {
        accountService = services.GetRequiredService<AccountService>();
        InitializeComponent();
        DashboardContent.ContentTemplate = Resolve<DashboardPage>(services);
        TransactionsContent.ContentTemplate = Resolve<TransactionsPage>(services);
        NewTransactionContent.ContentTemplate = Resolve<TransactionEditorPage>(services);
        StatisticsContent.ContentTemplate = Resolve<StatisticsPage>(services);
        PlanningContent.ContentTemplate = Resolve<PlanningPage>(services);
        AssistantContent.ContentTemplate = Resolve<AssistantPage>(services);
        MoreContent.ContentTemplate = Resolve<MorePage>(services);
        AccountsContent.ContentTemplate = Resolve<AccountsPage>(services);
        CategoriesContent.ContentTemplate = Resolve<CategoriesPage>(services);
        ExportContent.ContentTemplate = Resolve<ExportPage>(services);
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(FlyoutIsPresented) && FlyoutIsPresented && accountService is not null)
            _ = RefreshBalanceAsync();
    }

    private async Task RefreshBalanceAsync()
    {
        try
        {
            var accounts = (await accountService.GetAccountsAsync()).Where(item => item.Account.IsActive).ToArray();
            MenuBalance = accounts.Any(a => a.HasCurrencyMismatch) ? "Verifica le valute dei conti" : "Saldo: " + MoneyFormatter.Format(accounts.Sum(item => item.CurrentBalance), CurrencySelection.SingleOrDefault(accounts.Select(item => item.Account.Currency)));
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            MenuBalance = "Saldo non disponibile";
        }
    }

    private async void OnNavigate(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: string route }) return;
        FlyoutIsPresented = false;
        await GoToAsync(route);
    }

    private static DataTemplate Resolve<TPage>(IServiceProvider services) where TPage : Page =>
        new(() => services.GetRequiredService<TPage>());
}
