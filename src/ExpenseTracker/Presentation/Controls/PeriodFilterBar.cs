using System.Globalization;
using ExpenseTracker.Application;
using ExpenseTracker.Presentation.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace ExpenseTracker.Presentation.Controls;

/// <summary>One compact period selector shared by Home, Movimenti and Statistiche.</summary>
public sealed class PeriodFilterBar : ContentView
{
    public static readonly BindableProperty SelectedPeriodProperty = BindableProperty.Create(nameof(SelectedPeriod), typeof(PeriodOption),
        typeof(PeriodFilterBar), PeriodOption.All[1], BindingMode.TwoWay, propertyChanged: Changed);
    public static readonly BindableProperty ReferenceDateProperty = BindableProperty.Create(nameof(ReferenceDate), typeof(DateTime),
        typeof(PeriodFilterBar), DateTime.Today, BindingMode.TwoWay, propertyChanged: Changed);
    public static readonly BindableProperty CustomFromProperty = BindableProperty.Create(nameof(CustomFrom), typeof(DateTime),
        typeof(PeriodFilterBar), DateTime.Today.AddMonths(-1), BindingMode.TwoWay, propertyChanged: Changed);
    public static readonly BindableProperty CustomToProperty = BindableProperty.Create(nameof(CustomTo), typeof(DateTime),
        typeof(PeriodFilterBar), DateTime.Today, BindingMode.TwoWay, propertyChanged: Changed);

