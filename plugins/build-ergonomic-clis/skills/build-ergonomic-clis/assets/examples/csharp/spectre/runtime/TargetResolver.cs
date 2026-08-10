using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace ExampleCli.Runtime;

// This example implements the **hostname-key** target identity mode (one valid target-identity choice).
// - Network operations use a normalized base URL (scheme/port/path may matter).
// - Credentials and defaults bind to the hostname key (lowercased hostname; IP addresses supported).
public enum AuthSource
{
    None,
    Flag,
    Environment,
    Profile,
}

public enum ResolutionSource
{
    Default,
    Flag,
    Environment,
    TargetDefault,
    SingleMatch,
    ActiveProfile,
    ProfileConfig,
}

public sealed record ProfileConfig(
    string? Hostname = null,
    string? BaseUrl = null);

public sealed record ResolvedContext(
    string BaseUrl,
    ResolutionSource BaseUrlSource,
    string TargetIdentityKey,
    string Profile,
    ResolutionSource ProfileSource,
    [property: JsonIgnore] string? Token,
    AuthSource AuthSource,
    OutputMode OutputMode,
    ResolutionSource OutputModeSource)
{
    public override string ToString()
        => $"ResolvedContext {{ BaseUrl = {BaseUrl}, BaseUrlSource = {BaseUrlSource}, TargetIdentityKey = {TargetIdentityKey}, Profile = {Profile}, ProfileSource = {ProfileSource}, Token = REDACTED, AuthSource = {AuthSource}, OutputMode = {OutputMode}, OutputModeSource = {OutputModeSource} }}";
}

public sealed record ResolvedContextSafe(
    string BaseUrl,
    ResolutionSource BaseUrlSource,
    string TargetIdentityKey,
    string Profile,
    ResolutionSource ProfileSource,
    AuthSource AuthSource,
    OutputMode OutputMode,
    ResolutionSource OutputModeSource);

