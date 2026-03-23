namespace JDownloader.Cli.Runtime;

public interface ICliEnvironment
{
    string? GetEnvironmentVariable(string name);
    string GetFolderPath(Environment.SpecialFolder folder);
    bool IsInputRedirected { get; }
    bool IsOutputRedirected { get; }
    bool IsErrorRedirected { get; }
    DateTimeOffset UtcNow { get; }
    string CommandLine { get; }
}

public sealed class SystemCliEnvironment : ICliEnvironment
{
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    public string GetFolderPath(Environment.SpecialFolder folder) => Environment.GetFolderPath(folder);

    public bool IsInputRedirected => Console.IsInputRedirected;

    public bool IsOutputRedirected => Console.IsOutputRedirected;

    public bool IsErrorRedirected => Console.IsErrorRedirected;

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public string CommandLine => Environment.CommandLine;
}
