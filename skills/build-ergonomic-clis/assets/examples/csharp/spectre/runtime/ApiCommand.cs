using Spectre.Console.Cli;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DiagnosticLogPath = null);

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
        var httpDiagnostics = new HttpDiagnosticsContext();

        ResolvedContext resolved;
        try
        {
            resolved = _resolver.Resolve(settings);
        }
        catch (CliException ex)
        {
            var logPath = _diagnosticLogger.TryWrite(context.Name, ex);
            RenderCliError(ex, outputMode, logPath, settings.Quiet);
            return ex.ExitCode;
        }
        catch (Exception ex)
        {
            var logPath = _diagnosticLogger.TryWrite(context.Name, ex);
            RenderUnexpectedError(ex, logPath, outputMode, settings.Verbose, settings.Quiet);
            return 1;
        }

        try
        {
            return await ExecuteCoreAsync(context, settings, resolved, httpDiagnostics, cancellationToken);
        }
        catch (CliException ex)
        {
            var logPath = _diagnosticLogger.TryWrite(resolved.ToSafe(), context.Name, ex, httpDiagnostics.Snapshot);
            RenderCliError(ex, resolved.OutputMode, logPath, settings.Verbose, settings.Quiet, resolved.ToSafe(), httpDiagnostics.Snapshot);
            return ex.ExitCode;
        }
        catch (HttpRequestException ex)
        {
            var logPath = _diagnosticLogger.TryWrite(resolved.ToSafe(), context.Name, ex, httpDiagnostics.Snapshot);
            if (MapHttpError(ex, httpDiagnostics.Snapshot) is { } mapped)
            {
                RenderCliError(mapped, resolved.OutputMode, logPath, settings.Verbose, settings.Quiet, resolved.ToSafe(), httpDiagnostics.Snapshot);
                return mapped.ExitCode;
            }
            RenderNetworkError(ex, logPath, resolved.ToSafe(), httpDiagnostics.Snapshot, resolved.OutputMode, settings.Verbose, settings.Quiet);
            return 8;
        }
        catch (OperationCanceledException ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                var cancelled = CliException.Cancelled("Cancelled.");
                var logPath = _diagnosticLogger.TryWrite(resolved.ToSafe(), context.Name, cancelled, httpDiagnostics.Snapshot);
                RenderCliError(cancelled, resolved.OutputMode, logPath, settings.Verbose, settings.Quiet, resolved.ToSafe(), httpDiagnostics.Snapshot);
                return 10;
            }

            var logPath = _diagnosticLogger.TryWrite(resolved.ToSafe(), context.Name, ex, httpDiagnostics.Snapshot);
            RenderNetworkError(new HttpRequestException("Request cancelled or timed out.", ex), logPath, resolved.ToSafe(), httpDiagnostics.Snapshot, resolved.OutputMode, settings.Verbose, settings.Quiet);
            return 8;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var logPath = _diagnosticLogger.TryWrite(resolved.ToSafe(), context.Name, ex, httpDiagnostics.Snapshot);
            RenderUnexpectedError(ex, logPath, resolved.OutputMode, settings.Verbose, settings.Quiet, resolved.ToSafe());
            return 1;
        }
    }

    protected abstract Task<int> ExecuteCoreAsync(
        CommandContext context,
        TSettings settings,
        ResolvedContext resolved,
        HttpDiagnosticsContext httpDiagnostics,
        CancellationToken cancellationToken);

    protected static async Task<HttpResponseMessage> SendWithDiagnosticsAsync(
        HttpClient client,
        HttpRequestMessage request,
        HttpDiagnosticsContext httpDiagnostics,
        CancellationToken cancellationToken)
    {
        await httpDiagnostics.CaptureRequestAsync(request, cancellationToken);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await httpDiagnostics.CaptureResponseAsync(response, cancellationToken);
        return response;
    }

    private static void RenderCliError(
        CliException ex,
        OutputMode outputMode,
        string? logPath = null,
        bool verbose = false,
        bool quiet = false,
        ResolvedContextSafe? context = null,
        HttpExchangeSnapshot? exchange = null)
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
                Meta: new JsonMeta(SchemaVersion: 1, DiagnosticLogPath: NormalizeLogPath(logPath))));
            return;
        }

        Console.Error.WriteLine($"Error: {SecretRedactor.RedactPotentialSecrets(ex.Message)}");
        if (!string.IsNullOrWhiteSpace(ex.RecoveryCommand))
            Console.Error.WriteLine($"Try: {SecretRedactor.RedactPotentialSecrets(ex.RecoveryCommand)}");
        if (!quiet && verbose)
        {
            if (context is not null)
            {
                Console.Error.WriteLine($"Target: {SecretRedactor.RedactPotentialSecrets(context.BaseUrl)} [{context.TargetIdentityKey}]");
                Console.Error.WriteLine($"Profile: {context.Profile} (profile source: {context.ProfileSource}, auth source: {context.AuthSource})");
            }
            if (!string.IsNullOrWhiteSpace(exchange?.RequestMethod) || !string.IsNullOrWhiteSpace(exchange?.RequestUri))
                Console.Error.WriteLine($"Request: {exchange?.RequestMethod ?? "(unknown)"} {exchange?.RequestUri ?? "(unknown)"}");
            if (exchange?.ResponseStatusCode is int statusCode)
                Console.Error.WriteLine($"Response: {statusCode} {exchange.ResponseReasonPhrase}");
        }
        if (!quiet && !string.IsNullOrWhiteSpace(logPath))
            Console.Error.WriteLine($"Diagnostic log saved to: {logPath}");
    }

    private static void RenderNetworkError(
        HttpRequestException ex,
        string? logPath,
        ResolvedContextSafe context,
        HttpExchangeSnapshot? exchange,
        OutputMode outputMode,
        bool verbose,
        bool quiet)
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
        {
            Console.Error.WriteLine($"Target: {SecretRedactor.RedactPotentialSecrets(context.BaseUrl)} [{context.TargetIdentityKey}]");
            Console.Error.WriteLine($"Profile: {context.Profile} (auth source: {context.AuthSource})");
            if (!string.IsNullOrWhiteSpace(exchange?.RequestMethod) || !string.IsNullOrWhiteSpace(exchange?.RequestUri))
                Console.Error.WriteLine($"Request: {exchange?.RequestMethod ?? "(unknown)"} {exchange?.RequestUri ?? "(unknown)"}");
            if (exchange?.ResponseStatusCode is int statusCode)
                Console.Error.WriteLine($"Response: {statusCode} {exchange.ResponseReasonPhrase}");
            Console.Error.WriteLine($"Details: {SecretRedactor.RedactPotentialSecrets(ex.Message)}");
        }
        if (!quiet && !string.IsNullOrWhiteSpace(logPath))
            Console.Error.WriteLine($"Diagnostic log saved to: {logPath}");
    }

    private static void RenderUnexpectedError(
        Exception ex,
        string? logPath,
        OutputMode outputMode,
        bool verbose,
        bool quiet,
        ResolvedContextSafe? context = null)
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
        if (!quiet && verbose)
        {
            if (context is not null)
            {
                Console.Error.WriteLine($"Target: {SecretRedactor.RedactPotentialSecrets(context.BaseUrl)} [{context.TargetIdentityKey}]");
                Console.Error.WriteLine($"Profile: {context.Profile} (auth source: {context.AuthSource})");
            }
            Console.Error.WriteLine($"Details: {SecretRedactor.RedactPotentialSecrets(ex.Message)}");
        }
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

    protected virtual CliException? MapHttpError(HttpRequestException ex, HttpExchangeSnapshot? exchange)
    {
        if (ex.StatusCode is null)
            return null;

        return (int)ex.StatusCode switch
        {
            401 => new CliException(3, "Authentication required.", "Run 'example auth login' or provide a token."),
            403 => new CliException(4, "Access denied for the current identity.", "Use a different profile/account or request the required permission."),
            404 => new CliException(5, "Requested resource was not found."),
            409 or 412 => new CliException(6, "Request conflicts with the current server state.", "Refresh state and retry."),
            429 => new CliException(7, "Request was rate limited.", "Retry later or reduce request rate."),
            >= 500 => new CliException(1, $"Server returned HTTP {(int)ex.StatusCode} {ex.StatusCode}.", "Check server logs or retry later."),
            _ => new CliException(1, $"Request failed with HTTP {(int)ex.StatusCode} {ex.StatusCode}."),
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
