using System;
using System.Threading.Tasks;

namespace ExampleCli.Runtime;

public enum GuardDecision
{
    Continue,
    DryRunPrinted,
    // If the user declines, this helper throws CliException.Cancelled (exit 10).
}

// This example follows the base exit-code split:
// - exit 2: interaction-required refusal (quiet / non-TTY)
// - exit 10: explicit user cancellation (did not type "yes")
public sealed class DangerousActionGuard
{
    // This method is async to support an async dry-run preview action.
    public async Task<GuardDecision> AuthorizeAsync(
        GlobalOptions options,
        string prompt,
        Func<Task> dryRunAction)
    {
        if (options.DryRun)
        {
            await dryRunAction();
            return GuardDecision.DryRunPrinted;
        }

        if (options.Yes)
            return GuardDecision.Continue;

        if (options.OutputMode == OutputMode.Json)
            throw CliException.Usage(
                "Confirmation required. Use --yes to confirm or --dry-run to preview. Prompts are disabled in machine output modes (for example: --json).");

        if (options.Quiet || Console.IsInputRedirected || Console.IsErrorRedirected)
            throw CliException.Usage("Confirmation required. Use --yes to confirm or --dry-run to preview.");

        Console.Error.Write($"{prompt} Type 'yes' to confirm: ");
        Console.Error.Flush();

        var answer = (Console.ReadLine() ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        if (answer == "yes")
            return GuardDecision.Continue;

        throw CliException.Cancelled("Cancelled.");
    }
}
