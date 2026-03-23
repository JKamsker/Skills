using System.Security.Cryptography;
using System.Text;
using JDownloader.Cli.Config;

namespace JDownloader.Cli.Auth;

public interface IMyJdAuthService
{
    Task<LoginResult> LoginAsync(string email, string password, string profileName, CancellationToken cancellationToken);
    Task LogoutAsync(string profileName, CancellationToken cancellationToken);
    Task<AuthStatusResult> GetStatusAsync(string profileName, CancellationToken cancellationToken);
}

public sealed record LoginResult(string Email, string ProfileName, string ConfigPath, string KeyFilePath);
public sealed record AuthStatusResult(string ProfileName, string? Email, bool HasStoredAuth, bool TransportReady, DateTimeOffset? UpdatedAtUtc);

public sealed class MyJdAuthService : IMyJdAuthService
{
    private readonly IProfileStore _profileStore;
    private readonly ICredentialProtector _protector;
    private readonly IKeyFileProvider _keyFileProvider;
    private readonly CliPathProvider _paths;

    public MyJdAuthService(
        IProfileStore profileStore,
        ICredentialProtector protector,
        IKeyFileProvider keyFileProvider,
        CliPathProvider paths)
    {
        _profileStore = profileStore;
        _protector = protector;
        _keyFileProvider = keyFileProvider;
        _paths = paths;
    }

    public async Task<LoginResult> LoginAsync(string email, string password, string profileName, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.TryGetValue(profileName, out var profile))
        {
            profile = new ProfileRecord();
            config.Profiles[profileName] = profile;
        }

        var authMaterial = new StoredAuthMaterial
        {
            Email = normalizedEmail,
            DerivedSecretHex = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes($"{normalizedEmail}:{password}"))).ToLowerInvariant(),
            StorageModel = "config+sidecar-keyfile",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        profile.AccountEmail = normalizedEmail;
        config.DefaultProfile ??= profileName;
        config.Credentials[normalizedEmail] = new CredentialRecord
        {
            AuthBlob = await _protector.ProtectAsync(authMaterial, cancellationToken),
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };

        await _profileStore.SaveAsync(config, cancellationToken);
        await _keyFileProvider.GetOrCreateKeyAsync(cancellationToken);

        return new LoginResult(normalizedEmail, profileName, _paths.GetConfigFilePath(), _paths.GetKeyFilePath());
    }

    public async Task LogoutAsync(string profileName, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.TryGetValue(profileName, out var profile) || string.IsNullOrWhiteSpace(profile.AccountEmail))
            return;

        config.Credentials.Remove(NormalizeEmail(profile.AccountEmail));
        await _profileStore.SaveAsync(config, cancellationToken);
    }

    public async Task<AuthStatusResult> GetStatusAsync(string profileName, CancellationToken cancellationToken)
    {
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.TryGetValue(profileName, out var profile) || string.IsNullOrWhiteSpace(profile.AccountEmail))
            return new AuthStatusResult(profileName, null, false, false, null);

        var email = NormalizeEmail(profile.AccountEmail);
        config.Credentials.TryGetValue(email, out var credential);
        return new AuthStatusResult(profileName, email, credential?.AuthBlob is not null, false, credential?.UpdatedAtUtc);
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
