using JDownloader.Cli.Config;
using JDownloader.Cli.Transport;

namespace JDownloader.Cli.Runtime;

public sealed record ResolvedDevice(string Id, string Name)
{
    public string DisplayValue => $"{Name} ({Id})";
}

public sealed record ResolvedProfileContext(
    string ProfileName,
    string ProfileSource,
    string? AccountEmail,
    OutputMode OutputMode,
    string OutputSource,
    int TimeoutSeconds,
    string TimeoutSource,
    ResolvedDevice? Device,
    string? DeviceSource);

public interface IProfileResolver
{
    Task<ResolvedProfileContext> ResolveAsync(GlobalSettings settings, bool requireDevice, CancellationToken cancellationToken);
}

public sealed class ProfileResolver : IProfileResolver
{
    private readonly IProfileStore _profileStore;
    private readonly ICliEnvironment _environment;
    private readonly IDeviceCatalog _deviceCatalog;

    public ProfileResolver(IProfileStore profileStore, ICliEnvironment environment, IDeviceCatalog deviceCatalog)
    {
        _profileStore = profileStore;
        _environment = environment;
        _deviceCatalog = deviceCatalog;
    }

    public async Task<ResolvedProfileContext> ResolveAsync(GlobalSettings settings, bool requireDevice, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        var (profileName, profileSource) = ResolveProfileName(settings, config);
        config.Profiles.TryGetValue(profileName, out var profile);
        profile ??= new ProfileRecord();

        var (outputMode, outputSource) = ResolveOutputMode(settings, profile);
        var (timeoutSeconds, timeoutSource) = ResolveTimeout(settings, profile);
        var (device, deviceSource) = await ResolveDeviceAsync(
            settings,
            profile,
            profileName,
            timeoutSeconds,
            requireDevice,
            cancellationToken);

        return new ResolvedProfileContext(
            profileName,
            profileSource,
            profile.AccountEmail,
            outputMode,
            outputSource,
            timeoutSeconds,
            timeoutSource,
            device,
            deviceSource);
    }

    private (string ProfileName, string Source) ResolveProfileName(GlobalSettings settings, Jd2Config config)
    {
        if (!string.IsNullOrWhiteSpace(settings.Profile))
            return (settings.Profile.Trim(), "flag");

        var envProfile = _environment.GetEnvironmentVariable("JD2_PROFILE");
        if (!string.IsNullOrWhiteSpace(envProfile))
            return (envProfile.Trim(), "env");

        if (!string.IsNullOrWhiteSpace(config.DefaultProfile))
            return (config.DefaultProfile.Trim(), "config.defaultProfile");

        if (config.Profiles.Count == 1)
            return (config.Profiles.Keys.Single(), "single-profile-inference");

        throw CliException.Usage(
            "Profile is required because no default profile could be resolved.",
            "Pass --profile <name> or run 'jd2 auth profiles add <name>'.");
    }

    private (OutputMode OutputMode, string Source) ResolveOutputMode(GlobalSettings settings, ProfileRecord profile)
    {
        if (settings.Json)
            return (OutputMode.Json, "flag(--json)");

        if (!string.IsNullOrWhiteSpace(settings.Output))
        {
            return settings.Output.Trim().ToLowerInvariant() switch
            {
                "human" => (OutputMode.Human, "flag(--output)"),
                "json" => (OutputMode.Json, "flag(--output)"),
                _ => throw CliException.Usage("Unsupported output mode. Use 'human' or 'json'."),
            };
        }

        var envOutput = _environment.GetEnvironmentVariable("JD2_OUTPUT");
        if (!string.IsNullOrWhiteSpace(envOutput))
        {
            return envOutput.Trim().ToLowerInvariant() switch
            {
                "human" => (OutputMode.Human, "env"),
                "json" => (OutputMode.Json, "env"),
                _ => throw CliException.Usage("Unsupported JD2_OUTPUT value. Use 'human' or 'json'."),
            };
        }

        if (!string.IsNullOrWhiteSpace(profile.Output))
        {
            return profile.Output.Trim().ToLowerInvariant() switch
            {
                "human" => (OutputMode.Human, "profile"),
                "json" => (OutputMode.Json, "profile"),
                _ => throw CliException.Usage("Unsupported saved profile output mode."),
            };
        }

        return (OutputMode.Human, "default");
    }

    private (int TimeoutSeconds, string Source) ResolveTimeout(GlobalSettings settings, ProfileRecord profile)
    {
        if (settings.TimeoutSeconds is > 0)
            return (settings.TimeoutSeconds.Value, "flag");

        var envTimeout = _environment.GetEnvironmentVariable("JD2_TIMEOUT");
        if (int.TryParse(envTimeout, out var parsedEnvTimeout) && parsedEnvTimeout > 0)
            return (parsedEnvTimeout, "env");

        if (profile.TimeoutSeconds is > 0)
            return (profile.TimeoutSeconds.Value, "profile");

        return (30, "default");
    }