    private readonly Grid segments = new() { ColumnSpacing = 0, Padding = 3 };
    private readonly Border indicator = new() { InputTransparent = true, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 }, HorizontalOptions = LayoutOptions.Start };
    private readonly Button[] buttons = new Button[4];
    private readonly Grid navigation = new() { ColumnDefinitions = [new(44), new(GridLength.Star), new(44)] };
    private readonly Label caption;
    private readonly Grid custom = new() { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 12 };
    private bool updating;
    private int selectedIndex = -1;
    private double segmentWidth;

    public PeriodOption SelectedPeriod { get => (PeriodOption)GetValue(SelectedPeriodProperty); set => SetValue(SelectedPeriodProperty, value); }
    public DateTime ReferenceDate { get => (DateTime)GetValue(ReferenceDateProperty); set => SetValue(ReferenceDateProperty, value); }
    public DateTime CustomFrom { get => (DateTime)GetValue(CustomFromProperty); set => SetValue(CustomFromProperty, value); }
    public DateTime CustomTo { get => (DateTime)GetValue(CustomToProperty); set => SetValue(CustomToProperty, value); }

    public PeriodFilterBar()
    {
        var track = new Border { Content = segments, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 15 }, Padding = 0 };
        track.SetDynamicResource(BackgroundColorProperty, "SurfaceAltColor");
        indicator.SetDynamicResource(BackgroundColorProperty, "SurfaceColor");
        indicator.Shadow = new Shadow { Brush = Brush.Black, Opacity = .10f, Radius = 5, Offset = new Point(0, 2) };
        Grid.SetColumnSpan(indicator, 4); segments.Children.Add(indicator);
        string[] labels = ["Settimana", "Mese", "Anno", "Periodo"];
        for (var i = 0; i < labels.Length; i++)
        {
            var index = i;
            segments.ColumnDefinitions.Add(new(GridLength.Star));
            var button = MakeButton(labels[i]);
            button.FontSize = 13; button.Padding = new Thickness(2, 8); button.MinimumHeightRequest = 44;
            button.AutomationId = "Period." + labels[i];
            button.Clicked += (_, _) => SelectedPeriod = PeriodOption.All[index];
            Grid.SetColumn(button, i); segments.Children.Add(button); buttons[i] = button;
        }
        caption = new Label
        {
            FontAttributes = FontAttributes.Bold, FontSize = 16,
            HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center
        };
        caption.SetDynamicResource(Label.TextColorProperty, "TextColor");
        var previous = MakeButton("‹"); previous.FontSize = 28; previous.Clicked += (_, _) => Step(-1);
        var next = MakeButton("›"); next.FontSize = 28; next.Clicked += (_, _) => Step(1);
        SemanticProperties.SetDescription(previous, "Periodo precedente"); SemanticProperties.SetDescription(next, "Periodo successivo");
        navigation.Add(previous, 0); navigation.Add(caption, 1); navigation.Add(next, 2);
        AddDate("Dal", nameof(CustomFrom), 0); AddDate("Al", nameof(CustomTo), 1);
        Content = new VerticalStackLayout { Spacing = 8, Children = { track, navigation, custom } };
        segments.SizeChanged += (_, _) => PlaceIndicator(false);
        Loaded += (_, _) => Refresh(false);
        Unloaded += (_, _) => indicator.AbortAnimation("PeriodSlide");
        Refresh(false);
    }

    private static Button MakeButton(string text)
    {
        var b = new Button { Text = text, BackgroundColor = Colors.Transparent, BorderWidth = 0, CornerRadius = 12, Padding = 4, MinimumHeightRequest = 44 };
        b.SetDynamicResource(Button.TextColorProperty, "TextColor"); return b;
    }

    private void AddDate(string title, string property, int column)
    {
        var date = new DatePicker { Format = "dd MMM yyyy", FontSize = 14, HorizontalOptions = LayoutOptions.Fill };
        date.SetBinding(DatePicker.DateProperty, new Binding(property, source: this, mode: BindingMode.TwoWay));
        SemanticProperties.SetDescription(date, title == "Dal" ? "Data iniziale" : "Data finale");
        var label = new Label { Text = title, FontSize = 12 }; label.SetDynamicResource(Label.TextColorProperty, "MutedColor");
        custom.Add(new VerticalStackLayout { Spacing = 2, Children = { label, date } }, column);
    }

    private static void Changed(BindableObject bindable, object oldValue, object newValue) => ((PeriodFilterBar)bindable).Refresh(true);
    private void Refresh(bool animate)
    {
        if (caption is null || updating) return;
        updating = true;
        try
        {
            var preset = SelectedPeriod.Value;
            var index = preset switch { DateRangePreset.CurrentWeek => 0, DateRangePreset.CurrentYear or DateRangePreset.SelectedYear => 2, DateRangePreset.Custom => 3, _ => 1 };
            var changed = selectedIndex != index; selectedIndex = index;
            for (var i = 0; i < buttons.Length; i++)
            {
                buttons[i].FontAttributes = i == index ? FontAttributes.Bold : FontAttributes.None;
                SemanticProperties.SetDescription(buttons[i], buttons[i].Text + (i == index ? ", selezionato" : ""));
            }
            custom.IsVisible = index == 3; navigation.IsVisible = index != 3;
            if (index != 3)
            {
                var range = DateRangeFactory.Create(preset, ReferenceDate);
                caption.Text = index == 2 ? ReferenceDate.Year.ToString() : index == 1
                    ? ReferenceDate.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("it-IT"))
                    : $"{range.FromUtc.ToLocalTime():dd MMM} – {range.ToUtcExclusive.ToLocalTime().AddDays(-1):dd MMM}";
            }
            PlaceIndicator(animate && changed);
        }
        finally { updating = false; }
    }

    private void Step(int direction) => ReferenceDate = selectedIndex switch
    {
        0 => ReferenceDate.AddDays(7 * direction), 2 => ReferenceDate.AddYears(direction), _ => ReferenceDate.AddMonths(direction)
    };

    private void PlaceIndicator(bool animate)
    {
        if (segments.Width <= 6 || selectedIndex < 0) return;
        var width = (segments.Width - 6) / 4;
        var x = width * selectedIndex;
        if (Math.Abs(segmentWidth - width) > .5) { indicator.WidthRequest = width; segmentWidth = width; animate = false; }
        // Ignore layout notifications caused by the translation itself.
        if (!animate && indicator.AnimationIsRunning("PeriodSlide")) return;
        indicator.AbortAnimation("PeriodSlide");
        if (!animate || !IsLoaded || ReduceMotion()) { indicator.TranslationX = x; indicator.Opacity = 1; return; }
        var animation = new Animation(v => indicator.TranslationX = v, indicator.TranslationX, x, Easing.CubicOut);
        indicator.Opacity = .94;
        animation.Commit(indicator, "PeriodSlide", length: 190, finished: (_, _) => indicator.Opacity = 1);
    }

    private static bool ReduceMotion()
    {
#if IOS || MACCATALYST
        return UIKit.UIAccessibility.IsReduceMotionEnabled;
#elif ANDROID
        return OperatingSystem.IsAndroidVersionAtLeast(26) && !Android.Animation.ValueAnimator.AreAnimatorsEnabled();
#else
        return false;
#endif
    }
}
