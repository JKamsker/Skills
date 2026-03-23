using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;

namespace JDownloader.Cli.Commands.Extraction;

public abstract class ExtractionCommandBase : FixedRequestPlanCommand
{
    protected ExtractionCommandBase(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
}

public sealed class ExtractionQueueCommand : ExtractionCommandBase { public ExtractionQueueCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "extraction.queue"; protected override string Endpoint => "/extraction/getArchiveInfo"; }
public sealed class ExtractionInfoCommand : ExtractionCommandBase { public ExtractionInfoCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "extraction.info"; protected override string Endpoint => "/extraction/getArchiveInfo"; }
public sealed class ExtractionStartCommand : ExtractionCommandBase { public ExtractionStartCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "extraction.start"; protected override string Endpoint => "/extraction/startExtractionNow"; protected override bool Destructive => true; }
public sealed class ExtractionCancelCommand : ExtractionCommandBase { public ExtractionCancelCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "extraction.cancel"; protected override string Endpoint => "/extraction/cancelExtraction"; protected override bool Destructive => true; }
public sealed class ExtractionAddPasswordCommand : ExtractionCommandBase { public ExtractionAddPasswordCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "extraction.add-password"; protected override string Endpoint => "/extraction/addArchivePassword"; protected override bool Destructive => true; }
public sealed class ExtractionSettingsGetCommand : ExtractionCommandBase { public ExtractionSettingsGetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "extraction.settings.get"; protected override string Endpoint => "/extraction/getArchiveSettings"; }
public sealed class ExtractionSettingsSetCommand : ExtractionCommandBase { public ExtractionSettingsSetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "extraction.settings.set"; protected override string Endpoint => "/extraction/setArchiveSettings"; protected override bool Destructive => true; }
