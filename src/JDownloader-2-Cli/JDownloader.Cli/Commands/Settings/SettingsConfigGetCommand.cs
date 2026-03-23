using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Settings;

public sealed class SettingsConfigGetSettings : DeviceCommandSettings
{
    [CommandOption("--interface-name <NAME>")]
    [Description("Config interface name.")]
    public string? InterfaceName { get; init; }

    [CommandOption("--storage <NAME>")]
    [Description("Config storage name. Omit for entries without a dedicated storage.")]
    public string? Storage { get; init; }

    [CommandOption("--key <KEY>")]
    [Description("Config key.")]
    public string? Key { get; init; }
}

public sealed class SettingsConfigGetCommand : DeviceApiCommand<SettingsConfigGetSettings>
{
    private readonly IMyJdTransport _transport;

    public SettingsConfigGetCommand(
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
        SettingsConfigGetSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.InterfaceName) || string.IsNullOrWhiteSpace(settings.Key))
        {
            throw CliException.Usage("settings config get requires --interface-name <name> --key <key>.");
        }

        var result = await _transport.ExecuteAsync(
            resolved,
            new MyJdRequestPlan(
                "settings.config.get",
                "POST",
                "/config/get",
                new Dictionary<string, object?>
                {
                    ["interfaceName"] = settings.InterfaceName.Trim(),
                    ["storage"] = settings.Storage?.Trim() ?? string.Empty,
                    ["key"] = settings.Key.Trim(),
                },
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
