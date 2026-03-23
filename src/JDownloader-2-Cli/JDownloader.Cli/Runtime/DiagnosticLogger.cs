using JDownloader.Cli.Config;
using System.Text;

namespace JDownloader.Cli.Runtime;

public interface IDiagnosticLogger
{
    string? TryWrite(string operation, Exception exception);
    string? TryWrite(ResolvedProfileContext? resolved, string operation, Exception exception);
}

public sealed class DiagnosticLogger : IDiagnosticLogger
{
    private readonly ICliEnvironment _environment;
    private readonly CliPathProvider _paths;

    public DiagnosticLogger(ICliEnvironment environment, CliPathProvider paths)
    {
        _environment = environment;
        _paths = paths;
    }

    public string? TryWrite(string operation, Exception exception)
    {
        return TryWrite(null, operation, exception);
    }

    public string? TryWrite(ResolvedProfileContext? resolved, string operation, Exception exception)
    {
        try
        {
            var logsPath = _paths.GetLogsDirectory();
            Directory.CreateDirectory(logsPath);
            var path = Path.Combine(
                logsPath,
                $"jd2-error-{_environment.UtcNow:yyyyMMdd-HHmmss-fff}.log");

            var builder = new StringBuilder();
            builder.AppendLine($"Timestamp: {_environment.UtcNow:O}");
            builder.AppendLine($"Operation: {operation}");
            builder.AppendLine($"CommandLine: {_environment.CommandLine}");
            if (resolved is not null)
            {
                builder.AppendLine($"Profile: {resolved.ProfileName}");
                builder.AppendLine($"ProfileSource: {resolved.ProfileSource}");
                builder.AppendLine($"Device: {resolved.Device?.DisplayValue ?? "(none)"}");
                builder.AppendLine($"DeviceSource: {resolved.DeviceSource ?? "(none)"}");
                builder.AppendLine($"OutputMode: {resolved.OutputMode}");
            }

            builder.AppendLine($"Exception: {exception.GetType().FullName}");
            builder.AppendLine($"Message: {exception.Message}");
            builder.AppendLine();
            builder.AppendLine(exception.ToString());

            File.WriteAllText(path, builder.ToString());
            return path;
        }
        catch
        {
            return null;
        }
    }
}
