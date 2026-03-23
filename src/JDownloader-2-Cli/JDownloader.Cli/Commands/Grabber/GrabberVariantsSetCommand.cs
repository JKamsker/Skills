using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Grabber;

public sealed class GrabberVariantsSetSettings : DeviceCommandSettings
{
    [CommandOption("--link-id <ID>")]
    [Description("Linkgrabber link id to update.")]
    public long? LinkId { get; init; }

    [CommandOption("--variant-id <ID>")]
    [Description("Variant id to assign.")]
    public string? VariantId { get; init; }
}

public sealed class GrabberVariantsSetCommand : DeviceApiCommand<GrabberVariantsSetSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public GrabberVariantsSetCommand(
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
        GrabberVariantsSetSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (settings.LinkId is null || string.IsNullOrWhiteSpace(settings.VariantId))
            throw CliException.Usage("grabber variants set requires --link-id <id> --variant-id <id>.");

        var plan = new MyJdRequestPlan(
            "grabber.variants.set",
            "POST",
            "/linkgrabberv2/setVariant",
            new Dictionary<string, object?>
            {
                ["linkId"] = settings.LinkId.Value,
                ["variantId"] = settings.VariantId.Trim(),
            },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            $"'grabber variants set' will assign variant '{settings.VariantId.Trim()}' to link {settings.LinkId.Value}.",
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
