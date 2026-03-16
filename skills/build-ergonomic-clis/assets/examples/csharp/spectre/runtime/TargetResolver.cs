using System.Linq;

namespace ExampleCli.Runtime;

// This example implements the **hostname-key** target identity mode (the Jellyfin worked-example choice).
// - Network operations use a normalized base URL (scheme/port/path may matter).
// - Credentials and defaults bind to the hostname identity key (lowercased hostname).
public enum AuthSource
{
    None,
    Flag,
    Environment,
    Profile,
}

public sealed record ProfileConfig(
    string Name,
    string? Hostname = null,
    string? BaseUrl = null,
    OutputMode? Output = null);

public sealed record ResolvedContext(
    string BaseUrl,
    string TargetIdentityKey,
    string Profile,
    string? Token,
    AuthSource AuthSource,
    OutputMode OutputMode);

public interface IProfileStore
{
    string? ActiveProfile { get; }
    IReadOnlyDictionary<string, ProfileConfig> Profiles { get; }
    IReadOnlyDictionary<string, string> TargetDefaults { get; }
}

public interface ICredentialStore
{
    string? GetToken(string targetIdentityKey, string profileName);
}

public sealed class TargetResolver
{
    private readonly IProfileStore _profiles;
    private readonly ICredentialStore _credentials;

    public TargetResolver(IProfileStore profiles, ICredentialStore credentials)
    {
        _profiles = profiles;
        _credentials = credentials;
    }

    public ResolvedContext Resolve(GlobalOptions options)
    {
        var explicitProfile = FirstNonEmpty(
            options.Profile,
            Environment.GetEnvironmentVariable("EXAMPLE_PROFILE"));

        var explicitBaseUrl = FirstNonEmpty(
            options.Host,
            Environment.GetEnvironmentVariable("EXAMPLE_HOST"));

        explicitBaseUrl = explicitBaseUrl is null ? null : NormalizeBaseUrl(explicitBaseUrl);
        var explicitTargetKey = explicitBaseUrl is null ? null : CanonicalTargetIdentity(explicitBaseUrl);

        var profileName = explicitProfile
            ?? SelectProfileForTarget(explicitTargetKey)
            ?? _profiles.ActiveProfile
            ?? "default";

        _profiles.Profiles.TryGetValue(profileName, out var profile);
        var profileHostname = profile?.Hostname is null ? null : NormalizeHostname(profile.Hostname);

        if (explicitTargetKey is not null && explicitProfile is not null && profileHostname is not null && profileHostname != explicitTargetKey)
            throw CliException.Usage($"Profile '{profileName}' is configured for '{profileHostname}', but the target is '{explicitTargetKey}'.");

        string resolvedBaseUrl;
        if (explicitBaseUrl is not null)
        {
            resolvedBaseUrl = explicitBaseUrl;
        }
        else if (!string.IsNullOrWhiteSpace(profile?.BaseUrl))
        {
            resolvedBaseUrl = NormalizeBaseUrl(profile!.BaseUrl!);
        }
        else if (profileHostname is not null)
        {
            resolvedBaseUrl = NormalizeBaseUrl(profileHostname);
        }
        else
        {
            resolvedBaseUrl = "https://api.example.test";
        }

        var targetIdentityKey = CanonicalTargetIdentity(resolvedBaseUrl);

        var tokenFromFlag = FirstNonEmpty(options.Token);
        var tokenFromEnv = FirstNonEmpty(Environment.GetEnvironmentVariable("EXAMPLE_TOKEN"));
        var token = tokenFromFlag
            ?? tokenFromEnv
            ?? _credentials.GetToken(targetIdentityKey, profileName);

        var authSource = tokenFromFlag is not null
            ? AuthSource.Flag
            : tokenFromEnv is not null
                ? AuthSource.Environment
                : token is not null
                    ? AuthSource.Profile
                    : AuthSource.None;

        return new ResolvedContext(
            BaseUrl: resolvedBaseUrl,
            TargetIdentityKey: targetIdentityKey,
            Profile: profileName,
            Token: token,
            AuthSource: authSource,
            OutputMode: options.OutputMode);
    }

    private string? SelectProfileForTarget(string? explicitTargetKey)
    {
        if (explicitTargetKey is null)
            return null;

        if (_profiles.TargetDefaults.TryGetValue(explicitTargetKey, out var profileName))
            return profileName;

        var matchingProfiles = _profiles.Profiles
            .Where(pair =>
            {
                var hostname = pair.Value.Hostname is null ? null : NormalizeHostname(pair.Value.Hostname);
                return hostname is not null && hostname == explicitTargetKey;
            })
            .Select(pair => pair.Key)
            .ToArray();

        return matchingProfiles.Length switch
        {
            0 => null,
            1 => matchingProfiles[0],
            _ => throw CliException.Usage(
                $"Multiple profiles match '{explicitTargetKey}'. Pass --profile or define a target default."),
        };
    }

    public static string NormalizeBaseUrl(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw CliException.Usage("Target is required.");

        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = $"https://{trimmed}";

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            throw CliException.Usage($"Invalid target URL '{raw}'.");

        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty,
        };

        if (builder.Uri.IsDefaultPort)
            builder.Port = -1;

        var value = builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return string.IsNullOrWhiteSpace(value) ? builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') : value;
    }

    public static string CanonicalTargetIdentity(string normalizedBaseUrl)
    {
        var uri = new Uri(normalizedBaseUrl);
        return NormalizeHostname(uri.Host);
    }

    public static string NormalizeHostname(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw CliException.Usage("Hostname is required.");
        return trimmed.ToLowerInvariant();
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        return candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
