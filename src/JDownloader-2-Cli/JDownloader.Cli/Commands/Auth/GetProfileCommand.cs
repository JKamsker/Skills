using System.ComponentModel;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Auth;

public sealed class GetProfileSettings : GlobalSettings
{
    [CommandArgument(0, "<NAME>")]
    public required string Name { get; init; }
}

public sealed class GetProfileCommand : AnonymousCommand<GetProfileSettings>
{
    private readonly IProfileStore _profileStore;

    public GetProfileCommand(IProfileStore profileStore, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, GetProfileSettings settings, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.TryGetValue(settings.Name, out var profile))
            throw CliException.NotFound($"Profile '{settings.Name}' was not found.");

        return new CommandOutput(
            new
            {
                name = settings.Name,
                isDefault = string.Equals(config.DefaultProfile, settings.Name, StringComparison.OrdinalIgnoreCase),
                profile.AccountEmail,
                profile.DefaultDeviceId,
                profile.DefaultDeviceName,
                profile.Output,
                profile.TimeoutSeconds,
                profile.KnownDevices,
            },
            [
                $"Profile: {settings.Name}",
                $"Email: {profile.AccountEmail ?? "(none)"}",
                $"Default device: {profile.DefaultDeviceName ?? profile.DefaultDeviceId ?? "(none)"}",
                $"Known devices: {profile.KnownDevices.Count}",
            ]);
    }
}