    private async Task<(ResolvedDevice? Device, string? Source)> ResolveDeviceAsync(
        GlobalSettings settings,
        ProfileRecord profile,
        string profileName,
        int timeoutSeconds,
        bool requireDevice,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(settings.Device))
            return await ResolveExplicitDeviceAsync(profile, settings.Device, "flag", profileName, timeoutSeconds, cancellationToken);

        var envDevice = _environment.GetEnvironmentVariable("JD2_DEVICE");
        if (!string.IsNullOrWhiteSpace(envDevice))
            return await ResolveExplicitDeviceAsync(profile, envDevice, "env", profileName, timeoutSeconds, cancellationToken);

        if (!string.IsNullOrWhiteSpace(profile.DefaultDeviceId) || !string.IsNullOrWhiteSpace(profile.DefaultDeviceName))
            return await ResolveExplicitDeviceAsync(
                profile,
                profile.DefaultDeviceId ?? profile.DefaultDeviceName!,
                "profile-default",
                profileName,
                timeoutSeconds,
                cancellationToken);

        if (profile.KnownDevices.Count == 1)
        {
            var device = profile.KnownDevices[0];
            return (new ResolvedDevice(device.Id, device.Name), "single-device-inference");
        }

        if (requireDevice && !string.IsNullOrWhiteSpace(profile.AccountEmail))
        {
            var liveDevices = await _deviceCatalog.SyncAsync(profileName, profile.AccountEmail, timeoutSeconds, cancellationToken);
            if (liveDevices.Count == 1)
                return (liveDevices[0], "live-single-device-inference");
        }

        if (requireDevice)
        {
            throw CliException.Usage(
                "Device is required because no default device could be resolved.",
                "Pass --device <id-or-name> or run 'jd2 device use --device <id-or-name>'.");
        }

        return (null, null);
    }

    private async Task<(ResolvedDevice Device, string Source)> ResolveExplicitDeviceAsync(
        ProfileRecord profile,
        string lookup,
        string source,
        string profileName,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        try
        {
            return (FindDevice(profile, lookup, source), source);
        }
        catch (CliException ex) when (ex.Kind == "not_found" && !string.IsNullOrWhiteSpace(profile.AccountEmail))
        {
            var liveDevices = await _deviceCatalog.SyncAsync(profileName, profile.AccountEmail, timeoutSeconds, cancellationToken);
            return (FindDevice(ToProfile(liveDevices), lookup, $"{source}/live"), $"{source}/live");
        }
    }

    private static ResolvedDevice FindDevice(ProfileRecord profile, string lookup, string source)
    {
        var trimmed = lookup.Trim();
        var byId = profile.KnownDevices.Where(device => string.Equals(device.Id, trimmed, StringComparison.Ordinal)).ToList();
        if (byId.Count == 1)
            return new ResolvedDevice(byId[0].Id, byId[0].Name);
        if (byId.Count > 1)
            throw CliException.Usage($"Device value '{trimmed}' is ambiguous in {source} resolution.");

        var byName = profile.KnownDevices.Where(device => string.Equals(device.Name, trimmed, StringComparison.Ordinal)).ToList();
        if (byName.Count == 1)
            return new ResolvedDevice(byName[0].Id, byName[0].Name);
        if (byName.Count > 1)
            throw CliException.Usage($"Device name '{trimmed}' is ambiguous.");

        var byCaseInsensitiveName = profile.KnownDevices
            .Where(device => string.Equals(device.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (byCaseInsensitiveName.Count == 1)
            return new ResolvedDevice(byCaseInsensitiveName[0].Id, byCaseInsensitiveName[0].Name);
        if (byCaseInsensitiveName.Count > 1)
            throw CliException.Usage($"Device name '{trimmed}' matches multiple devices.");

        if (!string.IsNullOrWhiteSpace(profile.DefaultDeviceId)
            && string.Equals(profile.DefaultDeviceId, trimmed, StringComparison.Ordinal))
        {
            return new ResolvedDevice(profile.DefaultDeviceId, profile.DefaultDeviceName ?? profile.DefaultDeviceId);
        }

        if (!string.IsNullOrWhiteSpace(profile.DefaultDeviceName)
            && string.Equals(profile.DefaultDeviceName, trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return new ResolvedDevice(profile.DefaultDeviceId ?? profile.DefaultDeviceName, profile.DefaultDeviceName);
        }

        throw CliException.NotFound(
            $"Device '{trimmed}' was not found in the selected profile.",
            "Run 'jd2 device list' or update the profile default device.");
    }

    private static ProfileRecord ToProfile(IReadOnlyList<ResolvedDevice> devices)
    {
        return new ProfileRecord
        {
            KnownDevices = devices.Select(device => new KnownDeviceRecord
            {
                Id = device.Id,
                Name = device.Name,
                SeenAtUtc = DateTimeOffset.UtcNow,
            }).ToList(),
        };
    }
}
