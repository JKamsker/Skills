namespace JDownloader.Cli.Runtime;

public sealed class CliException : Exception
{
    public int ExitCode { get; }
    public string Kind { get; }
    public string? Recovery { get; }

    public CliException(int exitCode, string kind, string message, string? recovery = null)
        : base(message)
    {
        ExitCode = exitCode;
        Kind = kind;
        Recovery = recovery;
    }

    public static CliException Usage(string message, string? recovery = null)
        => new(2, "usage", message, recovery);

    public static CliException NotAuthenticated(string message, string? recovery = null)
        => new(3, "not_authenticated", message, recovery);

    public static CliException NotFound(string message, string? recovery = null)
        => new(5, "not_found", message, recovery);

    public static CliException Conflict(string message, string? recovery = null)
        => new(6, "conflict", message, recovery);

    public static CliException Transport(string message, string? recovery = null)
        => new(8, "transport", message, recovery);

    public static CliException Cancelled(string message)
        => new(10, "cancelled", message);
}
