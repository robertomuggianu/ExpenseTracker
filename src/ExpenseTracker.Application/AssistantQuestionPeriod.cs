using System.Text.RegularExpressions;

namespace ExpenseTracker.Application;

/// <summary>Explicit month references take precedence over a model's interpretation and older context.</summary>
public static class AssistantQuestionPeriod
{
    public static AssistantChatPeriod? Resolve(string question)
    {
        bool Has(string pattern) => Regex.IsMatch(question, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var previous = Has(@"\b(?:(?:scorso|precedente|passato)\s+mese|mese\s+(?:scorso|precedente|passato)|(?:last|previous|prior|past)\s+month)\b");
        var current = Has(@"\b(?:questo\s+mese|mese\s+(?:corrente|attuale|in\s+corso)|(?:this|current)\s+month)\b");
        var comparison = Has(@"\b(?:confront\w*|compar\w*|rispetto|versus|vs|relative\s+to|against)\b");
        if (current && Has(@"\b(?:con\s+il|rispetto\s+al)\s+precedente\b")) previous = true;
        if (current && Has(@"\b(?:with|to|against)\s+(?:the\s+)?(?:previous|last)\s+one\b")) previous = true;
        if (previous && (current || comparison)) return AssistantChatPeriod.Comparison;
        if (previous) return AssistantChatPeriod.Previous;
        if (current) return AssistantChatPeriod.Current;
        return null;
    }

    // A model cannot silently replace a comparison of equivalent days with a full month.
    public static AssistantChatComparison ResolveComparison(string question) => Regex.IsMatch(question,
        @"\b(?:mese\s+(?:(?:precedente|scorso)\s+)?(?:intero|completo)|(?:intero|completo)\s+(?:mese|mese\s+precedente)|(?:full|whole|entire|complete)\s+(?:(?:previous|last)\s+)?month|(?:previous|last)\s+month\s+in\s+full|whole\s+of\s+(?:the\s+)?(?:previous|last)\s+month)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
        ? AssistantChatComparison.FullPreviousMonth : AssistantChatComparison.SameDays;
}
