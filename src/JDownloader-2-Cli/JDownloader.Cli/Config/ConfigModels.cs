namespace JDownloader.Cli.Config;

public sealed class Jd2Config
{
    public string? DefaultProfile { get; set; }
    public Dictionary<string, ProfileRecord> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, CredentialRecord> Credentials { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProfileRecord
{
    public string? AccountEmail { get; set; }
    public string? DefaultDeviceId { get; set; }
    public string? DefaultDeviceName { get; set; }
    public string? Output { get; set; }
    public int? TimeoutSeconds { get; set; }
    public List<KnownDeviceRecord> KnownDevices { get; set; } = [];
}

public sealed class KnownDeviceRecord
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset SeenAtUtc { get; set; }
}

public sealed class CredentialRecord
{
    public ProtectedBlobRecord? AuthBlob { get; set; }
    public ProtectedBlobRecord? SessionBlob { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ProtectedBlobRecord
{
    public required string SaltBase64 { get; set; }
    public required string NonceBase64 { get; set; }
    public required string CiphertextBase64 { get; set; }
}

public sealed class StoredAuthMaterial
{
    public required string Email { get; set; }
    public required string DerivedSecretHex { get; set; }
    public required string StorageModel { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
