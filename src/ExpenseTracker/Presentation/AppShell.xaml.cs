using ExpenseTracker.Application;
using ExpenseTracker.Presentation.Services;
using ExpenseTracker.Presentation.Views;
using ExpenseTracker.Presentation.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Presentation;

public partial class AppShell : Shell
{
    private readonly AccountService accountService;
    private readonly IAppPreferences preferences;
    private readonly IServiceProvider services;
    private AssistantPage? assistantPage;
    private bool assistantTransition;
    public bool IsAssistantOpen { get; private set; }
    private string menuBalance = "I tuoi conti, sempre a portata di mano";
    public string MenuBalance { get => menuBalance; private set { menuBalance = value; OnPropertyChanged(nameof(MenuBalance)); } }

    public AppShell(IServiceProvider services)
    {
        this.services = services;
        accountService = services.GetRequiredService<AccountService>();
        preferences = services.GetRequiredService<IAppPreferences>();
        InitializeComponent();
        DashboardContent.ContentTemplate = Resolve<DashboardPage>(services);
        TransactionsContent.ContentTemplate = Resolve<TransactionsPage>(services);
        NewTransactionContent.ContentTemplate = Resolve<TransactionEditorPage>(services);
        StatisticsContent.ContentTemplate = Resolve<StatisticsPage>(services);
        PlanningContent.ContentTemplate = Resolve<PlanningPage>(services);
        MoreContent.ContentTemplate = Resolve<MorePage>(services);
        AccountsContent.ContentTemplate = Resolve<AccountsPage>(services);
        CategoriesContent.ContentTemplate = Resolve<CategoriesPage>(services);
        ExportContent.ContentTemplate = Resolve<ExportPage>(services);
        Navigated += (_, _) => RefreshWindowInsets();
    }

    public async Task OpenAssistantAsync()
    {
        if (IsAssistantOpen || assistantTransition) return;
        assistantTransition = true;
        IsAssistantOpen = true;
        try
        {
            assistantPage ??= services.GetRequiredService<AssistantPage>();
            Shell.SetNavBarIsVisible(assistantPage, false);
            Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page.SetModalPresentationStyle(assistantPage.On<Microsoft.Maui.Controls.PlatformConfiguration.iOS>(),
                Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.UIModalPresentationStyle.FullScreen);
            await Navigation.PushModalAsync(assistantPage, false);
        }
        catch { IsAssistantOpen = false; throw; }
        finally { assistantTransition = false; }
    }

    public async Task CloseAssistantAsync()
    {
        if (!IsAssistantOpen || assistantTransition || Navigation.ModalStack.LastOrDefault() != assistantPage) return;
        assistantTransition = true;
        try { await Navigation.PopModalAsync(false); }
        finally { IsAssistantOpen = false; assistantTransition = false; RefreshWindowInsets(); }
    }

    private void RefreshWindowInsets()
    {
#if ANDROID
        // MAUI can retain stale system-bar insets after a modal or Shell transition.
        // Request them again after the restored page has been attached to the window.
        void ApplyInsets()
        {
            CurrentPage?.Handler?.UpdateValue(nameof(ContentPage.SafeAreaEdges));
            if (CurrentPage?.Handler?.PlatformView is Android.Views.View view)
            {
                view.RequestLayout();
                view.RequestApplyInsets();
            }
            Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window?.DecorView?.RequestApplyInsets();
        }
        Dispatcher.Dispatch(ApplyInsets);
        // Flyout dismissal can finish after Navigated; repeat once after the transition.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(350), ApplyInsets);
#endif
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(FlyoutIsPresented) && FlyoutIsPresented && accountService is not null && preferences is not null)
        {
            // Clear the last displayed balance synchronously before the menu is drawn.
            MenuBalance = preferences.Get("hide-home-balance", false) ? "Saldo nascosto" : "Aggiornamento saldo…";
            _ = RefreshBalanceAsync();
        }
    }

    private async Task RefreshBalanceAsync()
    {
        try
        {
            if (preferences.Get("hide-home-balance", false)) { MenuBalance = "Saldo nascosto"; return; }
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
        new(() =>
        {
            var page = services.GetRequiredService<TPage>();
            // Apply to the resolved page: a setting on Shell alone is not sufficient on every platform.
            Shell.SetNavBarIsVisible(page, false);
            if (page is ContentPage contentPage) AssistantLauncher.Attach(contentPage);
            return page;
        });
}
