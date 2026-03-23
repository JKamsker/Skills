using JDownloader.Cli.Tests.Support;

namespace JDownloader.Cli.Tests;

public sealed class AuthAndMachineModeTests
{
    [Fact]
    public async Task AuthLoginJsonRequiresPasswordStdinAndWritesConfigArtifacts()
    {
        var home = CreateTempPath();
        var configRoot = Path.Combine(home, "cfg");
        var keyFile = Path.Combine(home, "secrets", "keyfile.pem");
        var env = new FakeCliEnvironment(home, new Dictionary<string, string>
        {
            ["JD2_CONFIG"] = configRoot,
            ["JD2_KEYFILE"] = keyFile,
        });

        var refused = await CliTestHarness.RunAsync(env, ["auth", "login", "--json", "--email", "user@example.com"]);
        Assert.Equal(2, refused.ExitCode);
        Assert.Contains("\"ok\": false", refused.StdOut);
        Assert.Contains("password-stdin", refused.StdOut, StringComparison.OrdinalIgnoreCase);

        var success = await CliTestHarness.RunAsync(
            env,
            ["auth", "login", "--json", "--email", "user@example.com", "--password-stdin"],
            "secret-value\n");

        Assert.Equal(0, success.ExitCode);
        Assert.Contains("\"ok\": true", success.StdOut);
        Assert.True(File.Exists(Path.Combine(configRoot, "config.json")));
        Assert.True(File.Exists(keyFile));

        var status = await CliTestHarness.RunAsync(env, ["auth", "status", "--json"]);
        Assert.Equal(0, status.ExitCode);
        Assert.Contains("\"transportReady\": true", status.StdOut);
    }

    [Fact]
    public async Task ProtectedCommandReturnsJsonEnvelopeForMissingAuth()
    {
        var home = CreateTempPath();
        var configRoot = Path.Combine(home, "cfg");
        var env = new FakeCliEnvironment(home, new Dictionary<string, string> { ["JD2_CONFIG"] = configRoot });
        await CliTestHarness.WriteConfigAsync(Path.Combine(configRoot, "config.json"), CreateConfiguredProfile("main", withAccount: true));

        var result = await CliTestHarness.RunAsync(env, ["downloads", "status", "--json"]);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("\"ok\": false", result.StdOut);
        Assert.Contains("\"kind\": \"not_authenticated\"", result.StdOut);
    }

    private static JDownloader.Cli.Config.Jd2Config CreateConfiguredProfile(string name, bool withAccount)
    {
        return new()
        {
            DefaultProfile = name,
            Profiles =
            {
                [name] = new JDownloader.Cli.Config.ProfileRecord
                {
                    AccountEmail = withAccount ? "user@example.com" : null,
                    DefaultDeviceId = "dev-1",
                    DefaultDeviceName = "Box",
                    KnownDevices = [new() { Id = "dev-1", Name = "Box", SeenAtUtc = DateTimeOffset.UtcNow }],
                },
            },
        };
    }

    private static string CreateTempPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "jd2-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
