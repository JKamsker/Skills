using System.Text;

namespace JDownloader.Cli.Runtime;

public static class SecretInput
{
    public static async Task<string> ReadSecretAsync(
        string? explicitValue,
        bool useStdin,
        bool requireStdinInNonInteractiveMode,
        bool jsonMode,
        bool quietMode,
        string usageMessage,
        string stdinRecoveryMessage,
        string prompt,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(explicitValue) && useStdin)
            throw CliException.Usage(usageMessage);

        if ((jsonMode || quietMode) && !useStdin && string.IsNullOrWhiteSpace(explicitValue) && requireStdinInNonInteractiveMode)
            throw CliException.Usage(usageMessage, stdinRecoveryMessage);

        string secret;
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            secret = explicitValue;
        }
        else if (useStdin)
        {
            secret = await Console.In.ReadToEndAsync(cancellationToken);
        }
        else
        {
            secret = ReadInteractively(prompt);
        }

        secret = secret.TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(secret))
            throw CliException.Usage("Password input was empty.");

        return secret;
    }

    private static string ReadInteractively(string prompt)
    {
        if (Console.IsInputRedirected || Console.IsErrorRedirected)
            throw CliException.Usage("Interactive password entry is unavailable in non-interactive mode.", "Use --password-stdin.");

        Console.Error.Write(prompt);
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
