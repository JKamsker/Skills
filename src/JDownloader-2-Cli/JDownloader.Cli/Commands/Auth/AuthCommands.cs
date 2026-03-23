using System.Text;
using JDownloader.Cli.Auth;
using JDownloader.Cli.Commands.Shared;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;
using Spectre.Console.Cli;

namespace JDownloader.Cli.Commands.Auth;

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

public sealed class LogoutCommand : AnonymousCommand<NoArgSettings>
{
    private readonly IMyJdAuthService _authService;
    private readonly IProfileResolver _profileResolver;

    public LogoutCommand(
        IMyJdAuthService authService,
        IProfileResolver profileResolver,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _authService = authService;
        _profileResolver = profileResolver;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, NoArgSettings settings, CancellationToken cancellationToken)
    {
        var resolved = await _profileResolver.ResolveAsync(settings, requireDevice: false, cancellationToken);
        await _authService.LogoutAsync(resolved.ProfileName, cancellationToken);
        return new CommandOutput(
            new { profile = resolved.ProfileName, loggedOut = true },
            [$"Removed stored auth for profile '{resolved.ProfileName}'."]);
    }
}

public sealed class AuthStatusCommand : AnonymousCommand<NoArgSettings>
{
    private readonly IMyJdAuthService _authService;
    private readonly IProfileResolver _profileResolver;

    public AuthStatusCommand(
        IMyJdAuthService authService,
        IProfileResolver profileResolver,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _authService = authService;
        _profileResolver = profileResolver;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, NoArgSettings settings, CancellationToken cancellationToken)
    {
        var resolved = await _profileResolver.ResolveAsync(settings, requireDevice: false, cancellationToken);
        var status = await _authService.GetStatusAsync(resolved.ProfileName, cancellationToken);
        return new CommandOutput(
            status,
            [
                $"Profile: {status.ProfileName}",
                $"Email: {status.Email ?? "(none)"}",
                $"Stored auth: {(status.HasStoredAuth ? "yes" : "no")}",
                $"Relay transport ready: {(status.TransportReady ? "yes" : "no")}",
            ]);
    }
}

public sealed class WhoAmICommand : AnonymousCommand<NoArgSettings>
{
    private readonly IProfileResolver _profileResolver;

    public WhoAmICommand(
        IProfileResolver profileResolver,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileResolver = profileResolver;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, NoArgSettings settings, CancellationToken cancellationToken)
    {
        var resolved = await _profileResolver.ResolveAsync(settings, requireDevice: false, cancellationToken);
        return new CommandOutput(
            new
            {
                profile = resolved.ProfileName,
                resolved.AccountEmail,
                resolved.ProfileSource,
                resolved.OutputMode,
                resolved.TimeoutSeconds,
            },
            [
                $"Profile: {resolved.ProfileName}",
                $"Email: {resolved.AccountEmail ?? "(none)"}",
                $"Profile source: {resolved.ProfileSource}",
                $"Output mode: {resolved.OutputMode}",
                $"Timeout: {resolved.TimeoutSeconds}s",
            ]);
    }
}

public sealed class ListProfilesCommand : AnonymousCommand<NoArgSettings>
{
    private readonly IProfileStore _profileStore;

    public ListProfilesCommand(IProfileStore profileStore, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, NoArgSettings settings, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        var items = config.Profiles
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new
            {
                name = pair.Key,
                isDefault = string.Equals(config.DefaultProfile, pair.Key, StringComparison.OrdinalIgnoreCase),
                pair.Value.AccountEmail,
                pair.Value.DefaultDeviceId,
                pair.Value.DefaultDeviceName,
                pair.Value.Output,
                pair.Value.TimeoutSeconds,
            })
            .ToList();

        var lines = items.Count == 0
            ? ["No profiles configured."]
            : items.Select(item =>
                    $"{item.name}{(item.isDefault ? " (default)" : string.Empty)}: email={item.AccountEmail ?? "(none)"}, device={item.DefaultDeviceName ?? item.DefaultDeviceId ?? "(none)"}")
                .ToArray();

        return new CommandOutput(items, lines);
    }
}

public sealed class GetProfileCommand : AnonymousCommand<ProfileNameSettings>
{
    private readonly IProfileStore _profileStore;

    public GetProfileCommand(IProfileStore profileStore, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, ProfileNameSettings settings, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.TryGetValue(settings.Name, out var profile))
            throw CliException.NotFound($"Profile '{settings.Name}' was not found.");

