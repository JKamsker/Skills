using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;

namespace JDownloader.Cli.Commands.Events;

public abstract class EventsCommandBase : FixedRequestPlanCommand
{
    protected EventsCommandBase(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
}
