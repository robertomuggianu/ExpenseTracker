using System.Globalization;

namespace ExpenseTracker.Presentation.Services;

public sealed class SelectionOutlineConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        new SolidColorBrush(values.Length >= 2 && values[0] is not null && Equals(values[0], values[1])
            ? Color.FromArgb(parameter as string ?? "#80D9BE") : Colors.Transparent);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
