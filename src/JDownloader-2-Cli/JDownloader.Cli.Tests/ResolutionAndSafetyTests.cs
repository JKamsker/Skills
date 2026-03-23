using JDownloader.Cli.Config;
using JDownloader.Cli.Tests.Support;

namespace JDownloader.Cli.Tests;

public sealed class ResolutionAndSafetyTests
{
    [Fact]
    public async Task ResolutionUsesFlagsThenEnvThenConfigAndAmbiguousNamesFail()
    {
        var home = CreateTempPath();
        var configRoot = Path.Combine(home, "cfg");
        var env = new FakeCliEnvironment(home, new Dictionary<string, string>
        {
            ["JD2_CONFIG"] = configRoot,
            ["JD2_PROFILE"] = "env",
        });

        await CliTestHarness.WriteConfigAsync(Path.Combine(configRoot, "config.json"), new Jd2Config
        {
            DefaultProfile = "cfg",
            Profiles =
            {
                ["cfg"] = new ProfileRecord
                {
                    DefaultDeviceId = "cfg-1",
                    DefaultDeviceName = "CfgBox",
                    KnownDevices = [new() { Id = "cfg-1", Name = "CfgBox", SeenAtUtc = DateTimeOffset.UtcNow }],
                },
                ["env"] = new ProfileRecord
                {
                    DefaultDeviceId = "env-1",
                    DefaultDeviceName = "EnvBox",
                    KnownDevices = [new() { Id = "env-1", Name = "EnvBox", SeenAtUtc = DateTimeOffset.UtcNow }],
                },
                ["flag"] = new ProfileRecord
                {
                    DefaultDeviceId = "flag-1",
                    DefaultDeviceName = "FlagBox",
                    KnownDevices =
                    [
                        new() { Id = "flag-1", Name = "FlagBox", SeenAtUtc = DateTimeOffset.UtcNow },
                        new() { Id = "dup-1", Name = "dup", SeenAtUtc = DateTimeOffset.UtcNow },
                        new() { Id = "dup-2", Name = "Dup", SeenAtUtc = DateTimeOffset.UtcNow },
                    ],
                },
            },
        });

        var envResolved = await CliTestHarness.RunAsync(env, ["doctor", "--json"]);
        Assert.Equal(0, envResolved.ExitCode);
        Assert.Contains("\"resolvedProfile\": \"env\"", envResolved.StdOut);

        var flagResolved = await CliTestHarness.RunAsync(env, ["device", "get", "--json", "--profile", "flag"]);
        Assert.Equal(0, flagResolved.ExitCode);
        Assert.Contains("\"profile\": \"flag\"", flagResolved.StdOut);

        var ambiguous = await CliTestHarness.RunAsync(env, ["device", "get", "--json", "--profile", "flag", "--device", "DuP"]);
        Assert.Equal(2, ambiguous.ExitCode);
        Assert.Contains("\"kind\": \"usage\"", ambiguous.StdOut);
    }

    [Fact]
    public async Task DestructiveCommandsRequireYesOrDryRunAndDryRunUsesJsonEnvelope()
    {
        var home = CreateTempPath();
        var configRoot = Path.Combine(home, "cfg");
        var env = new FakeCliEnvironment(home, new Dictionary<string, string> { ["JD2_CONFIG"] = configRoot });
        await CliTestHarness.WriteConfigAsync(Path.Combine(configRoot, "config.json"), new Jd2Config
        {
            DefaultProfile = "main",
            Profiles =
            {
                ["main"] = new ProfileRecord
                {
                    AccountEmail = "user@example.com",
                    DefaultDeviceId = "dev-1",
                    DefaultDeviceName = "Box",
                    KnownDevices = [new() { Id = "dev-1", Name = "Box", SeenAtUtc = DateTimeOffset.UtcNow }],
                },
            },
        });

        var refused = await CliTestHarness.RunAsync(env, ["downloads", "links", "remove", "--quiet", "--json"]);
        Assert.Equal(2, refused.ExitCode);
        Assert.Contains("\"kind\": \"usage\"", refused.StdOut);

        var preview = await CliTestHarness.RunAsync(env, ["downloads", "links", "remove", "--dry-run", "--json"]);
        Assert.Equal(0, preview.ExitCode);
        Assert.Contains("\"ok\": true", preview.StdOut);
        Assert.Contains("\"action\": \"dry-run\"", preview.StdOut);
    }

    private static string CreateTempPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "jd2-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
