using System.Text.Json;

namespace JDownloader.Cli.Config;

public interface IProfileStore
{
    Task<Jd2Config> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(Jd2Config config, CancellationToken cancellationToken);
}

public sealed class FileProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly CliPathProvider _paths;

    public FileProfileStore(CliPathProvider paths)
    {
        _paths = paths;
    }

    public async Task<Jd2Config> LoadAsync(CancellationToken cancellationToken)
    {
        var path = _paths.GetConfigFilePath();
        if (!File.Exists(path))
            return new Jd2Config();

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<Jd2Config>(stream, JsonOptions, cancellationToken)
            ?? new Jd2Config();
    }

    public async Task SaveAsync(Jd2Config config, CancellationToken cancellationToken)
    {
        var path = _paths.GetConfigFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken);
    }
}
