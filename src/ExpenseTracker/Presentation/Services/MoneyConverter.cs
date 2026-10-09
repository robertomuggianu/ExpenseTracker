using System.Globalization;

namespace ExpenseTracker.Presentation.Services;

public sealed class MoneyConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length >= 2 && values[0] is decimal amount
            ? MoneyFormatter.Format(amount, values[1] as string)
            : string.Empty;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
