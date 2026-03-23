using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.System;

public sealed class SystemStorageSettings : DeviceCommandSettings
{
    [CommandOption("--path <PATH>")]
    [Description("Filesystem path to inspect on the remote JDownloader host.")]
    public string? Path { get; init; }
}

public sealed class SystemStorageCommand : DeviceApiCommand<SystemStorageSettings>
{
    private readonly IMyJdTransport _transport;

    public SystemStorageCommand(
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
        SystemStorageSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Path))
            throw CliException.Usage("system storage requires --path <path>.");

        var result = await _transport.ExecuteAsync(
            resolved,
            new MyJdRequestPlan(
                "system.storage",
                "POST",
                "/system/getStorageInfos",
                new Dictionary<string, object?> { ["path"] = settings.Path.Trim() },
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
