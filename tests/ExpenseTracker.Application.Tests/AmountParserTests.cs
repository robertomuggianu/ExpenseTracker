using System.Globalization;
using ExpenseTracker.Presentation.Services;

namespace ExpenseTracker.Application.Tests;

public class AmountParserTests
{
    [Theory]
    [InlineData("6,50", "6.50")]
    [InlineData("6.50", "6.50")]
    [InlineData("650", "650")]
    [InlineData("0,05", "0.05")]
    [InlineData(" 1234,56 ", "1234.56")]
    [InlineData("-6,50", "-6.50")]
    public void ParsesDecimalSeparatorIndependentlyOfDeviceCulture(string input, string expected)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-US", "it-IT", "de-DE" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                Assert.True(AmountParser.TryParse(input, out var amount));
                Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), amount);
            }
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData("1.234,56")]
    [InlineData("1,234.56")]
    [InlineData("6,,50")]
    [InlineData("")]
    [InlineData("abc")]
    public void RejectsInvalidOrMixedSeparators(string input) => Assert.False(AmountParser.TryParse(input, out _));
}
