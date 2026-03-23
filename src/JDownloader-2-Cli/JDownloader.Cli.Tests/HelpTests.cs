using JDownloader.Cli.Tests.Support;

namespace JDownloader.Cli.Tests;

public sealed class HelpTests
{
    [Fact]
    public async Task HelpPagesExposeKeyBranches()
    {
        var root = CreateTempPath();
        var env = new FakeCliEnvironment(root);

        var rootHelp = await CliTestHarness.RunAsync(env, ["--help"]);
        var downloadsHelp = await CliTestHarness.RunAsync(env, ["downloads", "--help"]);
        var rawHelp = await CliTestHarness.RunAsync(env, ["advanced", "raw", "request", "--help"]);
        var rootText = rootHelp.StdOut + rootHelp.StdErr;
        var downloadsText = downloadsHelp.StdOut + downloadsHelp.StdErr;
        var rawText = rawHelp.StdOut + rawHelp.StdErr;

        Assert.Equal(0, rootHelp.ExitCode);
        Assert.Contains("auth", rootText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("downloads", rootText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("doctor", rootText, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(0, downloadsHelp.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(downloadsHelp.StdErr));

        Assert.Equal(0, rawHelp.ExitCode);
        Assert.True(string.IsNullOrWhiteSpace(rawHelp.StdErr));
    }

    private static string CreateTempPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "jd2-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
