using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Extraction;

public sealed class ExtractionCancelSettings : DeviceCommandSettings
{
    [CommandOption("--controller-id <ID>")]
    [Description("Extraction controller id to cancel.")]
    public long? ControllerId { get; init; }
}

public sealed class ExtractionCancelCommand : DeviceApiCommand<ExtractionCancelSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public ExtractionCancelCommand(
        IProfileResolver profileResolver,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger,
        IMyJdTransport transport,
        IConfirmationGuard confirmationGuard)
        : base(profileResolver, outputRenderer, diagnosticLogger)
    {
        _transport = transport;
        _confirmationGuard = confirmationGuard;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(
        CommandContext context,
        ExtractionCancelSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        var plan = new MyJdRequestPlan(
            "extraction.cancel",
            "POST",
            "/extraction/cancelExtraction",
            new Dictionary<string, object?> { ["controllerId"] = settings.ControllerId },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            "'extraction cancel' will cancel the selected extraction controller.",
            () => Task.FromResult(RequestPlanCommandBase.BuildPreviewOutput(resolved, plan)));
        if (!proceed)
            return new CommandOutput(new { preview = true });

        if (settings.ControllerId is null)
            throw CliException.Usage("extraction cancel requires --controller-id <id>.");

        var result = await _transport.ExecuteAsync(resolved, plan, cancellationToken);
        return new CommandOutput(
            result.Data,
            JsonSerializer.Serialize(
                    result.Data,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true,
                    })
                .Split(Environment.NewLine),
            result.Warnings);
    }
}
