namespace JDownloader.Cli.Runtime;

public interface IConfirmationGuard
{
    Task<bool> AuthorizeAsync(GlobalSettings settings, string prompt, Func<Task<CommandOutput>> dryRunFactory);
}

public sealed class ConfirmationGuard : IConfirmationGuard
{
    private readonly IOutputRenderer _outputRenderer;
    private readonly ICliEnvironment _environment;

    public ConfirmationGuard(IOutputRenderer outputRenderer, ICliEnvironment environment)
    {
        _outputRenderer = outputRenderer;
        _environment = environment;
    }

    public async Task<bool> AuthorizeAsync(GlobalSettings settings, string prompt, Func<Task<CommandOutput>> dryRunFactory)
    {
        if (settings.DryRun)
        {
            var output = await dryRunFactory();
            var mode = settings.Json || string.Equals(settings.Output, "json", StringComparison.OrdinalIgnoreCase)
                ? OutputMode.Json
                : OutputMode.Human;
            _outputRenderer.WriteAnonymousSuccess(mode, output);
            return false;
        }

        if (settings.Yes)
            return true;

        if (settings.Quiet || _environment.IsInputRedirected || _environment.IsErrorRedirected)
        {
            throw CliException.Usage(
                "Confirmation required in non-interactive mode.",
                "Use --yes to confirm or --dry-run to preview.");
        }

        Console.Error.Write($"{prompt} Type 'yes' to confirm: ");
        Console.Error.Flush();
        var response = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.Equals(response, "yes", StringComparison.OrdinalIgnoreCase))
            return true;

        throw CliException.Cancelled("Cancelled.");
    }
}
