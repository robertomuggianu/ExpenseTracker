using System.Collections;
using System.Reflection;

namespace ExpenseTracker.Presentation.Controls;

public partial class ChoiceField : ContentView
{
    private bool opening;
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(ChoiceField));
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem), typeof(object), typeof(ChoiceField), defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: static (bindable, _, _) => ((ChoiceField)bindable).OnPropertyChanged(nameof(SelectedLabel)));
    public static readonly BindableProperty DisplayMemberPathProperty = BindableProperty.Create(
        nameof(DisplayMemberPath), typeof(string), typeof(ChoiceField), string.Empty,
        propertyChanged: static (bindable, _, _) => ((ChoiceField)bindable).OnPropertyChanged(nameof(SelectedLabel)));
    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(ChoiceField), "Scegli un'opzione",
        propertyChanged: static (bindable, _, _) => ((ChoiceField)bindable).OnPropertyChanged(nameof(SelectedLabel)));

    public ChoiceField() => InitializeComponent();

    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public string DisplayMemberPath { get => (string)GetValue(DisplayMemberPathProperty); set => SetValue(DisplayMemberPathProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string SelectedLabel => SelectedItem is null ? Title : GetLabel(SelectedItem);

    private async void OnTapped(object? sender, TappedEventArgs e)
    {
        if (opening || ChoiceSelectionPage.IsOpen) return;
        var items = ItemsSource?.Cast<object>().ToArray() ?? [];
        if (items.Length == 0) return;
        opening = true;
        try
        {
            var (accepted, value) = await ChoiceSelectionPage.ShowAsync(
                Title,
                items.Select(item => new ChoiceSelectionItem(item, GetLabel(item), Equals(item, SelectedItem))).ToArray());
            if (accepted) SelectedItem = value;
        }
        finally { opening = false; }
    }

    private string GetLabel(object item)
    {
        if (string.IsNullOrWhiteSpace(DisplayMemberPath)) return item.ToString() ?? Title;
        return item.GetType().GetProperty(DisplayMemberPath, BindingFlags.Instance | BindingFlags.Public)?.GetValue(item)?.ToString() ?? Title;
    }
}
