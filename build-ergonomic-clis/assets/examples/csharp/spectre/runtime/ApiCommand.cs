using Spectre.Console;
using Spectre.Console.Cli;

namespace ExampleCli.Runtime;

public sealed class CliException : Exception
{
    public int ExitCode { get; }
    public string? RecoveryCommand { get; }

    public CliException(int exitCode, string message, string? recoveryCommand = null)
        : base(message)
    {
        ExitCode = exitCode;
        RecoveryCommand = recoveryCommand;
    }

    public static CliException Usage(string message, string? recoveryCommand = null)
        => new(2, message, recoveryCommand);

    public static CliException Cancelled(string message)
        => new(10, message);
}

public abstract class ApiCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : GlobalOptions
{
    private readonly TargetResolver _resolver;
    private readonly DiagnosticLogger _diagnosticLogger;
    private readonly IAnsiConsole _console;

    protected ApiCommand(
        TargetResolver resolver,
        DiagnosticLogger diagnosticLogger,
        IAnsiConsole console)
    {
        _resolver = resolver;
        _diagnosticLogger = diagnosticLogger;
        _console = console;
    }

    public sealed override async Task<int> ExecuteAsync(
        CommandContext context,
        TSettings settings,
        CancellationToken cancellationToken)
    {
        ResolvedContext resolved;
        try
        {
            resolved = _resolver.Resolve(settings);
        }
        catch (CliException ex)
        {
            RenderCliError(ex);
            return ex.ExitCode;
        }

        try
        {
            return await ExecuteAsync(context, settings, resolved, cancellationToken);
        }
        catch (CliException ex)
        {
            RenderCliError(ex);
            return ex.ExitCode;
        }
        catch (HttpRequestException ex)
        {
            var logPath = _diagnosticLogger.Write(resolved, context.Name, ex);
            _console.MarkupLine($"[red]Network error:[/] {Markup.Escape(ex.Message)}");
            _console.MarkupLine($"[yellow]Diagnostic log:[/] {Markup.Escape(logPath)}");
            return 8;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var logPath = _diagnosticLogger.Write(resolved, context.Name, ex);
            _console.MarkupLine("[red]Unexpected client error.[/]");
            _console.MarkupLine($"[yellow]Diagnostic log:[/] {Markup.Escape(logPath)}");
            return 1;
        }
    }

    protected abstract Task<int> ExecuteAsync(
        CommandContext context,
        TSettings settings,
        ResolvedContext resolved,
        CancellationToken cancellationToken);

    private void RenderCliError(CliException ex)
    {
        _console.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
        if (!string.IsNullOrWhiteSpace(ex.RecoveryCommand))
            _console.MarkupLine($"[dim]Try:[/] {Markup.Escape(ex.RecoveryCommand)}");
    }
}
