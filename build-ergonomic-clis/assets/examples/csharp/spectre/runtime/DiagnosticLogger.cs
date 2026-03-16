using System.Net.Http.Headers;
using System.Text;

namespace ExampleCli.Runtime;

public sealed class DiagnosticLogger
{
    public string Write(
        ResolvedContext context,
        string operation,
        Exception exception,
        HttpRequestMessage? request = null,
        HttpResponseMessage? response = null)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "example-cli",
            "logs");

        Directory.CreateDirectory(root);

        var path = Path.Combine(
            root,
            $"example-cli-error-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}.log");

        var builder = new StringBuilder();
        builder.AppendLine($"Timestamp: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"Operation: {operation}");
        builder.AppendLine($"Host: {context.Host}");
        builder.AppendLine($"Profile: {context.Profile}");
        builder.AppendLine($"AuthSource: {context.AuthSource}");
        builder.AppendLine($"Exception: {exception.GetType().FullName}");
        builder.AppendLine($"Message: {exception.Message}");

        if (request is not null)
        {
            builder.AppendLine();
            builder.AppendLine($"Request: {request.Method} {request.RequestUri}");
            builder.AppendLine(RedactHeaders(request.Headers));
        }

        if (response is not null)
        {
            builder.AppendLine();
            builder.AppendLine($"Response: {(int)response.StatusCode} {response.ReasonPhrase}");
            builder.AppendLine(RedactHeaders(response.Headers));
        }

        File.WriteAllText(path, builder.ToString());
        return path;
    }

    private static string RedactHeaders(HttpHeaders headers)
    {
        var lines = new List<string>();

        foreach (var header in headers)
        {
            var value = header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                || header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase)
                ? "REDACTED"
                : string.Join(", ", header.Value);

            lines.Add($"{header.Key}: {value}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
