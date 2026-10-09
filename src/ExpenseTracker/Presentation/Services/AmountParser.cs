using System.Globalization;

namespace ExpenseTracker.Presentation.Services;

public static class AmountParser
{
    // Entry fields accept either decimal separator, never thousands separators.
    public static bool TryParse(string? value, out decimal result) =>
        decimal.TryParse(value?.Trim().Replace(',', '.'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out result);
}
