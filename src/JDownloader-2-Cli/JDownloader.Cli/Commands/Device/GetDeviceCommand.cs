using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Device;

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
