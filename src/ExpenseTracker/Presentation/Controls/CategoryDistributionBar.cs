using System.Collections.Specialized;
using System.Windows.Input;
using ExpenseTracker.Presentation.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace ExpenseTracker.Presentation.Controls;

// Uses the same amounts, order and colours as the doughnut and category list.
public sealed class CategoryDistributionBar : Border
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable<CategoryBreakdownItem>), typeof(CategoryDistributionBar),
        propertyChanged: (bindable, oldValue, newValue) =>
        {
            var bar = (CategoryDistributionBar)bindable;
            bar.Observe(null);
            if (bar.IsLoaded) bar.Observe(newValue as INotifyCollectionChanged);
            bar.Rebuild();
        });
    public static readonly BindableProperty SegmentCommandProperty = BindableProperty.Create(
        nameof(SegmentCommand), typeof(ICommand), typeof(CategoryDistributionBar));

    private readonly Grid segments = new() { ColumnSpacing = 1 };
    private INotifyCollectionChanged? observed;
    public IEnumerable<CategoryBreakdownItem>? ItemsSource
    {
        get => (IEnumerable<CategoryBreakdownItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
    public ICommand? SegmentCommand
    {
        get => (ICommand?)GetValue(SegmentCommandProperty);
        set => SetValue(SegmentCommandProperty, value);
    }

    public CategoryDistributionBar()
    {
        HeightRequest = 18;
        StrokeThickness = 0;
        StrokeShape = new RoundRectangle { CornerRadius = 9 };
        BackgroundColor = Color.FromArgb("#56615B");
        Content = segments;
        Loaded += (_, _) => { Observe(ItemsSource as INotifyCollectionChanged); Rebuild(); };
        Unloaded += (_, _) => Observe(null);
    }

    private void Observe(INotifyCollectionChanged? source)
    {
        if (observed is not null) observed.CollectionChanged -= OnCollectionChanged;
        observed = source;
        if (observed is not null) observed.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        segments.Children.Clear();
        segments.ColumnDefinitions.Clear();
        foreach (var item in ItemsSource ?? [])
        {
            if (item.Total <= 0) continue;
            var column = segments.ColumnDefinitions.Count;
            segments.ColumnDefinitions.Add(new ColumnDefinition(new GridLength((double)item.Total, GridUnitType.Star)));
            var segment = new BoxView { Color = Color.FromArgb(item.Color) };
            SemanticProperties.SetDescription(segment, $"{item.Name}: {item.Percentage}");
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) =>
            {
                if (SegmentCommand?.CanExecute(item) == true) SegmentCommand.Execute(item);
            };
            segment.GestureRecognizers.Add(tap);
            segments.Add(segment, column);
        }
    }
}
