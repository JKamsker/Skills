using System.ComponentModel;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
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

    public UseDeviceCommand(
        IProfileResolver profileResolver,
        IProfileStore profileStore,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileResolver = profileResolver;
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, UseDeviceSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Device))
            throw CliException.Usage("device use requires --device <id-or-name>.");

        var resolved = await _profileResolver.ResolveAsync(settings, requireDevice: false, cancellationToken);
        var config = await _profileStore.LoadAsync(cancellationToken);
        var profile = config.Profiles[resolved.ProfileName];

        var match = profile.KnownDevices.FirstOrDefault(device =>
            string.Equals(device.Id, settings.Device, StringComparison.Ordinal)
            || string.Equals(device.Name, settings.Device, StringComparison.Ordinal)
            || string.Equals(device.Name, settings.Device, StringComparison.OrdinalIgnoreCase));

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
}
