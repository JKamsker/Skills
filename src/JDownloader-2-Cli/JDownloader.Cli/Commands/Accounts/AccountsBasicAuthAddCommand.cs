using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Accounts;

public sealed class AccountsBasicAuthAddSettings : DeviceCommandSettings
{
    [CommandOption("--type <TYPE>")]
    [Description("Basic auth type: http or ftp.")]
    public string? Type { get; init; }

    [CommandOption("--hostmask <MASK>")]
    [Description("Hostmask for the basic auth entry.")]
    public string? Hostmask { get; init; }

    [CommandOption("--username <NAME>")]
    [Description("Basic auth username.")]
    public string? Username { get; init; }

    [CommandOption("--password <PASSWORD>")]
    [Description("Basic auth password.")]
    public string? Password { get; init; }

    [CommandOption("--password-stdin")]
    [Description("Read the basic auth password from stdin.")]
    public bool PasswordStdin { get; init; }
}

public sealed class AccountsBasicAuthAddCommand : DeviceApiCommand<AccountsBasicAuthAddSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public AccountsBasicAuthAddCommand(
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
        AccountsBasicAuthAddSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Type)
            || string.IsNullOrWhiteSpace(settings.Hostmask)
            || string.IsNullOrWhiteSpace(settings.Username))
        {
            throw CliException.Usage("accounts basic-auth add requires --type <http|ftp> --hostmask <mask> --username <name> and exactly one password source.");
        }

        var type = NormalizeBasicAuthType(settings.Type);
        var password = await SecretInput.ReadSecretAsync(
            settings.Password,
            settings.PasswordStdin,
            requireStdinInNonInteractiveMode: true,
            settings.Json,
            settings.Quiet,
            "accounts basic-auth add requires exactly one of --password <password> or --password-stdin.",
            "Pipe the basic auth password to stdin and re-run with --password-stdin.",
            "Password: ",
            cancellationToken);

        var plan = new MyJdRequestPlan(
            "accounts.basic-auth.add",
            "POST",
            "/accountsV2/addBasicAuth",
            new Dictionary<string, object?>
            {
                ["type"] = type,
                ["hostmask"] = settings.Hostmask.Trim(),
                ["username"] = settings.Username.Trim(),
                ["password"] = password,
            },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            $"'accounts basic-auth add' will add a {type} basic auth entry for '{settings.Hostmask.Trim()}'.",
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

    internal static string NormalizeBasicAuthType(string rawType)
    {
        return rawType.Trim().ToUpperInvariant() switch
        {
            "HTTP" => "HTTP",
            "FTP" => "FTP",
            _ => throw CliException.Usage("accounts basic-auth add requires --type <http|ftp>."),
        };
    }
}
