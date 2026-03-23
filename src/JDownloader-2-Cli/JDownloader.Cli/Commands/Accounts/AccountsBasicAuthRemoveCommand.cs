using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Accounts;

public sealed class AccountsBasicAuthRemoveSettings : DeviceCommandSettings
{
    [CommandOption("--basic-auth-id <ID>")]
    [Description("Repeatable basic auth identifier to remove.")]
    public long[] BasicAuthIds { get; init; } = [];
}

public sealed class AccountsBasicAuthRemoveCommand : DeviceApiCommand<AccountsBasicAuthRemoveSettings>
{
    private readonly IMyJdTransport _transport;
    private readonly IConfirmationGuard _confirmationGuard;

    public AccountsBasicAuthRemoveCommand(
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
        AccountsBasicAuthRemoveSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (settings.BasicAuthIds.Length == 0)
            throw CliException.Usage("accounts basic-auth remove requires at least one --basic-auth-id <id>.");

        var plan = new MyJdRequestPlan(
            "accounts.basic-auth.remove",
            "POST",
            "/accountsV2/removeBasicAuths",
            new Dictionary<string, object?> { ["basicAuthIds"] = settings.BasicAuthIds },
            null,
            true,
            false,
            resolved.Device?.Id);

        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            $"'accounts basic-auth remove' will remove {settings.BasicAuthIds.Length} basic auth entr{(settings.BasicAuthIds.Length == 1 ? "y" : "ies")}.",
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
