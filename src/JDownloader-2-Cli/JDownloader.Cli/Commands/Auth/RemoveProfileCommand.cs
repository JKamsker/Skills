using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Auth;

public sealed class RemoveProfileSettings : GlobalSettings
{
    [CommandArgument(0, "<NAME>")]
    public required string Name { get; init; }
}

public sealed class RemoveProfileCommand : AnonymousCommand<RemoveProfileSettings>
{
    private readonly IProfileStore _profileStore;
    private readonly IConfirmationGuard _confirmationGuard;

    public RemoveProfileCommand(
        IProfileStore profileStore,
        IConfirmationGuard confirmationGuard,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
        _confirmationGuard = confirmationGuard;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, RemoveProfileSettings settings, CancellationToken cancellationToken)
    {
        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            $"Remove profile '{settings.Name}'?",
            () => Task.FromResult(new CommandOutput(new { profile = settings.Name, preview = true }, [$"Would remove profile '{settings.Name}'." ])));
        if (!proceed)
            return new CommandOutput(new { preview = true });

        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.TryGetValue(settings.Name, out var profile))
            throw CliException.NotFound($"Profile '{settings.Name}' was not found.");

        config.Profiles.Remove(settings.Name);
        if (string.Equals(config.DefaultProfile, settings.Name, StringComparison.OrdinalIgnoreCase))
            config.DefaultProfile = config.Profiles.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(profile.AccountEmail))
            config.Credentials.Remove(profile.AccountEmail);

        await _profileStore.SaveAsync(config, cancellationToken);
        return new CommandOutput(new { profile = settings.Name, removed = true }, [$"Removed profile '{settings.Name}'."]);
    }
}
