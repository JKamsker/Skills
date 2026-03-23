using System.Text.Json;

namespace JDownloader.Cli.Runtime;

public sealed class OutputRenderer : IOutputRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public void WriteSuccess(ResolvedProfileContext resolved, CommandOutput output)
    {
        if (resolved.OutputMode == OutputMode.Json)
        {
            WriteEnvelope(new JsonEnvelope(
                true,
                output.Data,
                null,
                new JsonMeta(1, output.Warnings)));
            return;
        }

        WriteHuman(output);
    }

    public void WriteAnonymousSuccess(OutputMode mode, CommandOutput output)
    {
        if (mode == OutputMode.Json)
        {
            WriteEnvelope(new JsonEnvelope(
                true,
                output.Data,
                null,
                new JsonMeta(1, output.Warnings)));
            return;
        }

        WriteHuman(output);
    }

    public void WriteFailure(OutputMode mode, CliException exception, string? diagnosticLogPath, bool verbose, bool quiet)
    {
        if (mode == OutputMode.Json)
        {
            WriteEnvelope(new JsonEnvelope(
                false,
                null,
                new JsonError(exception.Kind, exception.Message, exception.Recovery),
                new JsonMeta(1, DiagnosticLogPath: diagnosticLogPath)));
            return;
        }

        Console.Error.WriteLine($"Error: {exception.Message}");
        if (!string.IsNullOrWhiteSpace(exception.Recovery))
            Console.Error.WriteLine($"Try: {exception.Recovery}");
        if (verbose && !quiet && !string.IsNullOrWhiteSpace(diagnosticLogPath))
            Console.Error.WriteLine($"Diagnostic log saved to: {diagnosticLogPath}");
    }

    public void WriteUnexpectedFailure(OutputMode mode, Exception exception, string? diagnosticLogPath, bool verbose, bool quiet)
    {
        if (mode == OutputMode.Json)
        {
            WriteEnvelope(new JsonEnvelope(
                false,
                null,
                new JsonError("unexpected", "Unexpected client error."),
                new JsonMeta(1, DiagnosticLogPath: diagnosticLogPath)));
            return;
        }

        Console.Error.WriteLine("Unexpected client error.");
        if (verbose)
            Console.Error.WriteLine(exception.Message);
        if (!quiet && !string.IsNullOrWhiteSpace(diagnosticLogPath))
            Console.Error.WriteLine($"Diagnostic log saved to: {diagnosticLogPath}");
    }

    private static void WriteHuman(CommandOutput output)
    {
        if (output.HumanLines is not null)
        {
            foreach (var line in output.HumanLines)
                Console.Out.WriteLine(line);
        }

        if (output.Warnings is not null)
        {
            foreach (var warning in output.Warnings)
                Console.Error.WriteLine($"Warning: {warning}");
        }
    }

    private static void WriteEnvelope(JsonEnvelope envelope)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(envelope, JsonOptions));
    }
}
