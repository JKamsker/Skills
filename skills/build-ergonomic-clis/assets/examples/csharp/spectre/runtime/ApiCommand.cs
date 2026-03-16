using Spectre.Console.Cli;
using System.Text.Json;

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

internal sealed record JsonMeta(
    int SchemaVersion,
    string? DiagnosticLogPath = null);

internal sealed record JsonError(
    string Kind,
    string Message,
    string? Recovery = null);

internal sealed record JsonEnvelope(
    bool Ok,
    object? Data,
    JsonError? Error,
    JsonMeta Meta);

public abstract class ApiCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : GlobalOptions
{
    private readonly TargetResolver _resolver;
    private readonly DiagnosticLogger _diagnosticLogger;

    protected ApiCommand(
        TargetResolver resolver,
        DiagnosticLogger diagnosticLogger)
    {
        _resolver = resolver;
        _diagnosticLogger = diagnosticLogger;
    }

    public sealed override async Task<int> ExecuteAsync(
        CommandContext context,
        TSettings settings)
    {
        ResolvedContext resolved;
        try
        {
            resolved = _resolver.Resolve(settings);
        }
        catch (CliException ex)
        {
            RenderCliError(ex, settings.OutputMode);
            return ex.ExitCode;
        }

        try
        {
            return await ExecuteAsync(context, settings, resolved);
        }
        catch (CliException ex)
        {
            RenderCliError(ex, resolved.OutputMode);
            return ex.ExitCode;
        }
        catch (HttpRequestException ex)
        {
            var logPath = _diagnosticLogger.TryWrite(resolved, context.Name, ex);
            RenderNetworkError(ex, logPath, resolved.OutputMode);
            return 8;
        }
        catch (OperationCanceledException ex)
        {
            var logPath = _diagnosticLogger.TryWrite(resolved, context.Name, ex);
            RenderNetworkError(new HttpRequestException("Request timed out.", ex), logPath, resolved.OutputMode);
            return 8;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var logPath = _diagnosticLogger.TryWrite(resolved, context.Name, ex);
            RenderUnexpectedError(ex, logPath, resolved.OutputMode);
            return 1;
        }
    }

    protected abstract Task<int> ExecuteAsync(
        CommandContext context,
        TSettings settings,
        ResolvedContext resolved);

    private static void RenderCliError(CliException ex, OutputMode outputMode)
    {
        if (outputMode == OutputMode.Json)
        {
            WriteJson(new JsonEnvelope(
                Ok: false,
                Data: null,
                Error: new JsonError(
                    Kind: KindForExitCode(ex.ExitCode),
                    Message: RedactPotentialSecrets(ex.Message),
                    Recovery: ex.RecoveryCommand),
                Meta: new JsonMeta(SchemaVersion: 1)));
            return;
        }

        Console.Error.WriteLine($"Error: {RedactPotentialSecrets(ex.Message)}");
        if (!string.IsNullOrWhiteSpace(ex.RecoveryCommand))
            Console.Error.WriteLine($"Try: {ex.RecoveryCommand}");
    }

    private static void RenderNetworkError(HttpRequestException ex, string? logPath, OutputMode outputMode)
    {
        if (outputMode == OutputMode.Json)
        {
            WriteJson(new JsonEnvelope(
                Ok: false,
                Data: null,
                Error: new JsonError(
                    Kind: "network",
                    Message: "Network error."),
                Meta: new JsonMeta(SchemaVersion: 1, DiagnosticLogPath: logPath)));
            return;
        }

        Console.Error.WriteLine($"Network error: {RedactPotentialSecrets(ex.Message)}");
        if (!string.IsNullOrWhiteSpace(logPath))
            Console.Error.WriteLine($"Diagnostic log: {logPath}");
    }

    private static void RenderUnexpectedError(Exception ex, string? logPath, OutputMode outputMode)
    {
        if (outputMode == OutputMode.Json)
        {
            WriteJson(new JsonEnvelope(
                Ok: false,
                Data: null,
                Error: new JsonError(
                    Kind: "unexpected",
                    Message: "Unexpected error."),
                Meta: new JsonMeta(SchemaVersion: 1, DiagnosticLogPath: logPath)));
            return;
        }

        Console.Error.WriteLine("Unexpected client error.");
        if (!string.IsNullOrWhiteSpace(logPath))
            Console.Error.WriteLine($"Diagnostic log: {logPath}");
    }

    private static string KindForExitCode(int exitCode)
    {
        return exitCode switch
        {
            2 => "usage",
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

    private static string RedactPotentialSecrets(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        value = System.Text.RegularExpressions.Regex.Replace(
            value,
            @"\b[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b",
            "REDACTED_JWT");

        value = System.Text.RegularExpressions.Regex.Replace(
            value,
            @"\b(?<key>token|access_token|api_key|apikey|password|secret)=(?<value>[^&\s]+)",
            match => $"{match.Groups["key"].Value}=REDACTED",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        value = System.Text.RegularExpressions.Regex.Replace(
            value,
            @"\bBearer\s+[A-Za-z0-9._~-]+\b",
            "Bearer REDACTED",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return value;
    }
}
