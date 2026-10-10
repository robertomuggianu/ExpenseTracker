namespace ExpenseTracker.Application;

public enum AssistantChatPeriod { Current, Previous, Comparison }
public enum AssistantChatOperation { Summary, Total, Categories, AverageDaily, AverageTransaction, Largest, Count }
public enum AssistantChatMeasure { Expenses, Income, Net }
public enum AssistantChatComparison { SameDays, FullPreviousMonth }
public enum AssistantInterpretationStatus { Answer, Clarify, Unsupported }

public sealed record AssistantChatPlan(AssistantChatPeriod Period, AssistantChatOperation Operation,
    AssistantChatMeasure Measure, string? Category = null, AssistantChatComparison Comparison = AssistantChatComparison.SameDays);
public sealed record AssistantQuestionRequest(string Question, DateOnly Today, string Currency,
    IReadOnlyList<string> Categories, AssistantChatPlan? Context = null, string? PreviousQuestion = null);
public sealed record AssistantInterpretation(AssistantInterpretationStatus Status, AssistantChatPlan? Plan, string Message, string Model);
public sealed record AssistantChatPeriodData(DateOnly From, DateOnly Through, int MovementCount, int MeasureMovementCount);
public sealed record AssistantChatEvidence(string Question, AssistantChatPlan Plan, string Currency,
    string Facts, string CalculatedAnswer, IReadOnlyList<AssistantChatPeriodData>? Periods = null);
public sealed record AssistantChatAnswer(string Text, string Model, bool UsedCalculatedAnswer = false);

public static class AssistantChatPolicy
{
    public static bool ValidCurrency(string? value) => value is { Length: 3 } && value.All(c => c is >= 'A' and <= 'Z');
    public static bool ValidQuestion(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 1000;
    public static bool ValidPlan(AssistantChatPlan? plan) => plan is not null && Enum.IsDefined(plan.Period)
        && Enum.IsDefined(plan.Operation) && Enum.IsDefined(plan.Measure) && Enum.IsDefined(plan.Comparison)
        && (plan.Category is null || plan.Category.Length <= 120)
        && (plan.Measure != AssistantChatMeasure.Net || plan.Operation is AssistantChatOperation.Summary or AssistantChatOperation.Total or AssistantChatOperation.Count);
    public static bool ValidQuestionRequest(AssistantQuestionRequest? request) => request is not null
        && ValidQuestion(request.Question) && ValidCurrency(request.Currency) && request.Today.Year is >= 2000 and <= 9998
        && request.Categories is { Count: <= 128 } && request.Categories.All(c => !string.IsNullOrWhiteSpace(c) && c.Length <= 120)
        && request.Categories.Sum(c => c.Length) <= 4096
        && (request.Context is null || ValidPlan(request.Context))
        && (request.PreviousQuestion is null || ValidQuestion(request.PreviousQuestion));
    public static bool ValidEvidence(AssistantChatEvidence? evidence) => evidence is not null && ValidQuestion(evidence.Question)
        && ValidPlan(evidence.Plan) && ValidCurrency(evidence.Currency)
        && !string.IsNullOrWhiteSpace(evidence.Facts) && evidence.Facts.Length <= 10000
        && !string.IsNullOrWhiteSpace(evidence.CalculatedAnswer) && evidence.CalculatedAnswer.Length <= 8000
        && evidence.Periods is { Count: >= 1 and <= 2 }
        && evidence.Periods.Count == (evidence.Plan.Period == AssistantChatPeriod.Comparison ? 2 : 1)
        && evidence.Periods.All(p => p is not null && p.From <= p.Through && p.From.Day == 1
            && p.From.Year == p.Through.Year && p.From.Month == p.Through.Month
            && p.MovementCount >= 0 && p.MeasureMovementCount >= 0 && p.MeasureMovementCount <= p.MovementCount);
}
