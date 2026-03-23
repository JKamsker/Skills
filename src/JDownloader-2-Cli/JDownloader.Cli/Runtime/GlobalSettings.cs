using System.ComponentModel;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Runtime;

public enum OutputMode
{
    Human,
    Json,
}

public abstract class GlobalSettings : CommandSettings
{
    [CommandOption("--profile <NAME>")]
    [Description("Saved profile to use for auth, defaults, and output settings.")]
    public string? Profile { get; init; }

    [CommandOption("--device <VALUE>")]
    [Description("Device id or exact device name override.")]
    public string? Device { get; init; }

    [CommandOption("--json")]
    [Description("Emit the default stable JSON envelope contract (v1).")]
    public bool Json { get; init; }

    [CommandOption("--output <MODE>")]
    [Description("Output mode override: human or json.")]
    public string? Output { get; init; }

    [CommandOption("--verbose")]
    [Description("Increase diagnostic detail on stderr.")]
    public bool Verbose { get; init; }

    [CommandOption("--quiet")]
    [Description("Suppress prompts and non-essential stderr chatter.")]
    public bool Quiet { get; init; }

    [CommandOption("--dry-run")]
    [Description("Print the resolved request plan and exit without mutating.")]
    public bool DryRun { get; init; }

    [CommandOption("-y|--yes")]
    [Description("Skip confirmation prompts for destructive operations.")]
    public bool Yes { get; init; }

    [CommandOption("--timeout <SECONDS>")]
    [Description("Timeout override in seconds.")]
    public int? TimeoutSeconds { get; init; }

    [CommandOption("--no-color")]
    [Description("Disable ANSI color output.")]
    public bool NoColor { get; init; }

    public bool HasMachineOutputSelector => Json || !string.IsNullOrWhiteSpace(Output);
}
