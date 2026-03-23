using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JDownloader.Cli.Config;

public interface IKeyFileProvider
{
    Task<byte[]> GetOrCreateKeyAsync(CancellationToken cancellationToken);
    string GetKeyFilePath();
}

public interface ICredentialProtector
{
    Task<ProtectedBlobRecord> ProtectAsync<T>(T value, CancellationToken cancellationToken);
    Task<T?> UnprotectAsync<T>(ProtectedBlobRecord? blob, CancellationToken cancellationToken);
}

public sealed class FileKeyFileProvider : IKeyFileProvider
{
    private readonly CliPathProvider _paths;

    public FileKeyFileProvider(CliPathProvider paths)
    {
        _paths = paths;
    }

    public async Task<byte[]> GetOrCreateKeyAsync(CancellationToken cancellationToken)
    {
        var path = GetKeyFilePath();
        if (File.Exists(path))
        {
            var pem = await File.ReadAllTextAsync(path, cancellationToken);
            return Convert.FromBase64String(pem);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var key = RandomNumberGenerator.GetBytes(32);
        await File.WriteAllTextAsync(path, Convert.ToBase64String(key), cancellationToken);
        return key;
    }

    public string GetKeyFilePath() => _paths.GetKeyFilePath();
}

public sealed class AesCredentialProtector : ICredentialProtector
{
    private static readonly byte[] AppContext = Encoding.UTF8.GetBytes("jd2-cli-sidecar-kdf-v1");
    private readonly IKeyFileProvider _keyFileProvider;

    public AesCredentialProtector(IKeyFileProvider keyFileProvider)
    {
        _keyFileProvider = keyFileProvider;
    }

    public async Task<ProtectedBlobRecord> ProtectAsync<T>(T value, CancellationToken cancellationToken)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var key = await DeriveKeyAsync(salt, cancellationToken);
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, AppContext);

        var combined = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, combined, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, combined, ciphertext.Length, tag.Length);

        return new ProtectedBlobRecord
        {
            SaltBase64 = Convert.ToBase64String(salt),
            NonceBase64 = Convert.ToBase64String(nonce),
            CiphertextBase64 = Convert.ToBase64String(combined),
        };
    }

    public async Task<T?> UnprotectAsync<T>(ProtectedBlobRecord? blob, CancellationToken cancellationToken)
    {
        if (blob is null)
            return default;

        var combined = Convert.FromBase64String(blob.CiphertextBase64);
        var tagLength = AesGcm.TagByteSizes.MaxSize;
        if (combined.Length < tagLength)
            return default;

        var ciphertextLength = combined.Length - tagLength;
        var ciphertext = new byte[ciphertextLength];
        var tag = new byte[tagLength];
        Buffer.BlockCopy(combined, 0, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(combined, ciphertextLength, tag, 0, tagLength);

        var nonce = Convert.FromBase64String(blob.NonceBase64);
        var salt = Convert.FromBase64String(blob.SaltBase64);
        var key = await DeriveKeyAsync(salt, cancellationToken);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, AppContext);
        return JsonSerializer.Deserialize<T>(plaintext);
    }

    private async Task<byte[]> DeriveKeyAsync(byte[] salt, CancellationToken cancellationToken)
    {
        var sidecarKey = await _keyFileProvider.GetOrCreateKeyAsync(cancellationToken);
        return Rfc2898DeriveBytes.Pbkdf2(sidecarKey, salt, 100_000, HashAlgorithmName.SHA256, 32);
    }
}
