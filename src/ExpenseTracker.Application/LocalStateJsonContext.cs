using System.Text.Json.Serialization;
using ExpenseTracker.Domain;

namespace ExpenseTracker.Application;

public sealed record TransactionEditorDraft(Guid? Id, TransactionType Type, Guid? Account, Guid? Category,
    DateTime Date, TimeSpan Time, string Amount, string? Description, string? Notes, bool Details);

[JsonSerializable(typeof(TransactionEditorDraft))]
[JsonSerializable(typeof(PlanningState))]
internal partial class LocalStateJsonContext : JsonSerializerContext;

public static class TransactionDraftSerializer
{
    public static string Serialize(TransactionEditorDraft draft) =>
        System.Text.Json.JsonSerializer.Serialize(draft, LocalStateJsonContext.Default.TransactionEditorDraft);
    public static TransactionEditorDraft? Deserialize(string json) =>
        System.Text.Json.JsonSerializer.Deserialize(json, LocalStateJsonContext.Default.TransactionEditorDraft);
}
