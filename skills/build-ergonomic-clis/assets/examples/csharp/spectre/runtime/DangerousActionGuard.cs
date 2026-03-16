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
// - exit 10: explicit user cancellation (answered "no")
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
            throw CliException.Usage("Confirmation required. Re-run with --yes or --dry-run. Prompts are disabled in --json mode.");

        if (options.Quiet || Console.IsInputRedirected || Console.IsErrorRedirected)
            throw CliException.Usage("Confirmation required. Re-run with --yes or --dry-run.");

        Console.Error.Write($"{prompt} [y/N]: ");
        Console.Error.Flush();

        var answer = (Console.ReadLine() ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        if (answer is "y" or "yes")
            return GuardDecision.Continue;

        throw CliException.Cancelled("Cancelled.");
    }
}
