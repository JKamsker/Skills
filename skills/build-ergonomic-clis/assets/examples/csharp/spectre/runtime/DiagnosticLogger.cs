using System.Net.Http;
using System.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ExampleCli.Runtime;

public sealed class DiagnosticLogger
{
    private static readonly Regex JwtPattern = new(@"\b[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b", RegexOptions.Compiled);
    private static readonly Regex SecretParamPattern = new(@"\b(?<key>token|access_token|refresh_token|id_token|api_key|apikey|client_secret|password|secret)=(?<value>[^&\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string? TryWrite(
        ResolvedContextSafe context,
        string operation,
        Exception exception,
        HttpRequestMessage? request = null,
        HttpResponseMessage? response = null)
    {
        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "example-cli",
                "logs");

            Directory.CreateDirectory(root);

            var path = Path.Combine(
                root,
                $"example-cli-error-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.log");

            var builder = new StringBuilder();
            builder.AppendLine($"Timestamp: {DateTimeOffset.UtcNow:O}");
            builder.AppendLine($"Operation: {operation}");
            builder.AppendLine($"BaseUrl: {RedactPotentialSecrets(context.BaseUrl)}");
            builder.AppendLine($"TargetIdentityKey: {context.TargetIdentityKey}");
            builder.AppendLine($"Profile: {context.Profile}");
            builder.AppendLine($"AuthSource: {context.AuthSource}");
            builder.AppendLine($"Exception: {exception.GetType().FullName}");
            builder.AppendLine($"Message: {RedactPotentialSecrets(exception.Message)}");

            if (request is not null)
            {
                builder.AppendLine();
                builder.AppendLine($"Request: {request.Method} {RedactPotentialSecrets(SanitizeUri(request.RequestUri) ?? "(unknown)")}");
                builder.AppendLine(RedactHeaders(request.Headers));
                if (request.Content is not null)
                    builder.AppendLine(RedactHeaders(request.Content.Headers));
            }

            if (response is not null)
            {
                builder.AppendLine();
                builder.AppendLine($"Response: {(int)response.StatusCode} {response.ReasonPhrase}");
                builder.AppendLine(RedactHeaders(response.Headers));
                if (response.Content is not null)
                    builder.AppendLine(RedactHeaders(response.Content.Headers));
            }

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
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "example-cli",
                "logs");

            Directory.CreateDirectory(root);

            var path = Path.Combine(
                root,
                $"example-cli-error-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.log");

            var builder = new StringBuilder();
            builder.AppendLine($"Timestamp: {DateTimeOffset.UtcNow:O}");
            builder.AppendLine($"Operation: {operation}");
            builder.AppendLine($"Exception: {exception.GetType().FullName}");
            builder.AppendLine($"Message: {RedactPotentialSecrets(exception.Message)}");

            File.WriteAllText(path, builder.ToString());
            return path;
        }
        catch
        {
            return null;
        }
    }

    private static string RedactHeaders(HttpHeaders headers)
    {
        var lines = new List<string>();

        foreach (var header in headers)
        {
            var keyLower = header.Key.ToLowerInvariant();
            var looksSensitiveByName =
                keyLower.Contains("authorization")
                || keyLower.Contains("cookie")
                || keyLower.Contains("token")
                || keyLower.Contains("secret")
                || keyLower.Contains("password")
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
            var value = isSensitive ? "REDACTED" : RedactPotentialSecrets(rawValue);

            lines.Add($"{header.Key}: {value}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string RedactPotentialSecrets(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        value = JwtPattern.Replace(value, "REDACTED_JWT");
        value = SecretParamPattern.Replace(value, match => $"{match.Groups["key"].Value}=REDACTED");
        value = Regex.Replace(value, @"\bBearer\s+[A-Za-z0-9._~+\-/=]+\b", "Bearer REDACTED", RegexOptions.IgnoreCase);
        value = Regex.Replace(
            value,
            "\"(?<key>token|accessToken|access_token|refreshToken|refresh_token|idToken|id_token|apiKey|api_key|apikey|clientSecret|client_secret|password|secret)\"\\s*:\\s*\"(?<value>[^\"]+)\"",
            match => $"\"{match.Groups["key"].Value}\":\"REDACTED\"",
            RegexOptions.IgnoreCase);
        return value;
    }

    private static string? SanitizeUri(Uri? uri)
    {
        if (uri is null)
            return null;

        if (uri.IsAbsoluteUri)
        {
            var builder = new UriBuilder(uri)
            {
                Query = string.Empty,
                Fragment = string.Empty,
                UserName = string.Empty,
                Password = string.Empty,
            };

            if (builder.Uri.IsDefaultPort)
                builder.Port = -1;

            return builder.Uri.GetLeftPart(UriPartial.Path);
        }

        var raw = uri.OriginalString;
        var hashIndex = raw.IndexOf('#');
        if (hashIndex >= 0)
            raw = raw[..hashIndex];
        var qIndex = raw.IndexOf('?');
        if (qIndex >= 0)
            raw = raw[..qIndex];
        return raw;
    }
}
