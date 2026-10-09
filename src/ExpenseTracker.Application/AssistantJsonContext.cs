using System.Text.Json.Serialization;

namespace ExpenseTracker.Application;

// Generated metadata also works when the mobile build trims unused reflection metadata.
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AssistantReportRequest))]
[JsonSerializable(typeof(AssistantReportResponse))]
[JsonSerializable(typeof(AssistantHealth))]
public partial class AssistantJsonContext : JsonSerializerContext;
