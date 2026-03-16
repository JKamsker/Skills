using System.Linq;

namespace ExampleCli.Runtime;

public enum AuthSource
{
    None,
    Flag,
    Environment,
    Profile,
}

public sealed record ProfileConfig(
    string Name,
    string? Host = null,
    string? Token = null,
    OutputMode? Output = null);

public sealed record ResolvedContext(
    string Host,
    string CanonicalHostKey,
    string Profile,
    string? Token,
    AuthSource AuthSource,
    OutputMode OutputMode);

public interface IProfileStore
{
    string? ActiveProfile { get; }
    IReadOnlyDictionary<string, ProfileConfig> Profiles { get; }
    IReadOnlyDictionary<string, string> HostDefaults { get; }
}

public sealed class TargetResolver
{
    private readonly IProfileStore _profiles;

    public TargetResolver(IProfileStore profiles)
    {
        _profiles = profiles;
    }

    public ResolvedContext Resolve(GlobalOptions options)
    {
        var explicitProfile = FirstNonEmpty(
            options.Profile,
            Environment.GetEnvironmentVariable("EXAMPLE_PROFILE"));

        var explicitHost = FirstNonEmpty(
            options.Host,
            Environment.GetEnvironmentVariable("EXAMPLE_HOST"));

        explicitHost = explicitHost is null ? null : NormalizeHost(explicitHost);

        var profileName = explicitProfile
            ?? SelectProfileForHost(explicitHost)
            ?? _profiles.ActiveProfile
            ?? "default";

        _profiles.Profiles.TryGetValue(profileName, out var profile);
        var profileHost = profile?.Host is null ? null : NormalizeHost(profile.Host);

        string resolvedHost;
        if (explicitHost is not null)
        {
            if (explicitProfile is not null && profileHost is not null && CanonicalHostKey(profileHost) != CanonicalHostKey(explicitHost))
                throw CliException.Usage($"Profile '{profileName}' is configured for '{profileHost}', but the target host is '{explicitHost}'.");

            resolvedHost = explicitHost;
        }
        else if (profileHost is not null)
        {
            resolvedHost = profileHost;
        }
        else
        {
            resolvedHost = "https://api.example.test";
        }

        var canonicalHostKey = CanonicalHostKey(resolvedHost);
        var profileMatchesHost = profileHost is not null && CanonicalHostKey(profileHost) == canonicalHostKey;

        var tokenFromFlag = FirstNonEmpty(options.Token);
        var tokenFromEnv = FirstNonEmpty(Environment.GetEnvironmentVariable("EXAMPLE_TOKEN"));
        var token = tokenFromFlag
            ?? tokenFromEnv
            ?? (profileMatchesHost ? profile?.Token : null);

        var authSource = tokenFromFlag is not null
            ? AuthSource.Flag
            : tokenFromEnv is not null
                ? AuthSource.Environment
                : token is not null
                    ? AuthSource.Profile
                    : AuthSource.None;

        return new ResolvedContext(
            Host: resolvedHost,
            CanonicalHostKey: canonicalHostKey,
            Profile: profileName,
            Token: token,
            AuthSource: authSource,
            OutputMode: options.OutputMode);
    }

    private string? SelectProfileForHost(string? explicitHost)
    {
        if (explicitHost is null)
            return null;

        var hostKey = CanonicalHostKey(explicitHost);

        if (_profiles.HostDefaults.TryGetValue(hostKey, out var profileName))
            return profileName;

        var matchingProfiles = _profiles.Profiles
            .Where(pair => pair.Value.Host is not null && CanonicalHostKey(pair.Value.Host) == hostKey)
            .Select(pair => pair.Key)
            .ToArray();

        return matchingProfiles.Length switch
        {
            0 => null,
            1 => matchingProfiles[0],
            _ => throw CliException.Usage(
                $"Multiple profiles match '{explicitHost}'. Pass --profile or define a host default."),
        };
    }

    public static string NormalizeHost(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw CliException.Usage("Host is required.");

        if (!trimmed.Contains("://", StringComparison.Ordinal))
            trimmed = $"https://{trimmed}";

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            throw CliException.Usage($"Invalid host URL '{raw}'.");

        var builder = new UriBuilder(uri)
        {
            Path = string.Empty,
            Query = string.Empty,
            Fragment = string.Empty,
        };

        if (builder.Uri.IsDefaultPort)
            builder.Port = -1;

        return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    public static string CanonicalHostKey(string raw)
    {
        var uri = new Uri(NormalizeHost(raw));
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        return $"{uri.Scheme.ToLowerInvariant()}://{uri.Host.ToLowerInvariant()}{port}";
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        return candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
