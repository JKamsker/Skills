using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Auth;

public sealed class WhoAmICommand : AnonymousCommand<NoArgSettings>
{
    private readonly IProfileResolver _profileResolver;

    public WhoAmICommand(
        IProfileResolver profileResolver,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileResolver = profileResolver;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, NoArgSettings settings, CancellationToken cancellationToken)
    {
        var resolved = await _profileResolver.ResolveAsync(settings, requireDevice: false, cancellationToken);
        return new CommandOutput(
            new
            {
                profile = resolved.ProfileName,
                resolved.AccountEmail,
                resolved.ProfileSource,
                resolved.OutputMode,
                resolved.TimeoutSeconds,
            },
            [
                $"Profile: {resolved.ProfileName}",
                $"Email: {resolved.AccountEmail ?? "(none)"}",
                $"Profile source: {resolved.ProfileSource}",
                $"Output mode: {resolved.OutputMode}",
                $"Timeout: {resolved.TimeoutSeconds}s",
            ]);
    }
}
