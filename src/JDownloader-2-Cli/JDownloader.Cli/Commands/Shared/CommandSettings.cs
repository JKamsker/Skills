using System.ComponentModel;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Shared;

public abstract class DeviceCommandSettings : GlobalSettings
{
}

public sealed class DeviceNoArgSettings : DeviceCommandSettings
{
}

public sealed class NoArgSettings : GlobalSettings
{
}

public class RequestCommandSettings : DeviceCommandSettings
{
    [CommandOption("--fields <CSV>")]
    [Description("Comma-separated field projection for query-style endpoints.")]
    public string? Fields { get; init; }

    [CommandOption("--limit <NUMBER>")]
    [Description("Maximum number of results.")]
    public int? Limit { get; init; }

    [CommandOption("--offset <NUMBER>")]
    [Description("Result offset.")]
    public int? Offset { get; init; }

    [CommandOption("--link-id <ID>")]
    [Description("Repeatable link identifier filter.")]
    public string[] LinkIds { get; init; } = [];

    [CommandOption("--package-id <ID>")]
    [Description("Repeatable package identifier filter.")]
    public string[] PackageIds { get; init; } = [];

    [CommandOption("--hoster <NAME>")]
    [Description("Repeatable hoster selector.")]
    public string[] Hosters { get; init; } = [];

    [CommandOption("--query-json <JSON>")]
    [Description("Raw query object JSON or @file override.")]
    public string? QueryJson { get; init; }

    [CommandOption("--body-json <JSON>")]
    [Description("Raw body object JSON or @file override.")]
    public string? BodyJson { get; init; }
}
