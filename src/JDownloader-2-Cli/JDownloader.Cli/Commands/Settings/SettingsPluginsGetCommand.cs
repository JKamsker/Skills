using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Settings;

public sealed class SettingsPluginsGetSettings : DeviceCommandSettings
{
    [CommandOption("--interface-name <NAME>")]
    [Description("Plugin config interface name.")]
    public string? InterfaceName { get; init; }

    [CommandOption("--display-name <NAME>")]
    [Description("Plugin display name.")]
    public string? DisplayName { get; init; }

    [CommandOption("--key <KEY>")]
    [Description("Plugin config key.")]
    public string? Key { get; init; }
}

public sealed class SettingsPluginsGetCommand : DeviceApiCommand<SettingsPluginsGetSettings>
{
    private readonly IMyJdTransport _transport;

    public SettingsPluginsGetCommand(
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
        SettingsPluginsGetSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.InterfaceName)
            || string.IsNullOrWhiteSpace(settings.DisplayName)
            || string.IsNullOrWhiteSpace(settings.Key))
        {
            throw CliException.Usage("settings plugins get requires --interface-name <name> --display-name <name> --key <key>.");
        }

        var result = await _transport.ExecuteAsync(
            resolved,
            new MyJdRequestPlan(
                "settings.plugins.get",
                "POST",
                "/plugins/get",
                new Dictionary<string, object?>
                {
                    ["interfaceName"] = settings.InterfaceName.Trim(),
                    ["displayName"] = settings.DisplayName.Trim(),
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
