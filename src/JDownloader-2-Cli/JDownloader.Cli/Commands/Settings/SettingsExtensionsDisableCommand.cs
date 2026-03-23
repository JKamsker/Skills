using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Settings;

public sealed class SettingsExtensionsDisableSettings : DeviceCommandSettings
{
    [CommandOption("--id <ID>")]
    [Description("Extension id to uninstall.")]
    public string? Id { get; init; }
}

public sealed class SettingsExtensionsDisableCommand : DeviceApiCommand<SettingsExtensionsDisableSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public SettingsExtensionsDisableCommand(
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
        SettingsExtensionsDisableSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Id))
            throw CliException.Usage("settings extensions disable requires --id <id>.");

        var plan = new MyJdRequestPlan(
            "settings.extensions.disable",
            "POST",
            "/extensions/uninstall",
            new Dictionary<string, object?> { ["id"] = settings.Id.Trim() },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            $"'settings extensions disable' will uninstall extension '{settings.Id.Trim()}'.",
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
