# Implementing a CLI in C#

## Stack

For modern .NET CLIs, prefer this baseline:

- `Spectre.Console.Cli` for command trees and help
- `Microsoft.Extensions.Hosting` for DI, config, and logging
- `AsyncCommand<TSettings>` for I/O-bound commands
- `IHttpClientFactory` for HTTP clients

This matches the strongest pattern from `jf`: Spectre owns the command surface, while the service container owns runtime wiring.

## Canonical Example Assets

Prefer the small examples in this skill before copying repository code directly:

- [../assets/examples/csharp/spectre/command-tree/Program.cs](../assets/examples/csharp/spectre/command-tree/Program.cs): task-first branch registration with descriptions and a default subcommand.
- [../assets/examples/csharp/spectre/runtime/GlobalOptions.cs](../assets/examples/csharp/spectre/runtime/GlobalOptions.cs): shared Spectre global flags for host, profile, output, dry-run, and confirmations.
- [../assets/examples/csharp/spectre/runtime/TargetResolver.cs](../assets/examples/csharp/spectre/runtime/TargetResolver.cs): canonical host keys, profile matching, and flag/env/config/default precedence.
- [../assets/examples/csharp/spectre/runtime/ApiCommand.cs](../assets/examples/csharp/spectre/runtime/ApiCommand.cs): central context resolution, exit-code mapping, and recovery-oriented error reporting.
- [../assets/examples/csharp/spectre/runtime/DangerousActionGuard.cs](../assets/examples/csharp/spectre/runtime/DangerousActionGuard.cs): TTY-aware confirmation, `--dry-run`, and `--yes`.
- [../assets/examples/csharp/spectre/runtime/DiagnosticLogger.cs](../assets/examples/csharp/spectre/runtime/DiagnosticLogger.cs): timestamped diagnostic logs with header redaction.

## Application Shape

Keep the command tree in one bootstrap location so the product surface is visible in one file, but keep the implementation feature-based rather than layer-based.

Recommended shape:

```text
src/MyCli/
  Program.cs
  Common/
    TypeRegistrar.cs
    GlobalSettings.cs
  Commands/
    Auth/
      LoginCommand.cs
      LogoutCommand.cs
      WhoAmICommand.cs
      AuthService.cs
      CredentialStore.cs
    Config/
      ConfigGetCommand.cs
      ConfigSetCommand.cs
      ConfigStore.cs
    Deployments/
      DeploymentsListCommand.cs
      DeploymentsStartCommand.cs
      DeploymentService.cs
      DeploymentClient.cs
    Releases/
      ReleasesListCommand.cs
      ReleasesPromoteCommand.cs
      ReleaseService.cs
```

Reference pattern:

- `Jellyfin-Cli/src/Jellyfin.Cli/Program.cs`
- `Jellyfin-Cli/src/Jellyfin.Cli/Common/GlobalSettings.cs`

Structure rules:

- Organize by feature first.
- Move shared code up one level only until it is in scope for every feature that needs it.
- Do not create broad layer folders for everything up front.
- If code is shared only by `Deployments` and `Releases`, move it to their nearest common scope, not straight into a global `Common` bucket.
- Keep `Common/` small and reserved for true cross-cutting infrastructure such as bootstrap, global settings, or generic helpers.
- Aim for files below 300 LOC.
- Treat 500 LOC as a hard limit.
- Do not use `partial` purely to split a large class across files to satisfy the line-count target. Prefer composition and smaller types.
- Avoid broad `Utility`, `Helper`, or `Extensions` dumping grounds when a feature-local service, formatter, parser, or value object would be clearer.

## Bootstrapping

In `Program.cs`:

1. Build the service collection or generic host.
2. Register stores, services, HTTP clients, and render helpers.
3. Bridge DI into Spectre with `ITypeRegistrar`.
4. Configure the command tree with `AddBranch()` and `AddCommand()`.

Use `SetApplicationName()` and `SetApplicationVersion()` early.

Keep branches task-first:

```csharp
config.AddBranch("auth", auth =>
{
    auth.SetDescription("Authentication and identity");
    auth.AddCommand<LoginCommand>("login");
    auth.AddCommand<WhoAmICommand>("whoami");
});
```

## Settings and Global Options

Create one shared base settings type for common flags.

Typical fields:

```csharp
public class GlobalSettings : CommandSettings
{
    [CommandOption("-H|--host <URL>")]
    public string? Host { get; set; }

    [CommandOption("--profile <NAME>")]
    public string? Profile { get; set; }

    [CommandOption("--json")]
    public bool Json { get; set; }

    [CommandOption("--no-color")]
    public bool NoColor { get; set; }

    [CommandOption("--quiet")]
    public bool Quiet { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    [CommandOption("-y|--yes")]
    public bool Yes { get; set; }
}
```

Rules:

- Put shared flags in the base class.
- Keep command-specific settings small and local to the command.
- Keep one command and its settings in the same file by default.
- Keep one command plus one settings type per file maximum unless the settings become unusually large.
- Split oversized settings into a nearby feature-local file only when the combined file becomes hard to navigate.
- Keep command files comfortably below 300 LOC when possible.
- If a command file approaches 500 LOC, refactor by extracting collaborators, not by slicing the command into `partial` files.
- Use `Validate()` on settings when the framework path is simple enough.
- Prefer repeatable options to comma-separated user input.

