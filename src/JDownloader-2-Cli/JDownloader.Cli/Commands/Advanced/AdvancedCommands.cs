using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Advanced;

public abstract class AdvancedCommandBase : FixedRequestPlanCommand
{
    protected AdvancedCommandBase(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { }
}

public sealed class AdvancedContentIconCommand : AdvancedCommandBase { public AdvancedContentIconCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.content.icon"; protected override string Endpoint => "/contentV2/getIcon"; protected override bool ProducesBinary => true; }
public sealed class AdvancedContentFavIconCommand : AdvancedCommandBase { public AdvancedContentFavIconCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.content.favicon"; protected override string Endpoint => "/contentV2/getFavIcon"; protected override bool ProducesBinary => true; }
public sealed class AdvancedContentFileIconCommand : AdvancedCommandBase { public AdvancedContentFileIconCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.content.file-icon"; protected override string Endpoint => "/contentV2/getFileIcon"; protected override bool ProducesBinary => true; }
public sealed class AdvancedContentDescribeIconCommand : AdvancedCommandBase { public AdvancedContentDescribeIconCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.content.describe"; protected override string Endpoint => "/contentV2/getIconDescription"; }
public sealed class AdvancedDialogsListCommand : AdvancedCommandBase { public AdvancedDialogsListCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.dialogs.list"; protected override string Endpoint => "/dialogs/list"; }
public sealed class AdvancedDialogsGetCommand : AdvancedCommandBase { public AdvancedDialogsGetCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.dialogs.get"; protected override string Endpoint => "/dialogs/get"; }
public sealed class AdvancedDialogsAnswerCommand : AdvancedCommandBase { public AdvancedDialogsAnswerCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.dialogs.answer"; protected override string Endpoint => "/dialogs/answer"; protected override bool Destructive => true; }
public sealed class AdvancedDialogsTypeInfoCommand : AdvancedCommandBase { public AdvancedDialogsTypeInfoCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.dialogs.type-info"; protected override string Endpoint => "/dialogs/getTypeInfo"; }
public sealed class AdvancedUiRefreshCommand : AdvancedCommandBase { public AdvancedUiRefreshCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.ui.refresh"; protected override string Endpoint => "/jd/refreshPlugins"; protected override bool Destructive => true; }
public sealed class AdvancedUiFocusCommand : AdvancedCommandBase { public AdvancedUiFocusCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.ui.focus"; protected override string Endpoint => "/jd/doSomethingCool"; protected override bool Destructive => true; }
public sealed class AdvancedIngestCnlCommand : AdvancedCommandBase { public AdvancedIngestCnlCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.ingest.cnl"; protected override string Endpoint => "/flash/add"; protected override bool Destructive => true; }
public sealed class AdvancedIngestFlashCommand : AdvancedCommandBase { public AdvancedIngestFlashCommand(IProfileResolver a, IOutputRenderer b, IDiagnosticLogger c, IMyJdTransport d, IConfirmationGuard e) : base(a, b, c, d, e) { } protected override string Operation => "advanced.ingest.flash"; protected override string Endpoint => "/flashgot"; protected override bool Destructive => true; }

public sealed class AdvancedRawRequestCommand : DeviceApiCommand<RawRequestSettings>
{
    private readonly IMyJdTransport _transport;

    public AdvancedRawRequestCommand(IProfileResolver profileResolver, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger, IMyJdTransport transport)
        : base(profileResolver, outputRenderer, diagnosticLogger)
    {
        _transport = transport;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, RawRequestSettings settings, ResolvedProfileContext resolved, CancellationToken cancellationToken)
    {
        var producesBinary = !string.IsNullOrWhiteSpace(settings.OutputFile);
        if (resolved.OutputMode == OutputMode.Json && producesBinary)
        {
            throw CliException.Usage("Binary-producing raw requests require --output-file and do not stream raw bytes to stdout JSON.");
        }

        var plan = new MyJdRequestPlan(
            "advanced.raw.request",
            string.IsNullOrWhiteSpace(settings.Method) ? "POST" : settings.Method.Trim().ToUpperInvariant(),
            settings.Path,
            JsonInput.ParseOptional(settings.QueryJson),
            JsonInput.ParseOptional(settings.BodyJson),
            Destructive: false,
            ProducesBinary: producesBinary,
            resolved.Device?.Id);

        if (settings.DryRun)
            return RequestPlanCommandBase.BuildPreviewOutput(resolved, plan);

        var result = await _transport.ExecuteAsync(resolved, plan, cancellationToken);
        return new CommandOutput(
            result.Data,
            [
                $"Path: {plan.Endpoint}",
                $"Method: {plan.Method}",
                $"Profile: {resolved.ProfileName}",
                $"Device: {resolved.Device?.DisplayValue ?? "(none)"}",
            ],
            result.Warnings);
    }
}
