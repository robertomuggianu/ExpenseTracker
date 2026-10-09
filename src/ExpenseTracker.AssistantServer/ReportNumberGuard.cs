using System.Globalization;
using System.Text.RegularExpressions;

namespace ExpenseTracker.AssistantServer;

/// <summary>Rejects unsupported numeric values. It cannot verify the meaning of generated prose.</summary>
public static partial class ReportNumberGuard
{
    [GeneratedRegex(@"\d+(?:[.,]\d+)*")]
    private static partial Regex Numbers();
    public static bool UsesProvidedNumbers(string text, string facts)
    {
        // The server formats all facts in Italian. Do not interpret a decimal comma as a grouping separator here.
        var allowed = Numbers().Matches(facts).SelectMany(m => Parse(m.Value, allowInvariant: false)).ToHashSet();
        return Numbers().Matches(text).All(m => Parse(m.Value, allowInvariant: true).Any(allowed.Contains));
    }
    private static IEnumerable<decimal> Parse(string value, bool allowInvariant)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("it-IT"), out var italian)) yield return italian;
        if (allowInvariant && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant)) yield return invariant;
    }
}
