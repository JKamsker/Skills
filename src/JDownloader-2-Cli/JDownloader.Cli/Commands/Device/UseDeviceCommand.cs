using System.ComponentModel;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Device;

public sealed class UseDeviceSettings : GlobalSettings
{
    [CommandOption("--device-name <NAME>")]
    [Description("Optional friendly name when adding a new local device record.")]
    public string? DeviceName { get; init; }
}

public sealed class UseDeviceCommand : AnonymousCommand<UseDeviceSettings>
{
    private readonly IProfileResolver _profileResolver;
    private readonly IProfileStore _profileStore;
    private readonly IDeviceCatalog _deviceCatalog;

    public UseDeviceCommand(
        IProfileResolver profileResolver,
        IDeviceCatalog deviceCatalog,
        IProfileStore profileStore,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileResolver = profileResolver;
        _deviceCatalog = deviceCatalog;
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, UseDeviceSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Device))
            throw CliException.Usage("device use requires --device <id-or-name>.");

        var resolved = await _profileResolver.ResolveAsync(settings, requireDevice: false, cancellationToken);
        var config = await _profileStore.LoadAsync(cancellationToken);
        var profile = config.Profiles[resolved.ProfileName];

        var match = FindMatch(profile, settings.Device);
        if (match is null && !string.IsNullOrWhiteSpace(profile.AccountEmail))
        {
            try
            {
                await _deviceCatalog.SyncAsync(resolved.ProfileName, profile.AccountEmail, resolved.TimeoutSeconds, cancellationToken);
                config = await _profileStore.LoadAsync(cancellationToken);
                profile = config.Profiles[resolved.ProfileName];
                match = FindMatch(profile, settings.Device);
            }
            catch (CliException ex) when (ex.Kind is "not_authenticated" or "transport")
            {
                // keep the original ergonomic fallback and let the caller opt into a manual record
            }
        }

        if (match is null)
        {
            match = new KnownDeviceRecord
            {
                Id = settings.Device.Trim(),
                Name = settings.DeviceName?.Trim() ?? settings.Device.Trim(),
                SeenAtUtc = DateTimeOffset.UtcNow,
            };
            profile.KnownDevices.Add(match);
        }

        profile.DefaultDeviceId = match.Id;
        profile.DefaultDeviceName = match.Name;
        await _profileStore.SaveAsync(config, cancellationToken);

        return new CommandOutput(
            new { profile = resolved.ProfileName, device = new { match.Id, match.Name } },
            [$"Default device for profile '{resolved.ProfileName}' set to {match.Name} ({match.Id})."]);
    }

    private static KnownDeviceRecord? FindMatch(ProfileRecord profile, string lookup)
    {
        return profile.KnownDevices.FirstOrDefault(device =>
            string.Equals(device.Id, lookup, StringComparison.Ordinal)
            || string.Equals(device.Name, lookup, StringComparison.Ordinal)
            || string.Equals(device.Name, lookup, StringComparison.OrdinalIgnoreCase));
    }
}
