using System.Text.Json.Serialization;

namespace ExpenseTracker.Application;

// Generated metadata also works when the mobile build trims unused reflection metadata.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AssistantReportRequest))]
[JsonSerializable(typeof(AssistantReportResponse))]
[JsonSerializable(typeof(AssistantHealth))]
[JsonSerializable(typeof(AssistantQuestionRequest))]
[JsonSerializable(typeof(AssistantInterpretation))]
[JsonSerializable(typeof(AssistantChatEvidence))]
[JsonSerializable(typeof(AssistantChatAnswer))]
public partial class AssistantJsonContext : JsonSerializerContext;
