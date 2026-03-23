# JDownloader 2 CLI v1 Plan (`jd2`)

## Summary

- Build a cross-platform `.NET 10` CLI in `src/JDownloader-2-Cli` using `Spectre.Console.Cli` and `Microsoft.Extensions.Hosting`.
- Treat this as a service-native CLI with a My.JDownloader account and device model. v1 is My.JDownloader-first and relay-backed for normal execution; there is no public direct or local transport mode in v1.
- Ship broad first-class coverage for the major API domains, but keep the surface task-first. Use modern and v2 endpoint families as canonical and route legacy-only or oddball endpoints through `advanced raw request` unless they expose unique behavior.
- Use a global JSON envelope v1 contract for `--json`: `ok`, `data`, `error`, `meta.schemaVersion`, `meta.warnings`, `meta.diagnosticLogPath`.

## Public Contract

```text
jd2
  auth        login/logout/status/whoami/profiles [list|get|add|rename|remove|use]
  device      list/get/use/ping/direct-info
  downloads   status/speed/start/stop/pause; links [...]; packages [...]; stopmark [...]
  grabber     add/add-container/clear/move-to-downloads; links [...]; packages [...]; jobs [...]; variants [...]
  accounts    list/get/add/update/enable/disable/remove/refresh; hosters [...]; basic-auth [...]
  extraction  queue/info/settings [get|set]/start/cancel/add-password
  settings    config [...]; plugins [...]; extensions [...]
  captcha     list/get/job/solve/skip/forward [...]
  events      publishers/subscribe/set/remove/status/listen/poll
  system      info/storage/reconnect; jd [version|revision|uptime|refresh-plugins|restart|exit]; os [shutdown|hibernate|standby]; update [check|run|restart]; toggle [...]
  advanced    content [...]; dialogs [...]; ui [...]; ingest [...]; raw request
  doctor
```

- Canonical mappings:
  - `downloads` prefers `/downloadsV2`; use `/downloads` or `/downloadcontroller` only where v2 lacks the capability.
  - `grabber` prefers `/linkgrabberv2`; fold `linkcollector` and `flash`-style ingestion into `grabber` or `advanced ingest`.
  - `accounts` prefers `/accountsV2`.
  - `settings` wraps `/config`, `/plugins`, and `/extensions`.
- Do not create dedicated first-class commands for static or browser-test endpoints like `crossdomain.xml`, `favicon.ico`, `flashgot`, `jdcheck.js`, `jdcheckjson`, or novelty and debug endpoints like `jd/sum` and `jd/doSomethingCool`; expose them only through `advanced raw request`.
- Global flags: `--profile`, `--device`, `--json`, `--output human|json`, `--verbose`, `--quiet`, `--dry-run`, `--yes`, `--timeout`, `--no-color`.
- Complex query and body endpoints get ergonomic common flags plus escape hatches:
  - `--fields`, `--limit`, `--offset`, `--link-id`, `--package-id`, `--hoster`, and similar common selectors.
  - `--query-json <json-or-@file>` and `--body-json <json-or-@file>` for full object shapes like `LinkQuery`, `PackageQuery`, `AddLinksQuery`, `AccountQuery`, and plugin or config query bodies.
- Binary-producing commands in `advanced content` require `--output-file <path>` and reject `--json` if no file destination is provided.

## Resolution, Auth, and Output Rules

- Config files:
  - `config.json` stores profile data plus encrypted auth and session blobs.
  - `keyfile.pem` stores sidecar key material used to decrypt the encrypted auth and session blobs in `config.json`.
- Config paths:
  - Windows: `%APPDATA%/jd2/`
  - macOS: `~/Library/Application Support/jd2/`
  - Linux: `${XDG_CONFIG_HOME:-~/.config}/jd2/`
  - Overrides: `JD2_CONFIG`, `JD2_KEYFILE`
- Profile model:
  - Profiles are globally named and store `accountEmail`, `defaultDeviceId`, `defaultDeviceName`, `output`, and `timeoutSeconds`.
  - `defaultProfile` is stored once at the root.
  - Encrypted auth blobs are keyed by normalized lowercased email, not by profile name.
- Credential protection model:
  - Generate `keyfile.pem` automatically on first successful login if it does not exist.
  - Protect stored auth and session blobs with an application-specific KDF that combines the sidecar key-file material with bundled app context material.
  - Treat this as an explicit at-rest protection tradeoff, not as host-bound secret storage.
  - Do not derive keys from hardware identifiers or machine-specific values.
