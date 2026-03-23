using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Accounts;

public sealed class AccountsGetSettings : DeviceCommandSettings
{
    [CommandOption("--hoster <NAME>")]
    [Description("Premium hoster name to resolve to its account URL.")]
    public string? Hoster { get; init; }
}

public sealed class AccountsGetCommand : DeviceApiCommand<AccountsGetSettings>
{
    private readonly IMyJdTransport _transport;

    public AccountsGetCommand(
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
        AccountsGetSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Hoster))
            throw CliException.Usage("accounts get requires --hoster <name>.");

        var result = await _transport.ExecuteAsync(
            resolved,
            new MyJdRequestPlan(
                "accounts.get",
                "POST",
                "/accountsV2/getPremiumHosterUrl",
                new Dictionary<string, object?> { ["hoster"] = settings.Hoster.Trim() },
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
