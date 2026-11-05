namespace ExpenseTracker.Presentation.Services;

/// <summary>Appearance preferences and semantic resources; never changes account or category data.</summary>
public static class AppAppearance
{
    private const string PreferenceKey = "app-appearance";
    public static bool IsDark => Preferences.Default.Get(PreferenceKey, "dark") != "light";

    public static void Initialize(Microsoft.Maui.Controls.Application app)
    {
        SetTheme(app, IsDark);
    }

    public static void Select(bool dark)
    {
        if (Microsoft.Maui.Controls.Application.Current is not { } app) return;
        Preferences.Default.Set(PreferenceKey, dark ? "dark" : "light");
        SetTheme(app, dark);
    }

    private static void SetTheme(Microsoft.Maui.Controls.Application app, bool dark)
    {
        app.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
        ApplyPalette(app);
    }

    private static void ApplyPalette(Microsoft.Maui.Controls.Application app)
    {
        var dark = app.RequestedTheme == AppTheme.Dark;
        var resources = app.Resources;
        foreach (var (key, light, graphite) in new (string, string, string)[]
        {
            ("CanvasColor", "#F5F2EA", "#17181B"),
            ("SurfaceColor", "#FFFFFF", "#25262A"),
            ("SurfaceAltColor", "#EBE7DE", "#303136"),
            ("TextColor", "#242529", "#F4F0E7"),
            ("MutedColor", "#62625F", "#B4B2AC"),
            ("BorderColor", "#DCD7CE", "#3C3D42"),
            ("AccentTextColor", "#725600", "#FFD21F"),
            ("IncomeColor", "#386348", "#A7C38A"),
            ("ExpenseColor", "#9E442D", "#E7A088"),
            ("WarningColor", "#725600", "#FFD21F"),
            ("InfoColor", "#47627A", "#A5B8CC")
        }) resources[key] = Color.FromArgb(dark ? graphite : light);

        foreach (var name in new[] { "ui_search", "ui_eye", "ui_eye_off", "ui_assistant", "tab_month",
                     "tab_chart", "tab_transactions", "tab_plan", "tab_more", "tab_plus", "menu_wallet", "menu_tag", "menu_repeat", "theme_sun", "theme_moon" })
            resources[name + "Source"] = ImageSource.FromFile(name + (dark ? "_dark.png" : ".png"));
        resources["ChartDarkTheme"] = dark;
        app.Dispatcher.Dispatch(RefreshSystemBars);
    }

    public static void RefreshSystemBars()
    {
#if ANDROID
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.Window is not { } window) return;
        var controller = AndroidX.Core.View.WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller is null) return;
        controller.AppearanceLightStatusBars = !IsDark;
        controller.AppearanceLightNavigationBars = !IsDark;
#endif
    }
}
