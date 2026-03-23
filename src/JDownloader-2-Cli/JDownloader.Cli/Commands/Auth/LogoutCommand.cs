using JDownloader.Cli.Auth;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Auth;

public sealed class LogoutCommand : AnonymousCommand<NoArgSettings>
{
    private readonly IMyJdAuthService _authService;
    private readonly IProfileResolver _profileResolver;

    public LogoutCommand(
        IMyJdAuthService authService,
        IProfileResolver profileResolver,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _authService = authService;
        _profileResolver = profileResolver;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, NoArgSettings settings, CancellationToken cancellationToken)
    {
        var resolved = await _profileResolver.ResolveAsync(settings, requireDevice: false, cancellationToken);
        await _authService.LogoutAsync(resolved.ProfileName, cancellationToken);
        return new CommandOutput(
            new { profile = resolved.ProfileName, loggedOut = true },
            [$"Removed stored auth for profile '{resolved.ProfileName}'."]);
    }
}
