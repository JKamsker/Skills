using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Grabber;

public sealed class GrabberVariantsListSettings : DeviceCommandSettings
{
    [CommandOption("--link-id <ID>")]
    [Description("Linkgrabber link id to inspect variants for.")]
    public long? LinkId { get; init; }
}

public sealed class GrabberVariantsListCommand : DeviceApiCommand<GrabberVariantsListSettings>
{
    private readonly IMyJdTransport _transport;

    public GrabberVariantsListCommand(
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
        GrabberVariantsListSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (settings.LinkId is null)
            throw CliException.Usage("grabber variants list requires --link-id <id>.");

        var result = await _transport.ExecuteAsync(
            resolved,
            new MyJdRequestPlan(
                "grabber.variants.list",
                "POST",
                "/linkgrabberv2/getVariants",
                new Dictionary<string, object?> { ["linkId"] = settings.LinkId.Value },
                null,
                false,
                false,
                resolved.Device?.Id),
            cancellationToken);

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