## Auth, Config, and Profile Resolution

Do not scatter host and auth logic through commands.

Create dedicated services such as:

- `ITargetResolver`
- `IProfileStore`
- `ICredentialStore`
- `IAuthService`

Recommended split:

- Non-secret config in a config file
- Secrets in a secure store where possible
- Canonical host keys for matching credentials to targets

Guardrails:

- Protected commands should fail with a precise recovery message when auth is missing.
- Do not open a login prompt from unrelated commands.
- Only prompt in explicit auth commands and only when a TTY is present.

If many commands require auth, enforce it centrally with an interceptor or a pre-execution service.

## Output

Use Spectre rich output for humans, but keep automation stable.

- Inject `IAnsiConsole` into commands instead of using static `AnsiConsole`.
- Send structured machine output through a single serializer path.
- Keep `--json` behavior identical across commands.
- Print prompts and warnings on stderr when practical.

If the command can emit either tables or JSON, make that switch early in the command handler so the rest of the logic stays clean.

## Error Handling and Exit Codes

Prefer central exception-to-exit-code mapping.

Use:

- `SetExceptionHandler(...)` in production
- `PropagateExceptions()` only in tests or debug flows

Map domain exceptions rather than raw framework exceptions:

- `NotAuthenticatedException` -> exit code `3`
- `ValidationException` -> exit code `2`
- `NotFoundException` -> exit code `5`

## Confirmation and Interactivity

Wrap confirmation logic in a shared helper.

Rules:

- Prefer `--dry-run` over `--yes` in command docs, examples, and safety flows.
- Return a preview result immediately in `--dry-run`
- Use `--yes` only to bypass an otherwise interactive confirmation
- If a destructive command supports `--yes`, it should usually support `--dry-run` too
- Fail in `--quiet` if confirmation would otherwise be required
- Never read stdin unless the command explicitly supports it

Keep secret input separate from normal prompts. Use a no-echo prompt implementation for passwords and tokens.

## HTTP Client Layer

Use typed clients or a factory abstraction instead of making requests directly in commands.

Recommended behaviors:

- base address from resolved target
- auth header injection in one place
- user agent in one place
- timeout and retry policy in one place
- consistent JSON serialization

Commands should call services such as `UsersClient.ListAsync(...)`, not build URLs inline.

Within a feature:

- Keep the command file thin but self-contained.
- Put the command class and its settings type together in the same file when practical.
- Keep feature-local services and clients near the commands that use them.
- Promote a service or helper upward only when multiple sibling features actually share it.
- Prefer named collaborators such as `DeploymentPlanBuilder`, `ReleaseFormatter`, or `HostNormalizer` over anonymous helper buckets.

## Diagnostics and Logging

Capture the last HTTP exchange so that every error can be reported with full context.

Recommended approach:

- Create a delegating handler that records the last request and response in a scoped context object (e.g., `HttpDiagnosticsContext`).
- On error, write a timestamped log file to the CLI logs directory containing the command line, resolved settings, the full HTTP exchange (method, URL, headers with auth redacted, request body, response status, response headers, response body truncated to 64 KB), and the exception chain.
- Print a one-line hint to stderr: `Diagnostic log saved to %APPDATA%\tool\logs\tool-error-20260316-141523.log`

Implementation sketch:

```csharp
public class HttpDiagnosticsHandler : DelegatingHandler
{
    private readonly HttpDiagnosticsContext _context;

    public HttpDiagnosticsHandler(HttpDiagnosticsContext context)
        => _context = context;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        _context.CaptureRequest(request);
        var response = await base.SendAsync(request, ct);
        await _context.CaptureResponseAsync(response);
        return response;
    }
}
```

Register the handler in the HTTP client pipeline so every request is captured transparently. The `SetExceptionHandler` can then call `HttpDiagnosticsContext.WriteLogFile()` before mapping the exception to an exit code.

Verbosity control:

- Default: error summary only on stderr.
- `--verbose`: add resolved host, profile source, HTTP method + URL + status to stderr.
- Higher verbosity or the log file: full headers and bodies.

Use `IAnsiConsole.WriteException()` only in `--verbose` mode or higher. In default mode, print the human-readable summary and point to the log file.

## Testing

Cover both parsing and behavior.

- Use `Spectre.Console.Testing` for command-app tests.
- Test help text for important branches.
- Test settings validation.
- Test host/profile resolution independently from command handlers.
- Test JSON output for at least one command per branch type.

Minimum test matrix:

- missing auth on a protected command
- destructive command with `--dry-run`
- destructive command with and without `--yes`
- non-interactive secret input path
- branch help output
- one success path with `--json`
- API error produces a diagnostic log file with the HTTP exchange
- `--verbose` surfaces HTTP method, URL, and status on stderr

## Implementation Checklist

- Build the command tree first in `Program.cs`
- Define global settings
- Implement target resolution and config precedence
- Implement auth and credential storage
- Add one branch end to end
- Add shared output, preview, and confirmation helpers
- Add exit code mapping
- Add HTTP diagnostics handler and log file writer
- Add parser and command tests
