using System.Text.Json;
using JDownloader.Cli.Bootstrap;
using JDownloader.Cli.Config;

namespace JDownloader.Cli.Tests.Support;

internal static class CliTestHarness
{
    public sealed record CommandRunResult(int ExitCode, string StdOut, string StdErr);

    public static async Task<CommandRunResult> RunAsync(FakeCliEnvironment environment, string[] args, string input = "")
    {
        var app = CliApplication.Create(environment);
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var stdin = new StringReader(input);

        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var originalIn = Console.In;

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            Console.SetIn(stdin);
            var exitCode = await app.RunAsync(args);
            return new CommandRunResult(exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
            Console.SetIn(originalIn);
        }
    }

    public static async Task WriteConfigAsync(string configPath, Jd2Config config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        await using var stream = File.Create(configPath);
        await JsonSerializer.SerializeAsync(stream, config, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });
    }

    public static JsonDocument ParseJson(string content) => JsonDocument.Parse(content);
}
