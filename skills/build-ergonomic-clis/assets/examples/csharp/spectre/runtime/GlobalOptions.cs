using System.ComponentModel;
using Spectre.Console.Cli;

namespace ExampleCli.Runtime;

public enum OutputMode
{
    Table,
    Json,
}

public sealed class GlobalOptions : CommandSettings
{
    [CommandOption("-H|--host <URL>")]
    [Description("Service base URL")]
    public string? Host { get; init; }

    [CommandOption("--profile <NAME>")]
    [Description("Saved profile to use for host, auth, and defaults")]
    public string? Profile { get; init; }

    [CommandOption("--token <TOKEN>")]
    [Description("Access token override")]
    public string? Token { get; init; }

    [CommandOption("--json")]
    [Description("Emit JSON instead of human-readable tables")]
    public bool Json { get; init; }

    [CommandOption("--quiet")]
    [Description("Suppress banners and prompts")]
    public bool Quiet { get; init; }

    [CommandOption("-v|--verbose")]
    [Description("Increase diagnostic detail")]
    public int Verbose { get; init; }

    [CommandOption("--dry-run")]
    [Description("Print the request plan and exit without mutating")]
    public bool DryRun { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompts for destructive actions")]
    public bool Yes { get; init; }

    public OutputMode OutputMode => Json ? OutputMode.Json : OutputMode.Table;
}
