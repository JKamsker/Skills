using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Accounts;

public sealed class AccountsAddSettings : DeviceCommandSettings
{
    [CommandOption("--hoster <NAME>")]
    [Description("Premium hoster name, for example ddownload.com.")]
    public string? Hoster { get; init; }

    [CommandOption("--username <NAME>")]
    [Description("Account username or email.")]
    public string? Username { get; init; }

    [CommandOption("--password <PASSWORD>")]
    [Description("Account password.")]
    public string? Password { get; init; }

    [CommandOption("--password-stdin")]
    [Description("Read the account password from stdin.")]
    public bool PasswordStdin { get; init; }
}

public sealed class AccountsAddCommand : DeviceApiCommand<AccountsAddSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public AccountsAddCommand(
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
        AccountsAddSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Hoster) || string.IsNullOrWhiteSpace(settings.Username))
            throw CliException.Usage("accounts add requires --hoster <name> --username <name> and exactly one password source.");

        var password = await SecretInput.ReadSecretAsync(
            settings.Password,
            settings.PasswordStdin,
            requireStdinInNonInteractiveMode: true,
            settings.Json,
            settings.Quiet,
            "accounts add requires exactly one of --password <password> or --password-stdin.",
            "Pipe the account password to stdin and re-run with --password-stdin.",
            "Password: ",
            cancellationToken);

        var plan = new MyJdRequestPlan(
            "accounts.add",
            "POST",
            "/accountsV2/addAccount",
            new Dictionary<string, object?>
            {
                ["hoster"] = settings.Hoster.Trim(),
                ["username"] = settings.Username.Trim(),
                ["password"] = password,
            },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            $"'accounts add' will add account '{settings.Username.Trim()}' for '{settings.Hoster.Trim()}'.",
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
