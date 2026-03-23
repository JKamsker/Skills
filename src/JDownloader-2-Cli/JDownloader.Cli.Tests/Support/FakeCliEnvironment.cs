using JDownloader.Cli.Runtime;

namespace JDownloader.Cli.Tests.Support;

internal sealed class FakeCliEnvironment : ICliEnvironment
{
    private readonly Dictionary<string, string> _environmentVariables;
    private readonly string _home;

    public FakeCliEnvironment(
        string home,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        bool inputRedirected = true,
        bool outputRedirected = true,
        bool errorRedirected = true)
    {
        _home = home;
        _environmentVariables = environmentVariables?.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        IsInputRedirected = inputRedirected;
        IsOutputRedirected = outputRedirected;
        IsErrorRedirected = errorRedirected;
    }

    public string? GetEnvironmentVariable(string name)
    {
        return _environmentVariables.TryGetValue(name, out var value) ? value : null;
    }

    public string GetFolderPath(Environment.SpecialFolder folder)
    {
        return _home;
    }

    public bool IsInputRedirected { get; }
    public bool IsOutputRedirected { get; }
    public bool IsErrorRedirected { get; }
    public DateTimeOffset UtcNow => new(2026, 03, 23, 12, 0, 0, TimeSpan.Zero);
    public string CommandLine => "jd2-test";
}
