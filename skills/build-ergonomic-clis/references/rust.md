# Implementing a CLI in Rust

## Stack

For ergonomic Rust CLIs, prefer this baseline:

- `clap` derive API for parsing and help (enable the `env` feature for `#[arg(env = "...")]` support)
- `serde` and `serde_json` for machine output
- `reqwest` for HTTP (async by default; pair with `tokio` as the async runtime)
- `tokio` with `#[tokio::main]` for async entry point; use `reqwest::blocking` only if the CLI is entirely synchronous
- `thiserror` or `eyre` for error layering
- `clap_complete` if shell completions matter

The local references split into two good patterns:

- `ztnet`: strong config, profile, and host-binding model
- `fj-ex`: strong target inference and auth-stdin options

## Canonical Example Assets

Prefer the small examples in this skill before copying repository code directly:

- [../assets/examples/rust/clap/profile_context.rs](../assets/examples/rust/clap/profile_context.rs): shared host/profile/token resolution with canonical host keys and explicit ambiguity handling.
- [../assets/examples/rust/clap/target_resolution.rs](../assets/examples/rust/clap/target_resolution.rs): layered `--host` / `--repo` / `--remote` / env fallback target inference inspired by `fj-ex`.
- [../assets/examples/rust/clap/run_mode.rs](../assets/examples/rust/clap/run_mode.rs): machine-vs-human output, `--dry-run`, `--yes`, TTY-aware prompts, raw bytes, and exit categories.

## CLI Structure

Model branches with nested `Subcommand` enums and keep global flags on the top-level parser.

Typical layout:

```text
src/
  main.rs
  cli.rs
  cli/
    auth.rs
    config.rs
    domain_a.rs
  app/
    auth.rs
    config.rs
    domain_a.rs
  config.rs
  context.rs
  error.rs
  output.rs
```

Keep parsing types separate from execution logic.

- `cli/*.rs`: clap structs and enums only
- `app/*.rs`: command handlers
- `context.rs`: resolved host, profile, token, output, retries
- Aim for files below 300 LOC and treat 500 LOC as a hard limit.
- If a file gets too large, split by responsibility and composition, not by creating generic helper buckets.

## clap Patterns

Use derive macros consistently:

```rust
#[derive(Parser, Debug)]
pub struct Cli {
    #[command(subcommand)]
    pub command: Command,

    #[arg(short = 'H', long, env = "TOOL_HOST")]
    pub host: Option<String>,

    #[arg(long, env = "TOOL_PROFILE")]
    pub profile: Option<String>,

    #[arg(long)]
    pub json: bool,
}
```

Use clap features deliberately:

- `#[command(subcommand)]` for branches
- `#[command(flatten)]` to share global flags across subcommands (equivalent to the `GlobalSettings` base class in C#)
- `#[arg(env = "...")]` for env vars (requires clap's `env` feature)
- `conflicts_with` for mutually exclusive flags
- `requires` for dependent flags
- `value_enum` for controlled string values
- `value_name` to keep help readable

Good examples from the local refs:

- `ztnet` uses env-backed args plus nested subcommands for `auth profiles` and `auth hosts`.
- `fj-ex` uses explicit `--latest` flags instead of treating missing identifiers as implicit latest.

## Target and Profile Resolution

Keep resolution in one place, not in every command.

Create a context layer that resolves:

- profile
- host
- token or session cookie
- output format
- retries and timeout
- default org, repo, or network scope

Recommended precedence:

1. CLI flags
2. Environment variables
3. Config file or selected profile
4. Hardcoded defaults

For self-hosted service CLIs:

- Store host defaults separately from profile definitions.
- Canonicalize host keys before matching.
- Only reuse stored credentials when the selected profile host matches the target host.

Reference pattern:

- `ztnet-cli/src/context.rs`

## Auth Flows

Auth should live under `auth` and remain explicit.

Recommended commands:

- `auth login`
- `auth logout`
- `auth show` or `auth status`
- `auth set-token`
- `auth test`
- `auth profiles list`
- `auth profiles use`
- `auth hosts set-default`

Rules:

- Support `--stdin` or `--password-stdin` for secrets.
- Do not prompt outside explicit auth commands.
- If `--quiet` is active and the command would prompt, fail with an actionable error.
- If MFA or TOTP is required, make the extra input explicit and documented.

## Interactivity and Stdin

Use stdin only when the user opted in or the command is clearly interactive.

Good patterns:

```text
tool auth set-token --stdin
tool auth login --password-stdin
tool delete thing --dry-run
tool delete thing --yes
```

Implementation tips:

- Prefer `--dry-run` over `--yes` in docs and examples for mutating commands.
- Use `std::io::Read` for full stdin reads when `--stdin` is explicit.
- Use `rpassword` or equivalent for hidden password entry.
- Check terminal state before prompting when possible.
- Print prompts to stderr.

Reference patterns:

- `ztnet-cli/src/app/common.rs`
- `ztnet-cli/src/app/auth.rs`
- `forgejo-cli-ext/src/login.rs`

## Output

Keep one output abstraction for humans and machines.

- Table or rich human output by default
- `--json` or `--output json` for stable machine output
- warnings and prompts on stderr
- secret redaction in config or auth displays

If the tool has a `--dry-run` mode, print the resolved request shape and exit before making network calls.

## Error Handling

Use domain-level errors and map them consistently at the top.

Suggested categories:

- usage or validation
- auth missing
- auth rejected
- target resolution failed
- network or timeout
- not found
- conflict

Do not bury recovery instructions in debug logs. Put the recovery command in the user-facing error.

## HTTP Layer

Keep HTTP and parsing away from clap structs.

Recommended split:

- `target.rs` or `context.rs` resolves host and scope
- `session.rs` handles cookies or auth headers
- `client.rs` or service modules build requests
- command handlers call service functions and format the result

If the CLI infers host or repo from git remotes, keep that in a dedicated resolver and keep its precedence documented.

Reference pattern:

- `forgejo-cli-ext/src/target.rs`

## Diagnostics and Logging

Capture HTTP exchange details so that errors can be reported with full context.

Recommended approach:

- Wrap the HTTP client layer so that every request/response pair is recorded in a context struct before the result propagates to command handlers.
- On error, write a timestamped diagnostic file to the CLI logs directory (e.g., `~/.config/tool/logs/tool-error-{timestamp}.log`) containing:
  - The command line (secrets redacted).
  - Resolved context: host, profile, auth source.
  - HTTP request: method, URL, headers (auth redacted), body.
  - HTTP response: status, headers, body (truncated to 64 KB).
  - The full error chain (`eyre` report or `thiserror` chain).
- Print a one-line hint to stderr: `Diagnostic log saved to ~/.config/tool/logs/tool-error-20260316-141523.log`

Implementation tips:

- Store the last exchange in a `DiagnosticsContext` struct passed through the app. After each HTTP call, update it before checking the response status.
- Use `reqwest::Response::text()` with a byte limit to avoid reading unbounded response bodies into memory.
- If the CLI uses retries, log each attempt (method, URL, status, duration) so the user can see what happened before the final failure.

Verbosity control:

- Default: error summary only on stderr.
- `-v`: add resolved host, profile source, HTTP method + URL + status to stderr.
- `-vv`: add request and response headers (auth redacted), truncated response body.
- `-vvv`: full request body, full response body, timing.
- The diagnostic log file always captures full detail regardless of the verbosity flag.

Use the `--verbose` count from clap (`action = ArgAction::Count`) to gate output levels. All verbose output goes to stderr via `eprintln!` or a logging facade, never to stdout.

## Testing

Test parsing separately from behavior.

- `Cli::try_parse_from(...)` for parser tests
- unit tests for host normalization and precedence resolution
- unit tests for destructive-command confirmation helpers
- integration tests for binary behavior when feasible
- snapshot or golden tests for key help pages if the surface is large

Minimum parser tests:

- mutually exclusive flags
- missing required values
- env-var fallback
- nested subcommand parsing
- `--latest` style convenience remaining opt-in

Minimum behavior tests:

- `--dry-run` preview path
- `--quiet` refusing to prompt
- `--stdin` secret input path
- host/profile mismatch
- JSON output contract
- destructive command with and without `--yes`
- API error produces a diagnostic log file with the HTTP exchange
- `-v` surfaces HTTP method, URL, and status on stderr
