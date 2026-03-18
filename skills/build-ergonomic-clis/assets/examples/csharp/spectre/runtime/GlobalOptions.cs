using System.ComponentModel;
using Spectre.Console.Cli;

namespace ExampleCli.Runtime;

public enum OutputMode
{
    Table,
    Json,
}

public class GlobalOptions : CommandSettings
{
    [CommandOption("-H|--host <URL>")]
    [Description("Service base URL")]
    public string? Host { get; init; }

    [CommandOption("--profile <NAME>")]
    [Description("Saved profile to use for host, auth, and defaults")]
    public string? Profile { get; init; }

    [CommandOption("--token <TOKEN>")]
    [Description("Access token override (prefer env or stdin; argv secrets can leak via shell history/process list)")]
    public string? Token { get; init; }

    [CommandOption("--json")]
    [Description("Emit JSON instead of human-readable tables")]
    public bool Json { get; init; }

    [CommandOption("-q|--quiet")]
    [Description("Suppress non-essential output and prompts; fail if confirmation is required")]
    public bool Quiet { get; init; }

    [CommandOption("--verbose")]
    [Description("Increase diagnostic detail")]
    public bool Verbose { get; init; }

    [CommandOption("--dry-run")]
    [Description("Print the request plan and exit without mutating")]
    public bool DryRun { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompts for destructive actions")]
    public bool Yes { get; init; }

    public OutputMode OutputMode => Json ? OutputMode.Json : OutputMode.Table;
}