- Auth behavior:
  - `auth login --email <email> [--profile <name>] [--password-stdin]`
  - Human mode may prompt for password only inside `auth login`; machine mode and `--quiet` must use `--password-stdin`.
  - Persist derived `loginSecret` and any reusable session bundle as encrypted blobs in `config.json`; use `keyfile.pem` plus the application KDF context to decrypt them; do not persist the raw password after login completes.
- Resolution order:
  - Profile: `--profile` > `JD2_PROFILE` > `config.defaultProfile` > single-profile inference > error.
  - Device: `--device` > `JD2_DEVICE` > profile default device > single-device inference > error.
  - Output: flags and selectors > `JD2_OUTPUT` > profile default > human.
  - Timeout: flag > `JD2_TIMEOUT` > profile default > built-in default.
- Device matching:
  - Exact device id first, exact name second, case-insensitive exact name third; multiple matches are a usage error.
- Output and errors:
  - Human output goes to stdout; prompts, warnings, and diagnostics go to stderr.
  - `--json` emits the envelope on stdout for success and expected failures.
  - Exit codes: `0` success, `1` unexpected, `2` usage or validation or non-interactive refusal, `3` not authenticated, `4` not authorized, `5` not found, `6` conflict or precondition, `7` rate limited, `8` transport or timeout, `10` explicit cancel.
- Confirmation rules:
  - Require confirmation for destructive or disruptive operations such as remove, cleanup, clear, cancel, JD restart or exit, OS shutdown or hibernate or standby, reconnect, and update restart.
  - `--dry-run` prints the resolved device plus the exact API request plan and never mutates.
  - `--yes` skips confirmation.
  - Machine mode and `--quiet` never prompt.

## Implementation Changes

- Create:
  - `src/JDownloader-2-Cli/JDownloader.Cli/`
  - `src/JDownloader-2-Cli/JDownloader.Cli.Tests/`
  - `src/JDownloader-2-Cli/JDownloader.Cli.sln`
- Core runtime services and interfaces:
  - `IProfileStore`, `IKeyFileProvider`, `ICredentialProtector`, `IProfileResolver`, `IDeviceResolver`
  - `IMyJdAuthService`, `IMyJdTransport`, `IRequestIdProvider`
  - `IOutputRenderer`, `IConfirmationGuard`, `IDiagnosticLogger`
- Shared command bases:
  - `AnonymousCommand<TSettings>` for `auth login`, `doctor`, and profile commands that do not require a resolved device.
  - `DeviceApiCommand<TSettings>` for all protected commands; it resolves profile and device once, builds the JSON envelope, and maps exceptions to exit codes.
- Transport and client layout:
  - One My.JDownloader auth and crypto layer for login, signing, and session reuse.
  - Typed domain clients for downloads, grabber, accounts, captcha, extraction, settings, events, system, and advanced branches.
  - Central request serialization so parameter order and request-id generation are enforced in one place.
- Spectre structure:
  - One visible command tree in `Program.cs`.
  - Feature-first folders under `Commands/`.
  - Thin commands, with protocol logic in services and clients.
- Diagnostics:
  - Save redacted error logs under `logs/` in the CLI config directory.
  - Capture command line, resolved profile and device, request path, redacted payload, response metadata, and the exception chain.

## Test Plan

- Help and routing:
  - `jd2 --help`, `jd2 downloads --help`, and `jd2 advanced raw request --help` show the intended task-first tree and examples.
- Auth and non-interactive behavior:
  - `auth login --json` without `--password-stdin` fails with exit `2`; `auth login --password-stdin` succeeds and writes `config.json` and `keyfile.pem` in the expected locations.
- Resolution:
  - Profile and device precedence follows flags > env > config > single-entry inference, and ambiguous device names fail instead of guessing.
- Safety:
  - `downloads links remove` and `system update restart` refuse in non-interactive mode without `--yes` or `--dry-run`; `--dry-run` emits a preview envelope in JSON mode.
- Machine output:
  - A representative success and a representative domain failure return valid JSON envelope v1 with clean stdout and the correct exit codes.
- Binary and advanced path:
  - `advanced content icon ... --output-file ...` writes bytes to disk and returns metadata only; `advanced raw request` can call an unsupported endpoint with `--query-json`.

## Assumptions

- The executable name is `jd2`.
- v1 uses My.JDownloader relay transport for normal command execution; `device direct-info` is informational only.
- Broad coverage means first-class commands for all major capability families, not one CLI leaf per raw endpoint signature.
- Legacy duplicate families are normalized into the canonical branches; exact legacy behavior remains reachable through `advanced raw request`.
- The sidecar `keyfile.pem` model is an explicit product choice for credential protection and portability; it is not intended to behave like OS-managed secret storage.