        return new CommandOutput(
            new
            {
                name = settings.Name,
                isDefault = string.Equals(config.DefaultProfile, settings.Name, StringComparison.OrdinalIgnoreCase),
                profile.AccountEmail,
                profile.DefaultDeviceId,
                profile.DefaultDeviceName,
                profile.Output,
                profile.TimeoutSeconds,
                profile.KnownDevices,
            },
            [
                $"Profile: {settings.Name}",
                $"Email: {profile.AccountEmail ?? "(none)"}",
                $"Default device: {profile.DefaultDeviceName ?? profile.DefaultDeviceId ?? "(none)"}",
                $"Known devices: {profile.KnownDevices.Count}",
            ]);
    }
}

public sealed class AddProfileCommand : AnonymousCommand<ProfileNameSettings>
{
    private readonly IProfileStore _profileStore;

    public AddProfileCommand(IProfileStore profileStore, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, ProfileNameSettings settings, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (config.Profiles.ContainsKey(settings.Name))
            throw CliException.Conflict($"Profile '{settings.Name}' already exists.");

        config.Profiles[settings.Name] = new ProfileRecord
        {
            Output = settings.Output,
            TimeoutSeconds = settings.TimeoutSeconds,
        };
        config.DefaultProfile ??= settings.Name;
        await _profileStore.SaveAsync(config, cancellationToken);

        return new CommandOutput(
            new { name = settings.Name, created = true },
            [$"Created profile '{settings.Name}'."]);
    }
}

public sealed class RenameProfileCommand : AnonymousCommand<RenameProfileSettings>
{
    private readonly IProfileStore _profileStore;

    public RenameProfileCommand(IProfileStore profileStore, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, RenameProfileSettings settings, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.Remove(settings.OldName, out var profile))
            throw CliException.NotFound($"Profile '{settings.OldName}' was not found.");
        if (config.Profiles.ContainsKey(settings.NewName))
            throw CliException.Conflict($"Profile '{settings.NewName}' already exists.");

        config.Profiles[settings.NewName] = profile;
        if (string.Equals(config.DefaultProfile, settings.OldName, StringComparison.OrdinalIgnoreCase))
            config.DefaultProfile = settings.NewName;

        await _profileStore.SaveAsync(config, cancellationToken);
        return new CommandOutput(
            new { oldName = settings.OldName, newName = settings.NewName },
            [$"Renamed profile '{settings.OldName}' to '{settings.NewName}'."]);
    }
}

public sealed class RemoveProfileCommand : AnonymousCommand<ProfileNameSettings>
{
    private readonly IProfileStore _profileStore;
    private readonly IConfirmationGuard _confirmationGuard;

    public RemoveProfileCommand(
        IProfileStore profileStore,
        IConfirmationGuard confirmationGuard,
        IOutputRenderer outputRenderer,
        IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
        _confirmationGuard = confirmationGuard;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, ProfileNameSettings settings, CancellationToken cancellationToken)
    {
        var proceed = await _confirmationGuard.AuthorizeAsync(
            settings,
            $"Remove profile '{settings.Name}'?",
            () => Task.FromResult(new CommandOutput(new { profile = settings.Name, preview = true }, [$"Would remove profile '{settings.Name}'." ])));
        if (!proceed)
            return new CommandOutput(new { preview = true });

        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.TryGetValue(settings.Name, out var profile))
            throw CliException.NotFound($"Profile '{settings.Name}' was not found.");

        config.Profiles.Remove(settings.Name);
        if (string.Equals(config.DefaultProfile, settings.Name, StringComparison.OrdinalIgnoreCase))
            config.DefaultProfile = config.Profiles.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(profile.AccountEmail))
            config.Credentials.Remove(profile.AccountEmail);

        await _profileStore.SaveAsync(config, cancellationToken);
        return new CommandOutput(new { profile = settings.Name, removed = true }, [$"Removed profile '{settings.Name}'."]);
    }
}

public sealed class UseProfileCommand : AnonymousCommand<ProfileNameSettings>
{
    private readonly IProfileStore _profileStore;

    public UseProfileCommand(IProfileStore profileStore, IOutputRenderer outputRenderer, IDiagnosticLogger diagnosticLogger)
        : base(outputRenderer, diagnosticLogger)
    {
        _profileStore = profileStore;
    }

    protected override async Task<CommandOutput> ExecuteCoreAsync(CommandContext context, ProfileNameSettings settings, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.ContainsKey(settings.Name))
            throw CliException.NotFound($"Profile '{settings.Name}' was not found.");

        config.DefaultProfile = settings.Name;
        await _profileStore.SaveAsync(config, cancellationToken);
        return new CommandOutput(new { defaultProfile = settings.Name }, [$"Default profile set to '{settings.Name}'."]);
    }
}
