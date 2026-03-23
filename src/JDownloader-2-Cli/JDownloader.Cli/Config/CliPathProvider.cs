using JDownloader.Cli.Runtime;

namespace JDownloader.Cli.Config;

public sealed class CliPathProvider
{
    private readonly ICliEnvironment _environment;

    public CliPathProvider(ICliEnvironment environment)
    {
        _environment = environment;
    }

    public string GetConfigRoot()
    {
        var overridePath = _environment.GetEnvironmentVariable("JD2_CONFIG");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(
                _environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "jd2");
        }

        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(
                _environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "Library",
                "Application Support",
                "jd2");
        }

        var xdg = _environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdg))
            return Path.Combine(xdg, "jd2");

        return Path.Combine(
            _environment.GetFolderPath(Environment.SpecialFolder.Personal),
            ".config",
            "jd2");
    }

    public string GetConfigFilePath() => Path.Combine(GetConfigRoot(), "config.json");

    public string GetKeyFilePath()
    {
        var overridePath = _environment.GetEnvironmentVariable("JD2_KEYFILE");
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);

        return Path.Combine(GetConfigRoot(), "keyfile.pem");
    }

    public string GetLogsDirectory() => Path.Combine(GetConfigRoot(), "logs");
}
