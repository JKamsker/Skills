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

public sealed record ProfileConfig(
    string? Hostname = null,
    string? BaseUrl = null);

public sealed record ResolvedContext(
    string BaseUrl,
    string TargetIdentityKey,
    string Profile,
    [property: JsonIgnore] string? Token,
    AuthSource AuthSource,
    OutputMode OutputMode)
{
    public override string ToString()
        => $"ResolvedContext {{ BaseUrl = {BaseUrl}, TargetIdentityKey = {TargetIdentityKey}, Profile = {Profile}, Token = REDACTED, AuthSource = {AuthSource}, OutputMode = {OutputMode} }}";
}

public sealed record ResolvedContextSafe(
    string BaseUrl,
    string TargetIdentityKey,
    string Profile,
    AuthSource AuthSource,
    OutputMode OutputMode);

public static class ResolvedContextExtensions
{
    public static ResolvedContextSafe ToSafe(this ResolvedContext context)
        => new(
            BaseUrl: context.BaseUrl,
            TargetIdentityKey: context.TargetIdentityKey,
            Profile: context.Profile,
            AuthSource: context.AuthSource,
            OutputMode: context.OutputMode);
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
        var explicitProfile = FirstNonEmpty(
            options.Profile,
            Environment.GetEnvironmentVariable("EXAMPLE_PROFILE"));

        var explicitBaseUrl = FirstNonEmpty(
            options.Host,
            Environment.GetEnvironmentVariable("EXAMPLE_HOST"));

        explicitBaseUrl = explicitBaseUrl is null ? null : NormalizeBaseUrl(explicitBaseUrl);
        var explicitTargetKey = explicitBaseUrl is null ? null : DeriveHostnameIdentityKey(explicitBaseUrl);

        string? profileName = explicitProfile
            ?? SelectProfileForTarget(explicitTargetKey);

        if (profileName is null && explicitTargetKey is not null && !string.IsNullOrWhiteSpace(_profiles.ActiveProfile))
        {
            _profiles.Profiles.TryGetValue(_profiles.ActiveProfile!, out var activeProfile);
            var activeProfileTargetKey = ProfileTargetKey(activeProfile);
            if (activeProfileTargetKey == explicitTargetKey)
                profileName = _profiles.ActiveProfile;
        }

        if (profileName is null && explicitTargetKey is null)
            profileName = _profiles.ActiveProfile;

        profileName ??= "default";

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
        if (explicitBaseUrl is not null)
        {
            resolvedBaseUrl = explicitBaseUrl;
        }
        else if (!string.IsNullOrWhiteSpace(profile?.BaseUrl))
        {
            resolvedBaseUrl = NormalizeBaseUrl(profile!.BaseUrl!);
        }
        else
        {
            throw CliException.Usage("Target base URL is required. Pass --host/EXAMPLE_HOST or configure a profile base URL.");
        }

        var targetIdentityKey = DeriveHostnameIdentityKey(resolvedBaseUrl);

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
                var targetKey = ProfileTargetKey(pair.Value);
                return targetKey is not null && targetKey == explicitTargetKey;
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

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        return candidates.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
