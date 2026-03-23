using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Accounts;

public sealed class AccountsUpdateSettings : DeviceCommandSettings
{
    [CommandOption("--account-id <ID>")]
    [Description("Account identifier to update.")]
    public long? AccountId { get; init; }

    [CommandOption("--username <NAME>")]
    [Description("Updated account username or email.")]
    public string? Username { get; init; }

    [CommandOption("--password <PASSWORD>")]
    [Description("Updated account password.")]
    public string? Password { get; init; }

    [CommandOption("--password-stdin")]
    [Description("Read the updated password from stdin.")]
    public bool PasswordStdin { get; init; }
}

public sealed class AccountsUpdateCommand : DeviceApiCommand<AccountsUpdateSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public AccountsUpdateCommand(
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
        AccountsUpdateSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (settings.AccountId is null || string.IsNullOrWhiteSpace(settings.Username))
        {
            throw CliException.Usage("accounts update requires --account-id <id> --username <name> and exactly one password source.");
        }

        var password = await SecretInput.ReadSecretAsync(
            settings.Password,
            settings.PasswordStdin,
            requireStdinInNonInteractiveMode: true,
            settings.Json,
            settings.Quiet,
            "accounts update requires exactly one of --password <password> or --password-stdin.",
            "Pipe the new password to stdin and re-run with --password-stdin.",
            "Password: ",
            cancellationToken);

        var plan = new MyJdRequestPlan(
            "accounts.update",
            "POST",
            "/accountsV2/setUserNameAndPassword",
            new Dictionary<string, object?>
            {
                ["accountId"] = settings.AccountId.Value,
                ["username"] = settings.Username.Trim(),
                ["password"] = password,
            },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            $"'accounts update' will update credentials for account {settings.AccountId.Value}.",
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
