using System.Text.RegularExpressions;

namespace ExampleCli.Runtime;

public static class SecretRedactor
{
    private static readonly Regex JwtPattern = new(
        @"(?<![A-Za-z0-9_-])[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}(?![A-Za-z0-9_-])",
        RegexOptions.Compiled);

    private static readonly Regex SecretParamPattern = new(
        @"\b(?<key>token|access[-_]?token|refresh[-_]?token|id[-_]?token|api[-_]?key|client[-_]?secret|password|secret|sig|signature|credential|sharedaccesssignature|sas|x-amz-credential|x-amz-signature|x-amz-security-token)=(?<value>[^&\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BearerPattern = new(
        @"\bBearer\s+[A-Za-z0-9._~+\-/=]+(?=$|[^A-Za-z0-9._~+\-/=])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BasicPattern = new(
        @"\bBasic\s+[A-Za-z0-9+/=]+(?=$|[^A-Za-z0-9+/=])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex JsonSecretPattern = new(
        "\"(?<key>token|accessToken|access_token|refreshToken|refresh_token|idToken|id_token|apiKey|api_key|apikey|clientSecret|client_secret|password|secret|sig|signature|credential|sharedAccessSignature|sharedaccesssignature|sas)\"\\s*:\\s*\"(?<value>[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CliFlagValuePattern = new(
        @"(^|\s)--(?<key>token|access-token|access_token|refresh-token|refresh_token|id-token|id_token|api-key|api_key|apikey|client-secret|client_secret|password|secret)\s+(?<val>\S+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex CliFlagEqualsPattern = new(
        @"(^|\s)--(?<key>token|access-token|access_token|refresh-token|refresh_token|id-token|id_token|api-key|api_key|apikey|client-secret|client_secret|password|secret)=(?<val>\S+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string? RedactPotentialSecrets(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        value = JwtPattern.Replace(value, "REDACTED_JWT");
        value = SecretParamPattern.Replace(value, match => $"{match.Groups["key"].Value}=REDACTED");
        value = BearerPattern.Replace(value, "Bearer REDACTED");
        value = BasicPattern.Replace(value, "Basic REDACTED");
        value = JsonSecretPattern.Replace(value, match => $"\"{match.Groups["key"].Value}\":\"REDACTED\"");
        value = CliFlagValuePattern.Replace(value, match => $"{match.Groups[1].Value}--{match.Groups["key"].Value} REDACTED");
        value = CliFlagEqualsPattern.Replace(value, match => $"{match.Groups[1].Value}--{match.Groups["key"].Value}=REDACTED");
        return value;
    }
}
