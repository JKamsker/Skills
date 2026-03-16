using Spectre.Console.Cli;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

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

public sealed record JsonMeta(
    int SchemaVersion,
    string? DiagnosticLogPath = null);

public sealed record JsonError(
    string Kind,
    string Message,
    string? Recovery = null);

public sealed record JsonEnvelope(
    bool Ok,
    object? Data,
    JsonError? Error,
    JsonMeta Meta);

public abstract class ApiCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : GlobalOptions
{
    // This example requires Spectre.Console.Cli versions where `ExecuteAsync` includes a `CancellationToken`
    // (for example: 0.53+). If your version differs, adjust the override signature accordingly.
    private readonly TargetResolver _resolver;
    private readonly DiagnosticLogger _diagnosticLogger;

    protected ApiCommand(
        TargetResolver resolver,
        DiagnosticLogger diagnosticLogger)
    {
        _resolver = resolver;
        _diagnosticLogger = diagnosticLogger;
    }

    public sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken)
    {
        var outputMode = settings.OutputMode;

        ResolvedContext resolved;
        try
        {
            resolved = _resolver.Resolve(settings);
        }
        catch (CliException ex)
        {
            RenderCliError(ex, outputMode);
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            var logPath = _diagnosticLogger.TryWrite(context.Name, ex);
            RenderUnexpectedError(ex, logPath, outputMode, settings.Quiet);
            return 1;
        }

        try
        {
            return await ExecuteCoreAsync(context, settings, resolved, cancellationToken);
        }
        catch (CliException ex)
        {
            RenderCliError(ex, resolved.OutputMode);
            return ex.ExitCode;
        }
        catch (HttpRequestException ex)
        {
            var logPath = _diagnosticLogger.TryWrite(resolved.ToSafe(), context.Name, ex);
            RenderNetworkError(ex, logPath, resolved.OutputMode, settings.Verbose, settings.Quiet);
            return 8;
        }
        catch (OperationCanceledException ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                RenderCliError(CliException.Cancelled("Cancelled."), resolved.OutputMode);
                return 10;
            }

            var logPath = _diagnosticLogger.TryWrite(resolved.ToSafe(), context.Name, ex);
            RenderNetworkError(new HttpRequestException("Request cancelled or timed out.", ex), logPath, resolved.OutputMode, settings.Verbose, settings.Quiet);
            return 8;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var logPath = _diagnosticLogger.TryWrite(resolved.ToSafe(), context.Name, ex);
            RenderUnexpectedError(ex, logPath, resolved.OutputMode, settings.Quiet);
            return 1;
        }
    }

    protected abstract Task<int> ExecuteCoreAsync(
        CommandContext context,
        TSettings settings,
        ResolvedContext resolved,
        CancellationToken cancellationToken);

    private static void RenderCliError(CliException ex, OutputMode outputMode)
    {
        if (outputMode == OutputMode.Json)
        {
            WriteJson(new JsonEnvelope(
                Ok: false,
                Data: null,
                Error: new JsonError(
                    Kind: KindForExitCode(ex.ExitCode),
                    Message: SecretRedactor.RedactPotentialSecrets(ex.Message) ?? string.Empty,
                    Recovery: SecretRedactor.RedactPotentialSecrets(ex.RecoveryCommand)),
                Meta: new JsonMeta(SchemaVersion: 1)));
            return;
        }

        Console.Error.WriteLine($"Error: {SecretRedactor.RedactPotentialSecrets(ex.Message)}");
        if (!string.IsNullOrWhiteSpace(ex.RecoveryCommand))
            Console.Error.WriteLine($"Try: {SecretRedactor.RedactPotentialSecrets(ex.RecoveryCommand)}");
    }

    private static void RenderNetworkError(HttpRequestException ex, string? logPath, OutputMode outputMode, bool verbose, bool quiet)
    {
        if (outputMode == OutputMode.Json)
        {
            WriteJson(new JsonEnvelope(
                Ok: false,
                Data: null,
                Error: new JsonError(
                    Kind: "network",
                    Message: "Network error."),
                Meta: new JsonMeta(SchemaVersion: 1, DiagnosticLogPath: NormalizeLogPath(logPath))));
            return;
        }

        Console.Error.WriteLine("Network error.");
        if (!quiet && verbose)
            Console.Error.WriteLine($"Details: {SecretRedactor.RedactPotentialSecrets(ex.Message)}");
        if (!quiet && !string.IsNullOrWhiteSpace(logPath))
            Console.Error.WriteLine($"Diagnostic log saved to: {logPath}");
    }

    private static void RenderUnexpectedError(Exception ex, string? logPath, OutputMode outputMode, bool quiet)
    {
        if (outputMode == OutputMode.Json)
        {
            WriteJson(new JsonEnvelope(
                Ok: false,
                Data: null,
                Error: new JsonError(
                    Kind: "unexpected",
                    Message: "Unexpected error."),
                Meta: new JsonMeta(SchemaVersion: 1, DiagnosticLogPath: NormalizeLogPath(logPath))));
            return;
        }

        Console.Error.WriteLine("Unexpected client error.");
        if (!quiet && !string.IsNullOrWhiteSpace(logPath))
            Console.Error.WriteLine($"Diagnostic log saved to: {logPath}");
    }

    private static string KindForExitCode(int exitCode)
    {
        return exitCode switch
        {
            1 => "unexpected",
            2 => "refused",
            3 => "not_authenticated",
            4 => "not_authorized",
            5 => "not_found",
            6 => "conflict",
            7 => "rate_limited",
            8 => "network",
            10 => "cancelled",
            _ => "error",
        };
    }

    private static void WriteJson(JsonEnvelope envelope)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        Console.Out.WriteLine(JsonSerializer.Serialize(envelope, options));
    }

    private static string? NormalizeLogPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        return path.Trim();
    }
}
