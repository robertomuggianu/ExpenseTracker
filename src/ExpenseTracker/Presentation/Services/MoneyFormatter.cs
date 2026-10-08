using System.Globalization;

namespace ExpenseTracker.Presentation.Services;

public static class MoneyFormatter
{
    public static string Format(decimal amount, string? currency, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(currency)) return "Valute miste";
        var code = currency.Trim().ToUpperInvariant();
        var format = (NumberFormatInfo)(culture ?? CultureInfo.CurrentCulture).NumberFormat.Clone();
        format.CurrencySymbol = code switch { "EUR" => "€", "GBP" => "£", "USD" => "US$", _ => code };
        return amount.ToString("C2", format);
    }
}
