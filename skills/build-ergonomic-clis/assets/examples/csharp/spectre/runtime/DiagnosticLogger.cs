using System.Net.Http;
using System.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ExampleCli.Runtime;

public sealed class DiagnosticLogger
{
    private const int MaxBodyPreviewBytes = 64 * 1024;

    public string? TryWrite(
        ResolvedContextSafe context,
        string operation,
        Exception exception,
        HttpExchangeSnapshot? exchange = null)
    {
        try
        {
            var path = CreateLogPath();
            var builder = BuildBaseLog(operation, exception);
            builder.AppendLine($"BaseUrl: {SecretRedactor.RedactPotentialSecrets(context.BaseUrl)}");
            builder.AppendLine($"BaseUrlSource: {context.BaseUrlSource}");
            builder.AppendLine($"TargetIdentityKey: {context.TargetIdentityKey}");
            builder.AppendLine($"Profile: {context.Profile}");
            builder.AppendLine($"ProfileSource: {context.ProfileSource}");
            builder.AppendLine($"AuthSource: {context.AuthSource}");
            builder.AppendLine($"OutputMode: {context.OutputMode}");
            builder.AppendLine($"OutputModeSource: {context.OutputModeSource}");
            AppendExchange(builder, exchange);
            File.WriteAllText(path, builder.ToString());
            return path;
        }
        catch
        {
            return null;
        }
    }

    public string? TryWrite(string operation, Exception exception)
    {
        try
        {
            var path = CreateLogPath();
            var builder = BuildBaseLog(operation, exception);
            File.WriteAllText(path, builder.ToString());
            return path;
        }
        catch
        {
            return null;
        }
    }

    public static string RedactHeadersForDiagnostics(params HttpHeaders?[] headersSets)
    {
        var lines = new List<string>();

        foreach (var headers in headersSets)
        {
            if (headers is null)
                continue;

            foreach (var header in headers)
            {
                var keyLower = header.Key.ToLowerInvariant();
                var looksSensitiveByName =
                    keyLower.Contains("authorization")
                    || keyLower.Contains("cookie")
                    || keyLower.Contains("token")
                    || keyLower.Contains("secret")
                    || keyLower.Contains("password")
                    || keyLower.Contains("signature")
                    || keyLower.Contains("credential")
                    || keyLower.Contains("apikey")
                    || keyLower.Contains("api-key");

                var isSensitive =
                    header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Cookie", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("X-Auth-Token", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("X-Access-Token", StringComparison.OrdinalIgnoreCase)
                    || header.Key.Equals("X-Amz-Security-Token", StringComparison.OrdinalIgnoreCase);

                isSensitive |= looksSensitiveByName;

                var rawValue = string.Join(", ", header.Value);
                var value = isSensitive ? "REDACTED" : SecretRedactor.RedactPotentialSecrets(rawValue);

                lines.Add($"{header.Key}: {value}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static async Task<string?> ReadContentPreviewAsync(HttpContent? content, CancellationToken cancellationToken = default)
    {
        if (content is null)
            return null;

        try
        {
            using var stream = await content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            var remaining = MaxBodyPreviewBytes + 1;
            while (remaining > 0)
            {
                var read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, remaining)), cancellationToken);
                if (read == 0)
                    break;

                buffer.Write(chunk, 0, read);
                remaining -= read;
            }

            var bytes = buffer.ToArray();
            var truncated = bytes.Length > MaxBodyPreviewBytes;
            if (truncated)
                bytes = bytes[..MaxBodyPreviewBytes];

            var body = ResolveEncoding(content).GetString(bytes);
            var redacted = SecretRedactor.RedactPotentialSecrets(body) ?? string.Empty;
            return truncated ? $"{Truncate(redacted, MaxBodyPreviewBytes)}...(truncated to 64 KB)" : redacted;
        }
        catch
        {
            return "(body preview unavailable)";
        }
    }

    public static string? SanitizeUriForDiagnostics(Uri? uri)
    {
        if (uri is null)
            return null;

        if (uri.IsAbsoluteUri)
        {
            var builder = new UriBuilder(uri)
            {
                Fragment = string.Empty,
                UserName = string.Empty,
                Password = string.Empty,
            };

            if (builder.Uri.IsDefaultPort)
                builder.Port = -1;

            return SecretRedactor.RedactPotentialSecrets(builder.Uri.PathAndQuery.Length == 0
                ? builder.Uri.GetLeftPart(UriPartial.Authority)
                : $"{builder.Uri.GetLeftPart(UriPartial.Authority)}{builder.Uri.PathAndQuery}");
        }

        var raw = uri.OriginalString;
        var hashIndex = raw.IndexOf('#');
        if (hashIndex >= 0)
            raw = raw[..hashIndex];
        return SecretRedactor.RedactPotentialSecrets(raw);
    }

    private static string CreateLogPath()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "example-cli",
            "logs");

        Directory.CreateDirectory(root);

        return Path.Combine(
            root,
            $"example-cli-error-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.log");
    }

    private static StringBuilder BuildBaseLog(string operation, Exception exception)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Timestamp: {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"Operation: {operation}");
        builder.AppendLine($"CommandLine: {SecretRedactor.RedactPotentialSecrets(Environment.CommandLine)}");
        builder.AppendLine($"Exception: {exception.GetType().FullName}");
        builder.AppendLine($"Message: {SecretRedactor.RedactPotentialSecrets(exception.Message)}");
        builder.AppendLine();
        builder.AppendLine("ExceptionDetail:");
        builder.AppendLine(SecretRedactor.RedactPotentialSecrets(exception.ToString()));
        return builder;
    }

    private static void AppendExchange(StringBuilder builder, HttpExchangeSnapshot? exchange)
    {
        if (exchange is null)
            return;

        if (!string.IsNullOrWhiteSpace(exchange.RequestMethod)
            || !string.IsNullOrWhiteSpace(exchange.RequestUri)
            || !string.IsNullOrWhiteSpace(exchange.RequestHeaders)
            || !string.IsNullOrWhiteSpace(exchange.RequestBody))
        {
            builder.AppendLine();
            builder.AppendLine($"Request: {exchange.RequestMethod ?? "(unknown)"} {exchange.RequestUri ?? "(unknown)"}");
            if (!string.IsNullOrWhiteSpace(exchange.RequestHeaders))
                builder.AppendLine(exchange.RequestHeaders);
            if (!string.IsNullOrWhiteSpace(exchange.RequestBody))
            {
                builder.AppendLine("RequestBody:");
                builder.AppendLine(exchange.RequestBody);
            }
        }

        if (exchange.ResponseStatusCode is not null
            || !string.IsNullOrWhiteSpace(exchange.ResponseHeaders)
            || !string.IsNullOrWhiteSpace(exchange.ResponseBody))
        {
            builder.AppendLine();
            builder.AppendLine($"Response: {exchange.ResponseStatusCode?.ToString() ?? "(unknown)"} {exchange.ResponseReasonPhrase}");
            if (!string.IsNullOrWhiteSpace(exchange.ResponseHeaders))
                builder.AppendLine(exchange.ResponseHeaders);
            if (!string.IsNullOrWhiteSpace(exchange.ResponseBody))
            {
                builder.AppendLine("ResponseBody:");
                builder.AppendLine(exchange.ResponseBody);
            }
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        return $"{value[..maxLength]}...(truncated)";
    }

    private static Encoding ResolveEncoding(HttpContent content)
    {
        try
        {
            var charset = content.Headers.ContentType?.CharSet;
            return string.IsNullOrWhiteSpace(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }
}
