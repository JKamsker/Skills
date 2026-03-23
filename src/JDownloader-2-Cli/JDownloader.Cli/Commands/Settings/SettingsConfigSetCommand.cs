using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Settings;

public sealed class SettingsConfigSetSettings : DeviceCommandSettings
{
    [CommandOption("--interface-name <NAME>")]
    [Description("Config interface name.")]
    public string? InterfaceName { get; init; }

    [CommandOption("--storage <NAME>")]
    [Description("Config storage name. Omit for entries without a dedicated storage.")]
    public string? Storage { get; init; }

    [CommandOption("--key <KEY>")]
    [Description("Config key to set.")]
    public string? Key { get; init; }

    [CommandOption("--value <VALUE>")]
    [Description("String value to set.")]
    public string? Value { get; init; }

    [CommandOption("--value-json <JSON>")]
    [Description("Raw JSON value or @file for non-string values.")]
    public string? ValueJson { get; init; }
}

public sealed class SettingsConfigSetCommand : DeviceApiCommand<SettingsConfigSetSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public SettingsConfigSetCommand(
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
        SettingsConfigSetSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.InterfaceName) || string.IsNullOrWhiteSpace(settings.Key))
        {
            throw CliException.Usage("settings config set requires --interface-name <name> --key <key> and exactly one of --value <value> or --value-json <json>.");
        }

        var hasValue = settings.Value is not null;
        var hasValueJson = !string.IsNullOrWhiteSpace(settings.ValueJson);
        if (hasValue == hasValueJson)
        {
            throw CliException.Usage("settings config set requires exactly one of --value <value> or --value-json <json>.");
        }

        var value = hasValue ? settings.Value : JsonInput.ParseOptional(settings.ValueJson);
        var plan = new MyJdRequestPlan(
            "settings.config.set",
            "POST",
            "/config/set",
            new Dictionary<string, object?>
            {
                ["interfaceName"] = settings.InterfaceName.Trim(),
                ["storage"] = settings.Storage?.Trim() ?? string.Empty,
                ["key"] = settings.Key.Trim(),
                ["value"] = value,
            },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            $"'settings config set' will update '{settings.Key.Trim()}'.",
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
