using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Device;

public sealed class ListDevicesCommand : AnonymousCommand<NoArgSettings>
{
    private readonly IProfileResolver _profileResolver;
    private readonly IProfileStore _profileStore;
    private readonly IDeviceCatalog _deviceCatalog;

    public ListDevicesCommand(
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

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, NoArgSettings settings, CancellationToken cancellationToken)
    {
        var resolved = await _profileResolver.ResolveAsync(settings, requireDevice: false, cancellationToken);
        var warnings = new List<string>();
        var config = await _profileStore.LoadAsync(cancellationToken);
        var profile = config.Profiles[resolved.ProfileName];

        if (!string.IsNullOrWhiteSpace(profile.AccountEmail))
        {
            try
            {
                await _deviceCatalog.SyncAsync(resolved.ProfileName, profile.AccountEmail, resolved.TimeoutSeconds, cancellationToken);
                config = await _profileStore.LoadAsync(cancellationToken);
                profile = config.Profiles[resolved.ProfileName];
            }
            catch (CliException ex) when ((ex.Kind == "not_authenticated" || ex.Kind == "transport") && profile.KnownDevices.Count > 0)
            {
                warnings.Add(ex.Message);
            }
        }

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
        return new CommandOutput(items, lines, warnings.Count == 0 ? null : warnings);
    }
}
