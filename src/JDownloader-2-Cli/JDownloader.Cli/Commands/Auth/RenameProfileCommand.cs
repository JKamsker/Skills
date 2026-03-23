using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Auth;

public sealed class RenameProfileSettings : GlobalSettings
{
    [CommandArgument(0, "<OLD_NAME>")]
    public required string OldName { get; init; }

    [CommandArgument(1, "<NEW_NAME>")]
    public required string NewName { get; init; }
}

public sealed class RenameProfileCommand : AnonymousCommand<RenameProfileSettings>
{
    private readonly IProfileStore _profileStore;

    public RenameProfileCommand(IProfileStore profileStore, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, RenameProfileSettings settings, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.Remove(settings.OldName, out var profile))
            throw CliException.NotFound($"Profile '{settings.OldName}' was not found.");
        if (config.Profiles.ContainsKey(settings.NewName))
            throw CliException.Conflict($"Profile '{settings.NewName}' already exists.");

        config.Profiles[settings.NewName] = profile;
        if (string.Equals(config.DefaultProfile, settings.OldName, StringComparison.OrdinalIgnoreCase))
            config.DefaultProfile = settings.NewName;

        await _profileStore.SaveAsync(config, cancellationToken);
        return new CommandOutput(
            new { oldName = settings.OldName, newName = settings.NewName },
            [$"Renamed profile '{settings.OldName}' to '{settings.NewName}'."]);
    }
}
