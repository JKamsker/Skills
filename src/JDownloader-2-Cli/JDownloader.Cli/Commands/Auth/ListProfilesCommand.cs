using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Auth;

public sealed class ListProfilesCommand : AnonymousCommand<NoArgSettings>
{
    private readonly IProfileStore _profileStore;

    public ListProfilesCommand(IProfileStore profileStore, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, NoArgSettings settings, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        var items = config.Profiles
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new
            {
                name = pair.Key,
                isDefault = string.Equals(config.DefaultProfile, pair.Key, StringComparison.OrdinalIgnoreCase),
                pair.Value.AccountEmail,
                pair.Value.DefaultDeviceId,
                pair.Value.DefaultDeviceName,
                pair.Value.Output,
                pair.Value.TimeoutSeconds,
            })
            .ToList();

        var lines = items.Count == 0
            ? ["No profiles configured."]
            : items.Select(item =>
                    $"{item.name}{(item.isDefault ? " (default)" : string.Empty)}: email={item.AccountEmail ?? "(none)"}, device={item.DefaultDeviceName ?? item.DefaultDeviceId ?? "(none)"}")
                .ToArray();

        return new CommandOutput(items, lines);
    }
}
