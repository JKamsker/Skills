namespace ExampleCli.Runtime;

public enum GuardDecision
{
    Continue,
    DryRunPrinted,
    Cancelled,
}

public sealed class DangerousActionGuard
{
    public async Task<GuardDecision> AuthorizeAsync(
        GlobalOptions options,
        string prompt,
        Func<Task> dryRunAction,
        CancellationToken cancellationToken = default)
    {
        if (options.DryRun)
        {
            await dryRunAction();
            return GuardDecision.DryRunPrinted;
        }

        if (options.Yes)
            return GuardDecision.Continue;

        if (options.Quiet || Console.IsInputRedirected || Console.IsErrorRedirected)
            throw CliException.Cancelled("Confirmation required. Re-run with --yes or --dry-run.");

        Console.Error.Write($"{prompt} [y/N]: ");
        Console.Error.Flush();

        var answer = (await Task.Run(Console.ReadLine, cancellationToken) ?? string.Empty)
            .Trim()
            .ToLowerInvariant();

        return answer is "y" or "yes"
            ? GuardDecision.Continue
            : GuardDecision.Cancelled;
    }
}
