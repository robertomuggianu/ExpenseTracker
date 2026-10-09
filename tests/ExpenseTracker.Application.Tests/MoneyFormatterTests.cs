using System.Globalization;
using ExpenseTracker.Presentation.Services;

namespace ExpenseTracker.Application.Tests;

public sealed class MoneyFormatterTests
{
    [Fact]
    public void DefaultFormatting_UsesItalianDecimalsOnEnglishDevices()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Assert.Contains("1.234,56", MoneyFormatter.Format(1234.56m, "EUR"));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("en-GB", "EUR", "€", "£")]
    [InlineData("it-IT", "GBP", "£", "€")]
    [InlineData("en-US", "EUR", "€", "$")]
    public void CurrencyComesFromData_NotDeviceRegion(string locale, string currency, string expected, string unexpected)
    {
        var culture = CultureInfo.GetCultureInfo(locale);
        var originalSymbol = culture.NumberFormat.CurrencySymbol;
        var result = MoneyFormatter.Format(-1234.56m, currency, culture);
        Assert.Contains(expected, result);
        Assert.DoesNotContain(unexpected, result);
        Assert.Equal(originalSymbol, culture.NumberFormat.CurrencySymbol);
    }

    [Fact]
    public void MixedCurrenciesAreNotShownAsEuroTotals()
    {
        var currency = CurrencySelection.SingleOrDefault(["EUR", "GBP"]);
        Assert.Null(currency);
        Assert.Equal("Valute miste", MoneyFormatter.Format(100m, currency));
    }
}
