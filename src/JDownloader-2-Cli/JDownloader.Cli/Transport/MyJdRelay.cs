using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JDownloader.Cli.Config;
using JDownloader.Cli.Runtime;

namespace JDownloader.Cli.Transport;

public sealed record MyJdDeviceSummary(string Id, string Name, string? Type, string? Status);

public interface IDeviceCatalog
{
    Task<IReadOnlyList<ResolvedDevice>> SyncAsync(string profileName, string? accountEmail, int timeoutSeconds, CancellationToken cancellationToken);
}

public interface IMyJdRelayClient
{
    Task<IReadOnlyList<MyJdDeviceSummary>> ListDevicesAsync(string profileName, string? accountEmail, int timeoutSeconds, CancellationToken cancellationToken);
    Task<object?> InvokeAsync(ResolvedProfileContext resolved, string endpoint, object? parameters, CancellationToken cancellationToken);
}

public sealed class DeviceCatalog : IDeviceCatalog
{
    private readonly IMyJdRelayClient _relayClient;
    private readonly IProfileStore _profileStore;

    public DeviceCatalog(IMyJdRelayClient relayClient, IProfileStore profileStore)
    {
        _relayClient = relayClient;
        _profileStore = profileStore;
    }

    public async Task<IReadOnlyList<ResolvedDevice>> SyncAsync(
        string profileName,
        string? accountEmail,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountEmail))
            return [];

        var liveDevices = await _relayClient.ListDevicesAsync(profileName, accountEmail, timeoutSeconds, cancellationToken);
        var resolved = liveDevices
            .Where(device => !string.IsNullOrWhiteSpace(device.Id))
            .Select(device => new ResolvedDevice(device.Id, string.IsNullOrWhiteSpace(device.Name) ? device.Id : device.Name))
            .ToList();

        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Profiles.TryGetValue(profileName, out var profile))
        {
            profile = new ProfileRecord();
            config.Profiles[profileName] = profile;
        }

        var existing = profile.KnownDevices.ToDictionary(device => device.Id, StringComparer.Ordinal);
        foreach (var device in resolved)
        {
            existing[device.Id] = new KnownDeviceRecord
            {
                Id = device.Id,
                Name = device.Name,
                SeenAtUtc = DateTimeOffset.UtcNow,
            };
        }

        profile.KnownDevices = existing.Values
            .OrderBy(device => device.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(device => device.Id, StringComparer.Ordinal)
            .ToList();

        if (resolved.Count == 1 && string.IsNullOrWhiteSpace(profile.DefaultDeviceId) && string.IsNullOrWhiteSpace(profile.DefaultDeviceName))
        {
            profile.DefaultDeviceId = resolved[0].Id;
            profile.DefaultDeviceName = resolved[0].Name;
        }
        else if (!string.IsNullOrWhiteSpace(profile.DefaultDeviceId)
            && existing.TryGetValue(profile.DefaultDeviceId, out var defaultDevice))
        {
            profile.DefaultDeviceName = defaultDevice.Name;
        }

        await _profileStore.SaveAsync(config, cancellationToken);
        return resolved;
    }
}

public sealed class LiveMyJdTransport : IMyJdTransport
{
    private readonly IMyJdRelayClient _relayClient;

    public LiveMyJdTransport(IMyJdRelayClient relayClient)
    {
        _relayClient = relayClient;
    }

    public async Task<MyJdTransportResult> ExecuteAsync(ResolvedProfileContext resolved, MyJdRequestPlan plan, CancellationToken cancellationToken)
    {
        var (parameters, warnings) = MyJdParameterMapper.Build(plan);
        var data = await _relayClient.InvokeAsync(resolved, plan.Endpoint, parameters, cancellationToken);
        return new MyJdTransportResult(data ?? Array.Empty<object>(), warnings);
    }
}

public sealed class MyJdRelayClient : IMyJdRelayClient
{
    private const string ApiBaseUrl = "https://api.jdownloader.org";
    private const string AppKey = "jd2-cli";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _httpClient = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly IProfileStore _profileStore;
    private readonly ICredentialProtector _protector;
    private readonly IRequestIdProvider _requestIdProvider;

    public MyJdRelayClient(
        IProfileStore profileStore,
        ICredentialProtector protector,
        IRequestIdProvider requestIdProvider)
    {
        _profileStore = profileStore;
        _protector = protector;
        _requestIdProvider = requestIdProvider;
    }

