using ExpenseTracker.Presentation.ViewModels;
using ExpenseTracker.Presentation.Controls;

namespace ExpenseTracker.Presentation.Views;

public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel _viewModel;
    private bool _isSummaryCompact;
    private bool _isShortLayout;

    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        AutomationProperties.SetExcludedWithChildren(CompactSummary, true);
        ExpandedSummary.SizeChanged += (_, _) => UpdateSummary();
        CompactSummary.SizeChanged += (_, _) => UpdateSummary();
    }

    private void OnHomeScrolled(object? sender, ScrolledEventArgs e) => UpdateSummary();

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        // On short windows (including landscape phones), let the entire Home
        // scroll so a fixed header and summary cannot cover all the available space.
        var shortLayout = height < 600;
        if (HomeHeader is null || shortLayout == _isShortLayout) return;
        _isShortLayout = shortLayout;
        if (shortLayout)
        {
            HomeLayout.Children.Remove(HomeHeader);
            ReportContent.Children.Insert(0, HomeHeader);
            HomeHeader.Margin = new Thickness(-18, 0, -18, 0);
            ReportViewport.Margin = 0;
        }
        else
        {
            ReportContent.Children.Remove(HomeHeader);
            HomeLayout.Children.Insert(0, HomeHeader);
            HomeHeader.Margin = 0;
            ReportViewport.Margin = new Thickness(0, -20, 0, 0);
        }
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        if (ExpandedSummary.Height <= 0 || CompactSummary.Height <= 0) return;
        // Keep the full card's space in the scroll content. Swapping at the height
        // difference places the first row exactly below the pinned compact card,
        // without changing the scroll offset or jumping when returning to the top.
        var threshold = Math.Max(0, ExpandedSummary.Height - CompactSummary.Height);
        var compact = !_isShortLayout && HomeScroll.ScrollY > threshold + (_isSummaryCompact ? -4 : 4);
        if (compact == _isSummaryCompact) return;
        _isSummaryCompact = compact;
        CompactSummary.Opacity = compact ? 1 : 0;
        CompactSummary.InputTransparent = !compact;
        AutomationProperties.SetExcludedWithChildren(CompactSummary, !compact);
        ExpandedSummary.Opacity = compact ? 0 : 1;
        ExpandedSummary.InputTransparent = compact;
        AutomationProperties.SetExcludedWithChildren(ExpandedSummary, compact);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (ChoiceSelectionPage.IsOpen || Shell.Current is AppShell { IsAssistantOpen: true }) return;
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
