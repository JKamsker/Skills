using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Extraction;

public sealed class ExtractionInfoSettings : DeviceCommandSettings
{
    [CommandOption("--link-id <ID>")]
    [Description("Repeatable download link identifier to inspect extraction info for.")]
    public long[] LinkIds { get; init; } = [];

    [CommandOption("--package-id <ID>")]
    [Description("Repeatable package identifier to inspect extraction info for.")]
    public long[] PackageIds { get; init; } = [];
}

public sealed class ExtractionInfoCommand : DeviceApiCommand<ExtractionInfoSettings>
{
    private readonly IMyJdTransport _transport;

    public ExtractionInfoCommand(
        IProfileResolver profileResolver,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger,
        IMyJdTransport transport)
        : base(profileResolver, outputRenderer, diagnosticLogger)
    {
        _transport = transport;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(
        CommandContext context,
        ExtractionInfoSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        var plan = new MyJdRequestPlan(
            "extraction.info",
            "POST",
            "/extraction/getArchiveInfo",
            new Dictionary<string, object?>
            {
                ["linkIds"] = settings.LinkIds,
                ["packageIds"] = settings.PackageIds,
            },
            null,
            false,
            false,
            resolved.Device?.Id);

        if (settings.DryRun)
            return RequestPlanCommandBase.BuildPreviewOutput(resolved, plan);

        if (settings.LinkIds.Length == 0 && settings.PackageIds.Length == 0)
            throw CliException.Usage("extraction info requires at least one --link-id <id> or --package-id <id>.");

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
