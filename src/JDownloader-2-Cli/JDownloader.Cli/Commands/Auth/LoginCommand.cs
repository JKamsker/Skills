using System.ComponentModel;
using System.Text;
using JDownloader.Cli.Auth;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Auth;

public sealed class LoginSettings : GlobalSettings
{
    [CommandOption("--email <EMAIL>")]
    [Description("My.JDownloader account email.")]
    public string? Email { get; init; }

    [CommandOption("--password-stdin")]
    [Description("Read the password from stdin without echo.")]
    public bool PasswordStdin { get; init; }
}

public sealed class LoginCommand : AnonymousCommand<LoginSettings>
{
    private readonly IMyJdAuthService _authService;
    private readonly IProfileStore _profileStore;
    private readonly ICliEnvironment _environment;

    public LoginCommand(
        IMyJdAuthService authService,
        IProfileStore profileStore,
        ICliEnvironment environment,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _authService = authService;
        _profileStore = profileStore;
        _environment = environment;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, LoginSettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.Email))
            throw CliException.Usage("auth login requires --email <email>.");

        if ((settings.Json || settings.Quiet) && !settings.PasswordStdin)
        {
            throw CliException.Usage(
                "Non-interactive auth login requires --password-stdin.",
                "Pipe the password to stdin and re-run with --password-stdin.");
        }

        var password = settings.PasswordStdin
            ? await Console.In.ReadToEndAsync(cancellationToken)
            : ReadPasswordInteractively();
        password = password.TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(password))
            throw CliException.Usage("Password input was empty.");

        var profileName = await ResolveLoginProfileNameAsync(settings, cancellationToken);
        var result = await _authService.LoginAsync(settings.Email, password, profileName, cancellationToken);

        return new CommandOutput(
            new
            {
                profile = result.ProfileName,
                email = result.Email,
                configPath = result.ConfigPath,
                keyFilePath = result.KeyFilePath,
            },
            [
                $"Profile: {result.ProfileName}",
                $"Email: {result.Email}",
                $"Config: {result.ConfigPath}",
                $"Key file: {result.KeyFilePath}",
            ]);
    }

    private async Task<string> ResolveLoginProfileNameAsync(LoginSettings settings, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(settings.Profile))
            return settings.Profile.Trim();

        var envProfile = _environment.GetEnvironmentVariable("JD2_PROFILE");
        if (!string.IsNullOrWhiteSpace(envProfile))
            return envProfile.Trim();

        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(config.DefaultProfile))
            return config.DefaultProfile.Trim();

        if (config.Profiles.Count == 1)
            return config.Profiles.Keys.Single();

        return "default";
    }

    private static string ReadPasswordInteractively()
    {
        if (Console.IsInputRedirected || Console.IsErrorRedirected)
            throw CliException.Usage("Interactive password entry is unavailable in non-interactive mode.", "Use --password-stdin.");

        Console.Error.Write("Password: ");
        Console.Error.Flush();
        var builder = new StringBuilder();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace && builder.Length > 0)
            {
                builder.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar))
                builder.Append(key.KeyChar);
        }

        Console.Error.WriteLine();
        return builder.ToString();
    }
}
