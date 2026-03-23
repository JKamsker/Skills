using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Settings;

public sealed class SettingsConfigResetSettings : DeviceCommandSettings
{
    [CommandOption("--interface-name <NAME>")]
    [Description("Config interface name.")]
    public string? InterfaceName { get; init; }

    [CommandOption("--storage <NAME>")]
    [Description("Config storage name. Omit for entries without a dedicated storage.")]
    public string? Storage { get; init; }

    [CommandOption("--key <KEY>")]
    [Description("Config key to reset to its default value.")]
    public string? Key { get; init; }
}

public sealed class SettingsConfigResetCommand : DeviceApiCommand<SettingsConfigResetSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public SettingsConfigResetCommand(
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
        SettingsConfigResetSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.InterfaceName) || string.IsNullOrWhiteSpace(settings.Key))
        {
            throw CliException.Usage("settings config reset requires --interface-name <name> --key <key>.");
        }

        var plan = new MyJdRequestPlan(
            "settings.config.reset",
            "POST",
            "/config/reset",
            new Dictionary<string, object?>
            {
                ["interfaceName"] = settings.InterfaceName.Trim(),
                ["storage"] = settings.Storage?.Trim() ?? string.Empty,
                ["key"] = settings.Key.Trim(),
            },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            $"'settings config reset' will reset '{settings.Key.Trim()}' to its default value.",
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
