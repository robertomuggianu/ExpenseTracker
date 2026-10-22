using ExpenseTracker.Application;
using ExpenseTracker.Domain;
using ExpenseTracker.Presentation.ViewModels;

namespace ExpenseTracker.Presentation.Services;

// Explicit references keep selector labels available in trimmed iPhone builds.
public static class ChoiceLabelFormatter
{
    public static string Format(object item, string member, string fallback) => (item, member) switch
    {
        (TransactionTypeOption option, nameof(TransactionTypeOption.Label)) => option.Label,
        (PeriodOption option, nameof(PeriodOption.Label)) => option.Label,
        (FrequencyOption option, nameof(FrequencyOption.Label)) => option.Label,
        (AccountFilterOption option, nameof(AccountFilterOption.Name)) => option.Name,
        (CategoryFilterOption option, nameof(CategoryFilterOption.Name)) => option.Name,
        (AccountSummary account, nameof(AccountSummary.DisplayName)) => account.DisplayName,
        (Category category, nameof(Category.Name)) => category.Name,
        (string text, _) => text,
        _ => fallback
    };
}