    public async Task<IReadOnlyList<MyJdDeviceSummary>> ListDevicesAsync(
        string profileName,
        string? accountEmail,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CreateTimeoutSource(timeoutSeconds, cancellationToken);
        try
        {
            var auth = await LoadAuthAsync(profileName, accountEmail, timeoutCts.Token);
            var session = await ConnectAsync(auth, timeoutCts.Token);
            using var document = await SendServerGetAsync(
                $"/my/listdevices?sessiontoken={Uri.EscapeDataString(session.SessionToken)}",
                session.ServerEncryptionToken,
                session.ServerEncryptionToken,
                timeoutCts.Token);

            if (!TryGetProperty(document.RootElement, "list", out var listElement) || listElement.ValueKind != JsonValueKind.Array)
                return [];

            var devices = new List<MyJdDeviceSummary>();
            foreach (var item in listElement.EnumerateArray())
            {
                var id = GetString(item, "id");
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                devices.Add(new MyJdDeviceSummary(
                    id,
                    GetString(item, "name") ?? id,
                    GetString(item, "type"),
                    GetString(item, "status")));
            }

            return devices;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw CliException.Transport($"Timed out after {timeoutSeconds}s contacting My.JDownloader.");
        }
        catch (CliException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or CryptographicException or JsonException or FormatException)
        {
            throw CliException.Transport($"My.JDownloader device discovery failed: {ex.Message}");
        }
    }

    public async Task<object?> InvokeAsync(ResolvedProfileContext resolved, string endpoint, object? parameters, CancellationToken cancellationToken)
    {
        if (resolved.Device is null)
            throw CliException.Usage("Device is required because no default device could be resolved.");

        using var timeoutCts = CreateTimeoutSource(resolved.TimeoutSeconds, cancellationToken);
        try
        {
            var auth = await LoadAuthAsync(resolved.ProfileName, resolved.AccountEmail, timeoutCts.Token);
            var session = await ConnectAsync(auth, timeoutCts.Token);
            using var document = await SendDeviceActionAsync(session, resolved.Device.Id, endpoint, parameters, timeoutCts.Token);
            return ExtractDataOrWhole(document.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw CliException.Transport($"Timed out after {resolved.TimeoutSeconds}s contacting My.JDownloader.");
        }
        catch (CliException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or CryptographicException or JsonException or FormatException)
        {
            throw CliException.Transport($"My.JDownloader relay call failed: {ex.Message}");
        }
    }

    private async Task<StoredRelayAuth> LoadAuthAsync(string profileName, string? accountEmail, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountEmail))
        {
            throw CliException.NotAuthenticated(
                "Authentication required for protected commands.",
                $"Run 'jd2 auth login --profile {profileName} --email <email> --password-stdin'.");
        }

        var normalizedEmail = accountEmail.Trim().ToLowerInvariant();
        var config = await _profileStore.LoadAsync(cancellationToken);
        if (!config.Credentials.TryGetValue(normalizedEmail, out var credential) || credential.AuthBlob is null)
        {
            throw CliException.NotAuthenticated(
                "Authentication required for protected commands.",
                $"Run 'jd2 auth login --profile {profileName} --email {normalizedEmail} --password-stdin'.");
        }

        StoredAuthMaterial? authMaterial;
        try
        {
            authMaterial = await _protector.UnprotectAsync<StoredAuthMaterial>(credential.AuthBlob, cancellationToken);
        }
        catch (CryptographicException)
        {
            authMaterial = null;
        }
        catch (FormatException)
        {
            authMaterial = null;
        }

        if (authMaterial is null)
        {
            throw CliException.NotAuthenticated(
                "Stored auth material could not be decrypted.",
                $"Run 'jd2 auth login --profile {profileName} --email {normalizedEmail} --password-stdin'.");
        }

        if (string.IsNullOrWhiteSpace(authMaterial.ServerSecretHex) || string.IsNullOrWhiteSpace(authMaterial.DeviceSecretHex))
        {
            throw CliException.NotAuthenticated(
                "Saved auth material is from the initial scaffold and cannot authenticate live relay calls.",
                $"Run 'jd2 auth login --profile {profileName} --email {normalizedEmail} --password-stdin' once to refresh it.");
        }