public static class ResolvedContextExtensions
{
    public static ResolvedContextSafe ToSafe(this ResolvedContext context)
        => new(
            BaseUrl: context.BaseUrl,
            BaseUrlSource: context.BaseUrlSource,
            TargetIdentityKey: context.TargetIdentityKey,
            Profile: context.Profile,
            ProfileSource: context.ProfileSource,
            AuthSource: context.AuthSource,
            OutputMode: context.OutputMode,
            OutputModeSource: context.OutputModeSource);
}

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
        var (explicitProfile, explicitProfileSource) = FirstNonEmpty(
            (options.Profile, ResolutionSource.Flag),
            (Environment.GetEnvironmentVariable("EXAMPLE_PROFILE"), ResolutionSource.Environment));

        var (explicitBaseUrl, explicitBaseUrlSource) = FirstNonEmpty(
            (options.Host, ResolutionSource.Flag),
            (Environment.GetEnvironmentVariable("EXAMPLE_HOST"), ResolutionSource.Environment));

        explicitBaseUrl = explicitBaseUrl is null ? null : NormalizeBaseUrl(explicitBaseUrl);
        var explicitTargetKey = explicitBaseUrl is null ? null : DeriveHostnameIdentityKey(explicitBaseUrl);

        var (profileName, profileSource) = explicitProfile is not null
            ? (explicitProfile, explicitProfileSource ?? ResolutionSource.Flag)
            : SelectProfileForTarget(explicitTargetKey);

        if (profileName is null && explicitTargetKey is not null && !string.IsNullOrWhiteSpace(_profiles.ActiveProfile))
        {
            _profiles.Profiles.TryGetValue(_profiles.ActiveProfile!, out var activeProfile);
            var activeProfileTargetKey = ProfileTargetKey(activeProfile);
            if (activeProfileTargetKey == explicitTargetKey)
            {
                profileName = _profiles.ActiveProfile;
                profileSource = ResolutionSource.ActiveProfile;
            }
        }

        if (profileName is null && explicitTargetKey is null)
        {
            profileName = _profiles.ActiveProfile;
            profileSource = _profiles.ActiveProfile is null ? profileSource : ResolutionSource.ActiveProfile;
        }

        if (profileName is null)
        {
            profileName = "default";
            profileSource = ResolutionSource.Default;
        }

        _profiles.Profiles.TryGetValue(profileName, out var profile);
        if (profile is not null && !string.IsNullOrWhiteSpace(profile.BaseUrl) && !string.IsNullOrWhiteSpace(profile.Hostname))
        {
            var hostnameFromBaseUrl = DeriveHostnameIdentityKey(NormalizeBaseUrl(profile.BaseUrl!));
            var hostnameFromField = NormalizeHostname(profile.Hostname!);
            if (hostnameFromBaseUrl != hostnameFromField)
                throw CliException.Usage($"Profile '{profileName}' has mismatched Hostname ('{hostnameFromField}') and BaseUrl ('{NormalizeBaseUrl(profile.BaseUrl!)}').");
        }

        var profileTargetKey = ProfileTargetKey(profile);

        if (explicitTargetKey is not null && explicitProfile is not null && profileTargetKey is not null && profileTargetKey != explicitTargetKey)
            throw CliException.Usage($"Profile '{profileName}' is configured for '{profileTargetKey}', but the target is '{explicitTargetKey}'.");

        string resolvedBaseUrl;
        ResolutionSource resolvedBaseUrlSource;
        if (explicitBaseUrl is not null)
        {
            resolvedBaseUrl = explicitBaseUrl;
            resolvedBaseUrlSource = explicitBaseUrlSource ?? ResolutionSource.Flag;
        }
        else if (!string.IsNullOrWhiteSpace(profile?.BaseUrl))
        {
            resolvedBaseUrl = NormalizeBaseUrl(profile!.BaseUrl!);
            resolvedBaseUrlSource = ResolutionSource.ProfileConfig;
        }
        else
        {
            throw CliException.Usage("Target base URL is required. Pass --host/EXAMPLE_HOST or configure a profile base URL.");
        }

        var targetIdentityKey = DeriveHostnameIdentityKey(resolvedBaseUrl);

        var (tokenFromFlag, _) = FirstNonEmpty((options.Token, ResolutionSource.Flag));
        var (tokenFromEnv, _) = FirstNonEmpty((Environment.GetEnvironmentVariable("EXAMPLE_TOKEN"), ResolutionSource.Environment));
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
            BaseUrlSource: resolvedBaseUrlSource,
            TargetIdentityKey: targetIdentityKey,
            Profile: profileName,
            ProfileSource: profileSource,
            Token: token,
            AuthSource: authSource,
            OutputMode: options.OutputMode,
            OutputModeSource: options.Json ? ResolutionSource.Flag : ResolutionSource.Default);
    }

    private (string? ProfileName, ResolutionSource Source) SelectProfileForTarget(string? explicitTargetKey)
    {
        if (explicitTargetKey is null)
            return (null, ResolutionSource.Default);

        if (_profiles.TargetDefaults.TryGetValue(explicitTargetKey, out var profileName))
            return (profileName, ResolutionSource.TargetDefault);

        var matchingProfiles = _profiles.Profiles
            .Where(pair =>
            {
                var targetKey = ProfileTargetKey(pair.Value);
                return targetKey is not null && targetKey == explicitTargetKey;
            })
            .Select(pair => pair.Key)
            .ToArray();

        return matchingProfiles.Length switch
        {
            0 => (null, ResolutionSource.Default),
            1 => (matchingProfiles[0], ResolutionSource.SingleMatch),
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
            throw CliException.Usage("Invalid target URL.");

        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty,
            UserName = string.Empty,
            Password = string.Empty,
        };

        if (builder.Uri.IsDefaultPort)
            builder.Port = -1;

        var value = builder.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return string.IsNullOrWhiteSpace(value) ? builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/') : value;
    }

    public static string DeriveHostnameIdentityKey(string normalizedBaseUrl)
    {
        var uri = new Uri(normalizedBaseUrl);
        return NormalizeHostname(uri.Host);
    }

    public static string NormalizeHostname(string raw)
    {
        var trimmed = raw.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw CliException.Usage("Hostname is required.");

        if (trimmed.Contains("://", StringComparison.Ordinal) && Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            return uri.Host.ToLowerInvariant();

        return trimmed.ToLowerInvariant();
    }

    private static string? ProfileTargetKey(ProfileConfig? profile)
    {
        if (profile is null)
            return null;

        if (!string.IsNullOrWhiteSpace(profile.Hostname))
            return NormalizeHostname(profile.Hostname!);

        if (!string.IsNullOrWhiteSpace(profile.BaseUrl))
            return DeriveHostnameIdentityKey(NormalizeBaseUrl(profile.BaseUrl!));

        return null;
    }

    private static (string? Value, ResolutionSource? Source) FirstNonEmpty(params (string? Value, ResolutionSource Source)[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate.Value))
                return (candidate.Value, candidate.Source);
        }

        return (null, null);
    }
}
