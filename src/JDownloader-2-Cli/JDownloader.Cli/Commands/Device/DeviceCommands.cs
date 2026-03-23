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

public sealed class GetDeviceCommand : DeviceApiCommand<DeviceNoArgSettings>
{
    public GetDeviceCommand(IProfileResolver profileResolver, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
        : base(profileResolver, outputRenderer, diagnosticLogger)
    {
    }

    protected override Task<CommandOutput> ExecuteCoreAsync(CommandContext context, DeviceNoArgSettings settings, ResolvedProfileContext resolved, CancellationToken cancellationToken)
    {
        return Task.FromResult(new CommandOutput(
            new
            {
                profile = resolved.ProfileName,
                device = resolved.Device is null ? null : new { resolved.Device.Id, resolved.Device.Name },
                resolved.DeviceSource,
            },
            [
                $"Profile: {resolved.ProfileName}",
                $"Device: {resolved.Device?.DisplayValue ?? "(none)"}",
                $"Device source: {resolved.DeviceSource ?? "(none)"}",
            ]));
    }
}

public sealed class UseDeviceCommand : AnonymousCommand<DeviceUseSettings>
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

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, DeviceUseSettings settings, CancellationToken cancellationToken)
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

public sealed class DevicePingCommand : FixedRequestPlanCommand
{
    public DevicePingCommand(IProfileResolver profileResolver, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger, IMyJdTransport transport, IConfirmationGuard confirmationGuard)
        : base(profileResolver, outputRenderer, diagnosticLogger, transport, confirmationGuard) { }

    protected override string Operation => "device.ping";
    protected override string Endpoint => "/device/ping";
}

public sealed class DeviceDirectInfoCommand : FixedRequestPlanCommand
{
    public DeviceDirectInfoCommand(IProfileResolver profileResolver, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger, IMyJdTransport transport, IConfirmationGuard confirmationGuard)
        : base(profileResolver, outputRenderer, diagnosticLogger, transport, confirmationGuard) { }

    protected override string Operation => "device.direct-info";
    protected override string Endpoint => "/device/getDirectConnectionInfos";
}
