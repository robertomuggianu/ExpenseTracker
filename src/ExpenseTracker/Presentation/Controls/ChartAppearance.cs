using System.ComponentModel;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Maui;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace ExpenseTracker.Presentation.Controls;

/// <summary>Repaints chart chrome on theme or report changes without reloading data.</summary>
public static class ChartAppearance
{
    public static readonly BindableProperty IsDarkProperty = BindableProperty.CreateAttached(
        "IsDark", typeof(bool?), typeof(ChartAppearance), null, propertyChanged: (bindable, _, _) =>
        {
            if (bindable is not ChartView chart) return;
            chart.PropertyChanged -= OnChartChanged;
            chart.PropertyChanged += OnChartChanged;
            Apply(chart);
        });

    public static bool? GetIsDark(BindableObject target) => (bool?)target.GetValue(IsDarkProperty);
    public static void SetIsDark(BindableObject target, bool? value) => target.SetValue(IsDarkProperty, value);

    private static void OnChartChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is ChartView chart && e.PropertyName is "Series" or "XAxes" or "YAxes") Apply(chart);
    }

    private static void Apply(ChartView chart)
    {
        var dark = GetIsDark(chart) == true;
        var text = SKColor.Parse(dark ? "#B4B2AC" : "#62625F");
        var surface = SKColor.Parse(dark ? "#25262A" : "#FFFFFF");
        var border = SKColor.Parse(dark ? "#3C3D42" : "#DCD7CE");
        if (chart is PieChart pie)
        {
            pie.LegendTextPaint = new SolidColorPaint(text);
            pie.TooltipTextPaint = new SolidColorPaint(text);
            pie.TooltipBackgroundPaint = new SolidColorPaint(surface);
            foreach (var series in pie.Series?.OfType<PieSeries<decimal>>() ?? [])
                series.Stroke = new SolidColorPaint(surface, 3);
        }
        if (chart is CartesianChart cartesian)
        {
            cartesian.LegendTextPaint = new SolidColorPaint(text);
            cartesian.TooltipTextPaint = new SolidColorPaint(text);
            cartesian.TooltipBackgroundPaint = new SolidColorPaint(surface);
            foreach (var axis in (cartesian.XAxes ?? []).Concat(cartesian.YAxes ?? []).OfType<Axis>())
            {
                axis.LabelsPaint = new SolidColorPaint(text);
                if (axis.SeparatorsPaint is not null) axis.SeparatorsPaint = new SolidColorPaint(border);
            }
            foreach (var series in cartesian.Series?.OfType<ColumnSeries<decimal>>() ?? [])
                series.Fill = new SolidColorPaint(SKColor.Parse(series.Name == "Entrate"
                    ? dark ? "#A7C38A" : "#668A64"
                    : dark ? "#FFD21F" : "#C39420"));
        }
    }
}
