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
        AccountsContent.ContentTemplate = Resolve<AccountsPage>(services);
        CategoriesContent.ContentTemplate = Resolve<CategoriesPage>(services);
        ExportContent.ContentTemplate = Resolve<ExportPage>(services);
    }

    private static DataTemplate Resolve<TPage>(IServiceProvider services)
        where TPage : Page =>
        new(() => services.GetRequiredService<TPage>());
}
