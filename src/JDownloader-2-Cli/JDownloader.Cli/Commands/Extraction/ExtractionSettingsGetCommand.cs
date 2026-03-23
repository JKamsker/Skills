using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Extraction;

public sealed class ExtractionSettingsGetSettings : DeviceCommandSettings
{
    [CommandOption("--archive-id <ID>")]
    [Description("Repeatable archive identifier to fetch settings for.")]
    public string[] ArchiveIds { get; init; } = [];
}

public sealed class ExtractionSettingsGetCommand : DeviceApiCommand<ExtractionSettingsGetSettings>
{
    private readonly IMyJdTransport _transport;

    public ExtractionSettingsGetCommand(
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
        ExtractionSettingsGetSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        var plan = new MyJdRequestPlan(
            "extraction.settings.get",
            "POST",
            "/extraction/getArchiveSettings",
            new Dictionary<string, object?> { ["archiveIds"] = settings.ArchiveIds },
            null,
            false,
            false,
            resolved.Device?.Id);

        if (settings.DryRun)
            return RequestPlanCommandBase.BuildPreviewOutput(resolved, plan);

        if (settings.ArchiveIds.Length == 0)
            throw CliException.Usage("extraction settings get requires at least one --archive-id <id>.");

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