        return new StoredRelayAuth(
            normalizedEmail,
            HexToBytes(authMaterial.ServerSecretHex),
            HexToBytes(authMaterial.DeviceSecretHex));
    }

    private async Task<MyJdSession> ConnectAsync(StoredRelayAuth auth, CancellationToken cancellationToken)
    {
        using var document = await SendServerGetAsync(
            $"/my/connect?email={Uri.EscapeDataString(auth.Email)}&appkey={Uri.EscapeDataString(AppKey)}",
            auth.ServerSecret,
            auth.ServerSecret,
            cancellationToken);

        var sessionToken = GetRequiredString(document.RootElement, "sessiontoken");
        var serverEncryptionToken = UpdateEncryptionToken(auth.ServerSecret, sessionToken);
        var deviceEncryptionToken = UpdateEncryptionToken(auth.DeviceSecret, sessionToken);
        return new MyJdSession(sessionToken, serverEncryptionToken, deviceEncryptionToken);
    }

    private async Task<JsonDocument> SendServerGetAsync(string relativePath, byte[] signingKey, byte[] responseKey, CancellationToken cancellationToken)
    {
        var rid = _requestIdProvider.Next();
        var pathWithRid = relativePath.Contains('?')
            ? $"{relativePath}&rid={rid}"
            : $"{relativePath}?rid={rid}";
        var signature = ComputeSignature(pathWithRid, signingKey);
        var uri = new Uri($"{ApiBaseUrl}{pathWithRid}&signature={signature}");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response.StatusCode, content, "My.JDownloader server call");

        var plaintext = DecryptBase64(content, responseKey);
        return JsonDocument.Parse(plaintext);
    }

    private async Task<JsonDocument> SendDeviceActionAsync(
        MyJdSession session,
        string deviceId,
        string endpoint,
        object? parameters,
        CancellationToken cancellationToken)
    {
        var action = new MyJdActionEnvelope
        {
            ApiVer = 1,
            Params = parameters,
            RequestId = _requestIdProvider.Next(),
            Url = endpoint,
        };

        var uri = new Uri($"{ApiBaseUrl}/t_{Uri.EscapeDataString(session.SessionToken)}_{Uri.EscapeDataString(deviceId)}{endpoint}");
        var plaintext = JsonSerializer.Serialize(action, JsonOptions);
        var ciphertext = EncryptBase64(plaintext, session.DeviceEncryptionToken);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(ciphertext, Encoding.UTF8, "application/aesjson-jd"),
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/aesjson-jd");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        EnsureSuccess(response.StatusCode, content, $"My.JDownloader device call '{endpoint}'");

        var decrypted = DecryptBase64(content, session.DeviceEncryptionToken);
        return JsonDocument.Parse(decrypted);
    }

    private static object? ExtractDataOrWhole(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object && TryGetProperty(element, "data", out var dataElement))
            return ConvertElement(dataElement);

        return ConvertElement(element);
    }

    private static object? ConvertElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                property => property.Name,
                property => ConvertElement(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText(),
        };
    }

    private static void EnsureSuccess(HttpStatusCode statusCode, string body, string operation)
    {
        if ((int)statusCode is >= 200 and < 300)
            return;

        var preview = string.IsNullOrWhiteSpace(body) ? "(empty response)" : body.Trim();
        if (preview.Length > 200)
            preview = preview[..200] + "...";

        throw CliException.Transport($"{operation} failed with HTTP {(int)statusCode}.", preview);
    }

    private static CancellationTokenSource CreateTimeoutSource(int timeoutSeconds, CancellationToken cancellationToken)
    {
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));
        return timeoutCts;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        throw CliException.Transport($"My.JDownloader response did not include '{propertyName}'.");
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return TryGetProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        property = default;
        return false;
    }

    private static string ComputeSignature(string data, byte[] key)
    {
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private static byte[] UpdateEncryptionToken(byte[] secret, string sessionTokenHex)
    {
        var sessionBytes = HexToBytes(sessionTokenHex);
        var combined = new byte[secret.Length + sessionBytes.Length];
        Buffer.BlockCopy(secret, 0, combined, 0, secret.Length);
        Buffer.BlockCopy(sessionBytes, 0, combined, secret.Length, sessionBytes.Length);
        return SHA256.HashData(combined);
    }

    private static string EncryptBase64(string plaintext, byte[] keyMaterial)
    {
        using var aes = CreateAes(keyMaterial);
        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);
        return Convert.ToBase64String(ciphertext);
    }

    private static string DecryptBase64(string ciphertextBase64, byte[] keyMaterial)
    {
        using var aes = CreateAes(keyMaterial);
        using var decryptor = aes.CreateDecryptor();
        var ciphertext = Convert.FromBase64String(ciphertextBase64);
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
        return Encoding.UTF8.GetString(plaintextBytes);
    }

    private static Aes CreateAes(byte[] keyMaterial)
    {
        if (keyMaterial.Length < 32)
            throw new CryptographicException("My.JDownloader key material was shorter than expected.");

        var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.IV = keyMaterial[..16];
        aes.Key = keyMaterial[16..32];
        return aes;
    }

    private static byte[] HexToBytes(string hex)
    {
        return Convert.FromHexString(hex.Replace("-", string.Empty, StringComparison.Ordinal));
    }

    private sealed record StoredRelayAuth(string Email, byte[] ServerSecret, byte[] DeviceSecret);
    private sealed record MyJdSession(string SessionToken, byte[] ServerEncryptionToken, byte[] DeviceEncryptionToken);

    private sealed class MyJdActionEnvelope
    {
        [JsonPropertyName("ApiVer")]
        public int ApiVer { get; set; }

        [JsonPropertyName("params")]
        public object? Params { get; set; }

        [JsonPropertyName("rid")]
        public long RequestId { get; set; }

        [JsonPropertyName("url")]
        public required string Url { get; set; }
    }
}

