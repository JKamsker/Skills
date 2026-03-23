using JDownloader.Cli.Auth;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Auth;

public sealed class AuthStatusCommand : AnonymousCommand<NoArgSettings>
{
    private readonly IMyJdAuthService _authService;
    private readonly IProfileResolver _profileResolver;

    public AuthStatusCommand(
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
        var status = await _authService.GetStatusAsync(resolved.ProfileName, cancellationToken);
        return new CommandOutput(
            status,
            [
                $"Profile: {status.ProfileName}",
                $"Email: {status.Email ?? "(none)"}",
                $"Stored auth: {(status.HasStoredAuth ? "yes" : "no")}",
                $"Relay transport ready: {(status.TransportReady ? "yes" : "no")}",
            ]);
    }
}
