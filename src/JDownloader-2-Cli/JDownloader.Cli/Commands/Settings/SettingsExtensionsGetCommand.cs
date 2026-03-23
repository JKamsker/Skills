using System.ComponentModel;
using System.Text.Json;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Runtime;
using JDownloader.Cli.Transport;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Settings;

public sealed class SettingsExtensionsGetSettings : DeviceCommandSettings
{
    [CommandOption("--id <ID>")]
    [Description("Exact extension identifier to resolve.")]
    public string? Id { get; init; }

    [CommandOption("--name <NAME>")]
    [Description("Exact extension name to resolve.")]
    public string? Name { get; init; }
}

public sealed class SettingsExtensionsGetCommand : DeviceApiCommand<SettingsExtensionsGetSettings>
{
    private readonly IMyJdTransport _transport;

    public SettingsExtensionsGetCommand(
        IProfileResolver profileResolver,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger,
        IMyJdTransport transport)
        : base(profileResolver, outputRenderer, diagnosticLogger)
    {
        _transport = transport;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(
        CommandContext context,
        SettingsExtensionsGetSettings settings,
        ResolvedProfileContext resolved,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Id) == string.IsNullOrWhiteSpace(settings.Name))
            throw CliException.Usage("settings extensions get requires exactly one of --id <id> or --name <name>.");

        var result = await _transport.ExecuteAsync(
            resolved,
            new MyJdRequestPlan(
                "settings.extensions.get",
                "POST",
                "/extensions/list",
                new Dictionary<string, object?>(),
                null,
                false,
                false,
                resolved.Device?.Id),
            cancellationToken);

        var items = ToDictionaryList(result.Data);
        var matches = string.IsNullOrWhiteSpace(settings.Id)
            ? items.Where(item => Matches(item, "name", settings.Name!)).ToList()
            : items.Where(item => Matches(item, "id", settings.Id!)).ToList();

        if (matches.Count == 0)
            throw CliException.NotFound("Requested extension was not found.");
        if (matches.Count > 1)
            throw CliException.Conflict("Requested extension matched multiple entries.");

        var selected = matches[0];
        return new CommandOutput(
            selected,
            JsonSerializer.Serialize(
                    selected,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true,
                    })
                .Split(Environment.NewLine),
            result.Warnings);
    }

    private static List<Dictionary<string, object?>> ToDictionaryList(object data)
    {
        if (data is IEnumerable<object?> sequence)
        {
            return sequence.OfType<Dictionary<string, object?>>().ToList();
        }

        return [];
    }

    private static bool Matches(Dictionary<string, object?> item, string key, string expected)
    {
        return item.TryGetValue(key, out var value)
            && value is not null
            && string.Equals(value.ToString(), expected, StringComparison.OrdinalIgnoreCase);
    }
}
