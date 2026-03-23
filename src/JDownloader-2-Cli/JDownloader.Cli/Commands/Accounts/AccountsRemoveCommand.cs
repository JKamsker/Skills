using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Accounts;

public sealed class AccountsRemoveSettings : DeviceCommandSettings
{
    [CommandOption("--account-id <ID>")]
    [Description("Repeatable account identifier to remove.")]
    public long[] AccountIds { get; init; } = [];
}

public sealed class AccountsRemoveCommand : DeviceApiCommand<AccountsRemoveSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public AccountsRemoveCommand(
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
        AccountsRemoveSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (settings.AccountIds.Length == 0)
            throw CliException.Usage("accounts remove requires at least one --account-id <id>.");

        var plan = new MyJdRequestPlan(
            "accounts.remove",
            "POST",
            "/accountsV2/removeAccounts",
            new Dictionary<string, object?> { ["accountIds"] = settings.AccountIds },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            $"'accounts remove' will permanently remove {settings.AccountIds.Length} account(s).",
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
