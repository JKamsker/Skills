using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Downloads;

public sealed class DownloadsLinksRemoveSettings : DeviceCommandSettings
{
    [CommandOption("--link-id <ID>")]
    [Description("Repeatable download link identifier to remove.")]
    public long[] LinkIds { get; init; } = [];

    [CommandOption("--package-id <ID>")]
    [Description("Repeatable package identifier whose links should be removed.")]
    public long[] PackageIds { get; init; } = [];
}

public sealed class DownloadsLinksRemoveCommand : DeviceApiCommand<DownloadsLinksRemoveSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public DownloadsLinksRemoveCommand(
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
        DownloadsLinksRemoveSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        var plan = new MyJdRequestPlan(
            "downloads.links.remove",
            "POST",
            "/downloadsV2/removeLinks",
            new Dictionary<string, object?>
            {
                ["linkIds"] = settings.LinkIds,
                ["packageIds"] = settings.PackageIds,
            },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            "'downloads links remove' will remove selected download links.",
            () => Task.FromResult(RequestPlanCommandBase.BuildPreviewOutput(resolved, plan)));
        if (!proceed)
            return new CommandOutput(new { preview = true });

        if (settings.LinkIds.Length == 0 && settings.PackageIds.Length == 0)
            throw CliException.Usage("downloads links remove requires at least one --link-id <id> or --package-id <id>.");

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
