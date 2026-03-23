using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Downloads;

public sealed class DownloadsStopmarkSetSettings : DeviceCommandSettings
{
    [CommandOption("--link-id <ID>")]
    [Description("Download link id to stop at.")]
    public long? LinkId { get; init; }

    [CommandOption("--package-id <ID>")]
    [Description("Download package id to stop at.")]
    public long? PackageId { get; init; }
}

public sealed class DownloadsStopmarkSetCommand : DeviceApiCommand<DownloadsStopmarkSetSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public DownloadsStopmarkSetCommand(
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
        DownloadsStopmarkSetSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (settings.LinkId is null && settings.PackageId is null)
            throw CliException.Usage("downloads stopmark set requires --link-id <id> or --package-id <id>.");

        var plan = new MyJdRequestPlan(
            "downloads.stopmark.set",
            "POST",
            "/downloadsV2/setStopMark",
            new Dictionary<string, object?>
            {
                ["linkId"] = settings.LinkId,
                ["packageId"] = settings.PackageId,
            },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            "'downloads stopmark set' will update the stop mark.",
            () => Task.FromResult(RequestPlanCommandBase.BuildPreviewOutput(resolved, plan)));
        if (!proceed)
            return new CommandOutput(new { preview = true });

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
