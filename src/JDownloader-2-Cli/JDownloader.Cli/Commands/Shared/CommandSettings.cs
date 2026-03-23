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

public sealed class RawRequestSettings : DeviceCommandSettings
{
    [CommandArgument(0, "<PATH>")]
    public required string Path { get; init; }

    [CommandOption("--method <METHOD>")]
    [Description("HTTP method to plan. Defaults to POST.")]
    public string? Method { get; init; }

    [CommandOption("--query-json <JSON>")]
    [Description("Raw query JSON or @file.")]
    public string? QueryJson { get; init; }

    [CommandOption("--body-json <JSON>")]
    [Description("Raw body JSON or @file.")]
    public string? BodyJson { get; init; }

    [CommandOption("--output-file <PATH>")]
    [Description("Destination for binary response modes.")]
    public string? OutputFile { get; init; }
}

public sealed class ProfileNameSettings : GlobalSettings
{
    [CommandArgument(0, "<NAME>")]
    public required string Name { get; init; }
}

public sealed class RenameProfileSettings : GlobalSettings
{
    [CommandArgument(0, "<OLD_NAME>")]
    public required string OldName { get; init; }

    [CommandArgument(1, "<NEW_NAME>")]
    public required string NewName { get; init; }
}

public sealed class LoginSettings : GlobalSettings
{
    [CommandOption("--email <EMAIL>")]
    [Description("My.JDownloader account email.")]
    public string? Email { get; init; }

    [CommandOption("--password-stdin")]
    [Description("Read the password from stdin without echo.")]
    public bool PasswordStdin { get; init; }
}

public sealed class DeviceUseSettings : GlobalSettings
{
    [CommandOption("--device-name <NAME>")]
    [Description("Optional friendly name when adding a new local device record.")]
    public string? DeviceName { get; init; }
}
