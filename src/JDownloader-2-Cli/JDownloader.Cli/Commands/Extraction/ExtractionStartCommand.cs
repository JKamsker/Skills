using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Extraction;

public sealed class ExtractionStartSettings : DeviceCommandSettings
{
    [CommandOption("--link-id <ID>")]
    [Description("Repeatable link identifier to start extraction for.")]
    public long[] LinkIds { get; init; } = [];

    [CommandOption("--package-id <ID>")]
    [Description("Repeatable package identifier to start extraction for.")]
    public long[] PackageIds { get; init; } = [];
}

public sealed class ExtractionStartCommand : DeviceApiCommand<ExtractionStartSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public ExtractionStartCommand(
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
        ExtractionStartSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        var plan = new MyJdRequestPlan(
            "extraction.start",
            "POST",
            "/extraction/startExtractionNow",
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
            "'extraction start' will start extraction for the selected items.",
            () => Task.FromResult(RequestPlanCommandBase.BuildPreviewOutput(resolved, plan)));
        if (!proceed)
            return new CommandOutput(new { preview = true });

        if (settings.LinkIds.Length == 0 && settings.PackageIds.Length == 0)
            throw CliException.Usage("extraction start requires at least one --link-id <id> or --package-id <id>.");

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