internal static class MyJdParameterMapper
{
    private static readonly string[] GrabberLinkFields =
    [
        "availability",
        "bytesTotal",
        "comment",
        "enabled",
        "host",
        "password",
        "priority",
        "status",
        "url",
        "variantID",
        "variantIcon",
        "variantName",
        "variants",
    ];

    public static (object? Parameters, IReadOnlyList<string>? Warnings) Build(MyJdRequestPlan plan)
    {
        return plan.Endpoint switch
        {
            "/linkgrabberv2/queryLinks" => BuildJsonStringParameter(
                BuildGrabberLinksQuery(plan.Query, out var warnings),
                warnings),
            "/linkgrabberv2/queryPackages" => BuildJsonStringParameter(
                BuildGrabberPackagesQuery(plan.Query, out var warnings),
                warnings),
            "/downloadsV2/queryLinks" => BuildJsonStringParameter(
                BuildDownloadsLinksQuery(plan.Query, out var warnings),
                warnings),
            _ => BuildGenericParameters(plan),
        };
    }

    private static (object? Parameters, IReadOnlyList<string>? Warnings) BuildJsonStringParameter(
        object queryObject,
        IReadOnlyList<string>? warnings)
    {
        return (new object?[] { JsonSerializer.Serialize(queryObject) }, warnings);
    }

    private static (object? Parameters, IReadOnlyList<string>? Warnings) BuildGenericParameters(MyJdRequestPlan plan)
    {
        var queryIsEmpty = IsEmpty(plan.Query);
        var bodyIsEmpty = IsEmpty(plan.Body);
        if (queryIsEmpty && bodyIsEmpty)
            return (null, null);
        if (bodyIsEmpty)
            return (new object?[] { plan.Query }, null);
        if (queryIsEmpty)
            return (new object?[] { plan.Body }, null);

        return (new object?[] { plan.Query, plan.Body }, null);
    }

    private static object BuildGrabberLinksQuery(object? query, out IReadOnlyList<string>? warnings)
    {
        var projection = CreateProjection(GrabberLinkFields, includeByDefault: true);
        projection["maxResults"] = -1;
        projection["startAt"] = 0;
        return BuildQueryObject(query, projection, out warnings, "packageUUIDs");
    }

    private static object BuildDownloadsLinksQuery(object? query, out IReadOnlyList<string>? warnings)
    {
        var projection = CreateProjection(
            ["addedDate", "bytesLoaded", "bytesTotal", "comment", "enabled", "eta", "extractionStatus", "finished", "finishedDate", "host", "password", "priority", "running", "saveTo", "skipped", "speed", "status", "url"],
            includeByDefault: true);
        projection["maxResults"] = 20;
        projection["startAt"] = 0;
        return BuildQueryObject(query, projection, out warnings, "packageUUIDs", "jobUUIDs");
    }

    private static object BuildGrabberPackagesQuery(object? query, out IReadOnlyList<string>? warnings)
    {
        var projection = CreateProjection(
            ["availableOfflineCount", "availableOnlineCount", "availableTempUnknownCount", "availableUnknownCount", "bytesTotal", "childCount", "comment", "enabled", "hosts", "priority", "saveTo", "status"],
            includeByDefault: true);
        projection["maxResults"] = -1;
        projection["startAt"] = 0;
        return BuildQueryObject(query, projection, out warnings, "packageUUIDs");
    }

