using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;

namespace JDownloader.Cli.Commands.Device;

public sealed class DeviceDirectInfoCommand : FixedRequestPlanCommand
{
    public DeviceDirectInfoCommand(IProfileResolver profileResolver, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger, IMyJdTransport transport, IConfirmationGuard confirmationGuard)
        : base(profileResolver, outputRenderer, diagnosticLogger, transport, confirmationGuard) { }

    protected override string Operation => "device.direct-info";
    protected override string Endpoint => "/device/getDirectConnectionInfos";
}
