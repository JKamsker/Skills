using System.ComponentModel;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Advanced;

public sealed class RawRequestSettings : DeviceCommandSettings
{
    [CommandArgument(0, "<PATH>")]
    public required string Path { get; init; }

    [CommandOption("--method <METHOD>")]
    [Description("HTTP method to plan. Defaults to POST.")]
    public string? Method { get; init; }

    [CommandOption("--query-json <JSON>")]
    [Description("Raw query JSON or @file.")]
    public string? QueryJson { get; init; }

    [CommandOption("--body-json <JSON>")]
    [Description("Raw body JSON or @file.")]
    public string? BodyJson { get; init; }

    [CommandOption("--output-file <PATH>")]
    [Description("Destination for binary response modes.")]
    public string? OutputFile { get; init; }
}

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
            throw CliException.Usage("Binary-producing raw requests require --output-file and do not stream raw bytes to stdout JSON.");

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
