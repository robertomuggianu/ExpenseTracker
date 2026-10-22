using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.ViewModels;
using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace ExpenseTracker.Presentation.Controls;

/// <summary>Direct, accessible type selection without a modal or reflected labels.</summary>
public sealed class TransactionTypeField : Grid
{
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem), typeof(TransactionTypeOption), typeof(TransactionTypeField),
        TransactionTypeOption.Required[1], BindingMode.TwoWay,
        propertyChanged: static (bindable, _, _) => ((TransactionTypeField)bindable).UpdateSelection());

    private readonly Button income;
    private readonly Button expense;

    public TransactionTypeField()
    {
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        ColumnSpacing = 10;
        income = CreateButton(TransactionType.Income, 0);
        expense = CreateButton(TransactionType.Expense, 1);
        UpdateSelection();
    }

    public TransactionTypeOption SelectedItem
    {
        get => (TransactionTypeOption)GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    private Button CreateButton(TransactionType type, int column)
    {
        var option = TransactionTypeOption.Required.Single(item => item.Value == type);
        var button = new Button { Text = option.Label, MinimumHeightRequest = 48, CornerRadius = 16, BorderWidth = 2 };
        button.Clicked += (_, _) => SelectedItem = option;
        button.SetValue(ColumnProperty, column);
        Children.Add(button);
        return button;
    }

    private void UpdateSelection()
    {
        if (income is null || expense is null) return;
        StyleButton(income, SelectedItem?.Value == TransactionType.Income);
        StyleButton(expense, SelectedItem?.Value == TransactionType.Expense);
    }

    private static void StyleButton(Button button, bool selected)
    {
        button.SetDynamicResource(BackgroundColorProperty, selected ? "PrimaryGlow" : "DarkSurface");
        button.SetDynamicResource(Button.TextColorProperty, selected ? "DarkText" : "PrimaryGlow");
        button.SetDynamicResource(Button.BorderColorProperty, selected ? "PrimaryGlow" : "DarkSurface");
        SemanticProperties.SetDescription(button, selected ? $"{button.Text}, selezionata" : button.Text);
    }
}
