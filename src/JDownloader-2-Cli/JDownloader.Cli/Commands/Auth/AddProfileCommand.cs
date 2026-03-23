using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Auth;

public sealed class AddProfileSettings : GlobalSettings
{
    [CommandArgument(0, "<NAME>")]
    public required string Name { get; init; }
}

public sealed class AddProfileCommand : AnonymousCommand<AddProfileSettings>
{
    private readonly IProfileStore _profileStore;

    public AddProfileCommand(IProfileStore profileStore, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, AddProfileSettings settings, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (config.Profiles.ContainsKey(settings.Name))
            throw CliException.Conflict($"Profile '{settings.Name}' already exists.");

        config.Profiles[settings.Name] = new ProfileRecord
        {
            Output = settings.Output,
            TimeoutSeconds = settings.TimeoutSeconds,
        };
        config.DefaultProfile ??= settings.Name;
        await _profileStore.SaveAsync(config, cancellationToken);

        return new CommandOutput(
            new { name = settings.Name, created = true },
            [$"Created profile '{settings.Name}'."]);
    }
}
