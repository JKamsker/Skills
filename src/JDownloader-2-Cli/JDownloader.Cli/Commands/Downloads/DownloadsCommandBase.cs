using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;

namespace JDownloader.Cli.Commands.Downloads;

public abstract class DownloadsCommandBase : FixedRequestPlanCommand
{
    protected DownloadsCommandBase(IProfileResolver profileResolver, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger, IMyJdTransport transport, IConfirmationGuard confirmationGuard)
        : base(profileResolver, outputRenderer, diagnosticLogger, transport, confirmationGuard) { }
}
