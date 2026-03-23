namespace JDownloader.Cli.Runtime;

public sealed record CommandOutput(
    object? Data,
    IReadOnlyList<string>? HumanLines = null,
    IReadOnlyList<string>? Warnings = null);

public interface IOutputRenderer
{
    void WriteSuccess(ResolvedProfileContext resolved, CommandOutput output);
    void WriteAnonymousSuccess(OutputMode mode, CommandOutput output);
    void WriteFailure(OutputMode mode, CliException exception, string? diagnosticLogPath, bool verbose, bool quiet);
    void WriteUnexpectedFailure(OutputMode mode, Exception exception, string? diagnosticLogPath, bool verbose, bool quiet);
}
