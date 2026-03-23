using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Shared;

public abstract class AnonymousCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : GlobalSettings
{
    private readonly IOutputRenderer _outputRenderer;
    private readonly IDiagnosticLogger _diagnosticLogger;

    protected AnonymousCommand(IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
    {
        _outputRenderer = outputRenderer;
        _diagnosticLogger = diagnosticLogger;
    }

    public sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken)
    {
        var mode = settings.Json || string.Equals(settings.Output, "json", StringComparison.OrdinalIgnoreCase)
            ? OutputMode.Json
            : OutputMode.Human;

        try
        {
            var output = await ExecuteCoreAsync(context, settings, cancellationToken);
            _outputRenderer.WriteAnonymousSuccess(mode, output);
            return 0;
        }
        catch (CliException ex)
        {
            var logPath = _diagnosticLogger.TryWrite(context.Name, ex);
            _outputRenderer.WriteFailure(mode, ex, logPath, settings.Verbose, settings.Quiet);
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            var logPath = _diagnosticLogger.TryWrite(context.Name, ex);
            _outputRenderer.WriteUnexpectedFailure(mode, ex, logPath, settings.Verbose, settings.Quiet);
            return 1;
        }
    }

    protected abstract Task<CommandOutput> ExecuteCoreAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken);
}

public abstract class DeviceApiCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : DeviceCommandSettings
{
    private readonly IProfileResolver _profileResolver;
    private readonly IOutputRenderer _outputRenderer;
    private readonly IDiagnosticLogger _diagnosticLogger;

    protected DeviceApiCommand(
        IProfileResolver profileResolver,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
    {
        _profileResolver = profileResolver;
        _outputRenderer = outputRenderer;
        _diagnosticLogger = diagnosticLogger;
    }

    protected virtual bool RequireDevice => true;

    public sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken)
    {
        ResolvedProfileContext? resolved = null;
        try
        {
            resolved = await _profileResolver.ResolveAsync(settings, RequireDevice, cancellationToken);
            var output = await ExecuteCoreAsync(context, settings, resolved, cancellationToken);
            _outputRenderer.WriteSuccess(resolved, output);
            return 0;
        }
        catch (CliException ex)
        {
            var mode = resolved?.OutputMode
                ?? (settings.Json || string.Equals(settings.Output, "json", StringComparison.OrdinalIgnoreCase)
                    ? OutputMode.Json
                    : OutputMode.Human);
            var logPath = _diagnosticLogger.TryWrite(resolved, context.Name, ex);
            _outputRenderer.WriteFailure(mode, ex, logPath, settings.Verbose, settings.Quiet);
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            var mode = resolved?.OutputMode
                ?? (settings.Json || string.Equals(settings.Output, "json", StringComparison.OrdinalIgnoreCase)
                    ? OutputMode.Json
                    : OutputMode.Human);
            var logPath = _diagnosticLogger.TryWrite(resolved, context.Name, ex);
            _outputRenderer.WriteUnexpectedFailure(mode, ex, logPath, settings.Verbose, settings.Quiet);
            return 1;
        }
    }

    protected abstract Task<CommandOutput> ExecuteCoreAsync(
        CommandContext context,
        TSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken);
}
