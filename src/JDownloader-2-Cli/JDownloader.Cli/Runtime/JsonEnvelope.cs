using System.Text.Json.Serialization;

namespace JDownloader.Cli.Runtime;

public sealed record JsonError(
    string Kind,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Recovery = null);

public sealed record JsonMeta(
    int SchemaVersion,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Warnings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DiagnosticLogPath = null);

public sealed record JsonEnvelope(
    bool Ok,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Data,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonError? Error,
    JsonMeta Meta);
