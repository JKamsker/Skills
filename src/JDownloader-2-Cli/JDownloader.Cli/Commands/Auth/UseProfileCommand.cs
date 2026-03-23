using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Auth;

public sealed class UseProfileSettings : GlobalSettings
{
    [CommandArgument(0, "<NAME>")]
    public required string Name { get; init; }
}

public sealed class UseProfileCommand : AnonymousCommand<UseProfileSettings>
{
    private readonly IProfileStore _profileStore;

    public UseProfileCommand(IProfileStore profileStore, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, UseProfileSettings settings, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.ContainsKey(settings.Name))
            throw CliException.NotFound($"Profile '{settings.Name}' was not found.");

        config.DefaultProfile = settings.Name;
        await _profileStore.SaveAsync(config, cancellationToken);
        return new CommandOutput(new { defaultProfile = settings.Name }, [$"Default profile set to '{settings.Name}'."]);
    }
}
