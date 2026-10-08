using ExpenseTracker.Presentation.Views;
using Microsoft.Extensions.DependencyInjection;
namespace ExpenseTracker.Presentation;
public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        DashboardContent.ContentTemplate = Resolve<DashboardPage>(services);
        TransactionsContent.ContentTemplate = Resolve<TransactionsPage>(services);
        NewTransactionContent.ContentTemplate = Resolve<TransactionEditorPage>(services);
        StatisticsContent.ContentTemplate = Resolve<StatisticsPage>(services);
        MoreContent.ContentTemplate = Resolve<MorePage>(services);
        Routing.RegisterRoute("accounts", typeof(AccountsPage));
        Routing.RegisterRoute("categories", typeof(CategoriesPage));
        Routing.RegisterRoute("export", typeof(ExportPage));
    }
    private static DataTemplate Resolve<TPage>(IServiceProvider services) where TPage : Page =>
        new(() => services.GetRequiredService<TPage>());
}