    private static object BuildQueryObject(
        object? query,
        Dictionary<string, object?> defaults,
        out IReadOnlyList<string>? warnings,
        params string[] longArrayFields)
    {
        warnings = null;
        if (query is not Dictionary<string, object?> values || values.Count == 0)
            return defaults;

        if (values.TryGetValue("queryOverride", out var queryOverride) && queryOverride is not null)
            return queryOverride;

        var result = new Dictionary<string, object?>(defaults, StringComparer.OrdinalIgnoreCase);
        var localWarnings = new List<string>();

        if (values.TryGetValue("limit", out var limit) && TryReadInt(limit, out var maxResults))
            result["maxResults"] = maxResults;

        if (values.TryGetValue("offset", out var offset) && TryReadInt(offset, out var startAt))
            result["startAt"] = startAt;

        if (values.TryGetValue("fields", out var fields))
            ApplyProjectionFields(result, fields, localWarnings);

        foreach (var fieldName in longArrayFields)
        {
            var selectorKey = fieldName.Equals("packageUUIDs", StringComparison.OrdinalIgnoreCase) ? "packageIds" : fieldName;
            if (values.TryGetValue(selectorKey, out var rawValues) && TryReadLongArray(rawValues, out var longValues))
                result[fieldName] = longValues;
        }

        if (values.TryGetValue("hosters", out var hosters) && !IsEmpty(hosters))
            localWarnings.Add("The current live mapper does not translate --hoster for this endpoint.");
        if (values.TryGetValue("linkIds", out var linkIds) && !IsEmpty(linkIds))
            localWarnings.Add("The current live mapper does not translate --link-id for this endpoint.");

        warnings = localWarnings.Count == 0 ? null : localWarnings;
        return result;
    }

    private static Dictionary<string, object?> CreateProjection(IEnumerable<string> fieldNames, bool includeByDefault)
    {
        var projection = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var fieldName in fieldNames)
            projection[fieldName] = includeByDefault;
        return projection;
    }

    private static void ApplyProjectionFields(Dictionary<string, object?> target, object? rawFields, List<string> warnings)
    {
        var requestedFields = ToStringList(rawFields);
        if (requestedFields.Count == 0)
            return;

        var projectionKeys = target.Keys.Where(key => target[key] is bool).ToList();
        foreach (var key in projectionKeys)
            target[key] = false;

        foreach (var field in requestedFields)
        {
            var key = projectionKeys.FirstOrDefault(candidate => string.Equals(candidate, field, StringComparison.OrdinalIgnoreCase))
                ?? projectionKeys.FirstOrDefault(candidate => string.Equals(candidate, NormalizeFieldAlias(field), StringComparison.OrdinalIgnoreCase));
            if (key is null)
            {
                warnings.Add($"Unknown projection field '{field}' was ignored.");
                continue;
            }

            target[key] = true;
        }
    }

    private static string NormalizeFieldAlias(string field)
    {
        return field switch
        {
            "variantId" => "variantID",
            "jobUuids" => "jobUUIDs",
            "packageUuids" => "packageUUIDs",
            _ => field,
        };
    }

    private static bool TryReadInt(object? value, out int number)
    {
        switch (value)
        {
            case int intValue:
                number = intValue;
                return true;
            case long longValue when longValue is >= int.MinValue and <= int.MaxValue:
                number = (int)longValue;
                return true;
            case string stringValue when int.TryParse(stringValue, out var parsed):
                number = parsed;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static bool TryReadLongArray(object? value, out long[] numbers)
    {
        var items = new List<long>();
        foreach (var entry in EnumerateValues(value))
        {
            if (entry is long longValue)
            {
                items.Add(longValue);
                continue;
            }

            if (entry is int intValue)
            {
                items.Add(intValue);
                continue;
            }

            if (entry is string stringValue && long.TryParse(stringValue, out var parsed))
            {
                items.Add(parsed);
                continue;
            }

            numbers = [];
            return false;
        }

        numbers = items.ToArray();
        return numbers.Length > 0;
    }

    private static IReadOnlyList<string> ToStringList(object? value)
    {
        return EnumerateValues(value)
            .Select(item => item?.ToString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }

    private static IEnumerable<object?> EnumerateValues(object? value)
    {
        return value switch
        {
            null => [],
            string => [value],
            IEnumerable<object?> objectValues => objectValues,
            Array array => array.Cast<object?>(),
            _ => [value],
        };
    }

    private static bool IsEmpty(object? value)
    {
        return value switch
        {
            null => true,
            string stringValue => string.IsNullOrWhiteSpace(stringValue),
            Dictionary<string, object?> dictionary => dictionary.Count == 0,
            IEnumerable<object?> items => !items.Any(),
            Array array => array.Length == 0,
            _ => false,
        };
    }
}
