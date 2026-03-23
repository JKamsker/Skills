using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;

namespace JDownloader.Cli.Commands.Downloads;

public abstract class DownloadsCommandBase : FixedRequestPlanCommand
{
    protected DownloadsCommandBase(IProfileResolver profileResolver, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger, IMyJdTransport transport, IConfirmationGuard confirmationGuard)
        : base(profileResolver, outputRenderer, diagnosticLogger, transport, confirmationGuard) { }
}

public sealed class DownloadsStatusCommand : DownloadsCommandBase
{
    public DownloadsStatusCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.status";
    protected override string Endpoint => "/downloadcontroller/getCurrentState";
}

public sealed class DownloadsSpeedCommand : DownloadsCommandBase
{
    public DownloadsSpeedCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.speed";
    protected override string Endpoint => "/downloadcontroller/getSpeedInBps";
}

public sealed class DownloadsStartCommand : DownloadsCommandBase
{
    public DownloadsStartCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.start";
    protected override string Endpoint => "/downloadcontroller/start";
    protected override bool Destructive => true;
}

public sealed class DownloadsStopCommand : DownloadsCommandBase
{
    public DownloadsStopCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.stop";
    protected override string Endpoint => "/downloadcontroller/stop";
    protected override bool Destructive => true;
}

public sealed class DownloadsPauseCommand : DownloadsCommandBase
{
    public DownloadsPauseCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.pause";
    protected override string Endpoint => "/downloadcontroller/pause";
    protected override bool Destructive => true;
}

public sealed class DownloadsLinksListCommand : DownloadsCommandBase
{
    public DownloadsLinksListCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.links.list";
    protected override string Endpoint => "/downloadsV2/queryLinks";
}

public sealed class DownloadsLinksRemoveCommand : DownloadsCommandBase
{
    public DownloadsLinksRemoveCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.links.remove";
    protected override string Endpoint => "/downloadsV2/removeLinks";
    protected override bool Destructive => true;
}

public sealed class DownloadsPackagesListCommand : DownloadsCommandBase
{
    public DownloadsPackagesListCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.packages.list";
    protected override string Endpoint => "/downloadsV2/queryPackages";
}

public sealed class DownloadsPackagesRemoveCommand : DownloadsCommandBase
{
    public DownloadsPackagesRemoveCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.packages.remove";
    protected override string Endpoint => "/downloadsV2/removePackages";
    protected override bool Destructive => true;
}

public sealed class DownloadsStopmarkGetCommand : DownloadsCommandBase
{
    public DownloadsStopmarkGetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.stopmark.get";
    protected override string Endpoint => "/downloadsV2/getStopMark";
}

public sealed class DownloadsStopmarkSetCommand : DownloadsCommandBase
{
    public DownloadsStopmarkSetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.stopmark.set";
    protected override string Endpoint => "/downloadsV2/setStopMark";
    protected override bool Destructive => true;
}

public sealed class DownloadsStopmarkClearCommand : DownloadsCommandBase
{
    public DownloadsStopmarkClearCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
    protected override string Operation => "downloads.stopmark.clear";
    protected override string Endpoint => "/downloadsV2/clearStopMark";
    protected override bool Destructive => true;
}
