using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Device;

public sealed class ListDevicesCommand : AnonymousCommand<NoArgSettings>
{
    private readonly IProfileResolver _profileResolver;
    private readonly IProfileStore _profileStore;

    public ListDevicesCommand(
        IProfileResolver profileResolver,
        IProfileStore profileStore,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileResolver = profileResolver;
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, NoArgSettings settings, CancellationToken cancellationToken)
    {
        var resolved = await _profileResolver.ResolveAsync(settings, requireDevice: false, cancellationToken);
        var config = await _profileStore.LoadAsync(cancellationToken);
        var profile = config.Profiles[resolved.ProfileName];
        var items = profile.KnownDevices.Select(device => new
        {
            device.Id,
            device.Name,
            isDefault = string.Equals(profile.DefaultDeviceId, device.Id, StringComparison.Ordinal)
                || string.Equals(profile.DefaultDeviceName, device.Name, StringComparison.OrdinalIgnoreCase),
            device.SeenAtUtc,
        }).ToList();

        var lines = items.Count == 0
            ? ["No devices recorded for this profile."]
            : items.Select(item => $"{item.Name} ({item.Id}){(item.isDefault ? " [default]" : string.Empty)}").ToArray();
        return new CommandOutput(items, lines);
    }
}
