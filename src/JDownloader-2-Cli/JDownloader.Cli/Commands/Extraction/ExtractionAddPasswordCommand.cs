using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Extraction;

public sealed class ExtractionAddPasswordSettings : DeviceCommandSettings
{
    [CommandOption("--password <PASSWORD>")]
    [Description("Archive password to add.")]
    public string? Password { get; init; }

    [CommandOption("--password-stdin")]
    [Description("Read the archive password from stdin.")]
    public bool PasswordStdin { get; init; }
}

public sealed class ExtractionAddPasswordCommand : DeviceApiCommand<ExtractionAddPasswordSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public ExtractionAddPasswordCommand(
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
        ExtractionAddPasswordSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        var password = await SecretInput.ReadSecretAsync(
            settings.Password,
            settings.PasswordStdin,
            requireStdinInNonInteractiveMode: true,
            settings.Json,
            settings.Quiet,
            "extraction add-password requires exactly one of --password <password> or --password-stdin.",
            "Pipe the archive password to stdin and re-run with --password-stdin.",
            "Password: ",
            cancellationToken);

        var plan = new MyJdRequestPlan(
            "extraction.add-password",
            "POST",
            "/extraction/addArchivePassword",
            new Dictionary<string, object?> { ["password"] = password },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            "'extraction add-password' will add an archive password to JDownloader.",
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
