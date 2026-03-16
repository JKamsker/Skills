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

    [CommandOption("--quiet")]
    [Description("Suppress banners and prompts")]
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

    // In a real implementation, SIGINT/CancelKeyPress wiring should set this to true so that
    // user cancellation maps to exit 10 instead of being treated like a timeout/network error.
    public bool IsUserCancellation { get; set; }

    public OutputMode OutputMode => Json ? OutputMode.Json : OutputMode.Table;
}
