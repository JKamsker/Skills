using System.Text.Json;
using JDownloader.Cli.Runtime;

namespace JDownloader.Cli.Transport;

public sealed record MyJdRequestPlan(
    string Operation,
    string Method,
    string Endpoint,
    object? Query,
    object? Body,
    bool Destructive,
    bool ProducesBinary,
    string? DeviceId = null);

public sealed record MyJdTransportResult(object Data, IReadOnlyList<string>? Warnings = null);

public interface IRequestIdProvider
{
    long Next();
}

public interface IMyJdTransport
{
    Task<MyJdTransportResult> ExecuteAsync(ResolvedProfileContext resolved, MyJdRequestPlan plan, CancellationToken cancellationToken);
}

public sealed class TimestampRequestIdProvider : IRequestIdProvider
{
    private long _last = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public long Next() => Interlocked.Increment(ref _last);
}

public sealed class ScaffoldMyJdTransport : IMyJdTransport
{
    public Task<MyJdTransportResult> ExecuteAsync(ResolvedProfileContext resolved, MyJdRequestPlan plan, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resolved.AccountEmail))
        {
            throw CliException.NotAuthenticated(
                "Authentication required for protected commands.",
                $"Run 'jd2 auth login --profile {resolved.ProfileName} --email <email> --password-stdin'.");
        }

        throw CliException.Transport(
            "Live My.JDownloader relay transport is not implemented in this initial scaffold.",
            "Re-run with --dry-run to inspect the exact request plan.");
    }
}

public static class JsonInput
{
    public static object? ParseOptional(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var content = raw.Trim();
        if (content.StartsWith('@'))
        {
            var path = content[1..];
            content = File.ReadAllText(path);
        }

        using var document = JsonDocument.Parse(content);
        return ConvertElement(document.RootElement);
    }

    private static object? ConvertElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                property => property.Name,
                property => ConvertElement(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
    }
}
