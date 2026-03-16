# CLI Patterns

## Purpose

Generic CLI design patterns for any command-line tool. Use this guide first — it defines the product shape of the CLI before framework-specific implementation details.

## Core Principles

- Design for tasks, not backend controllers or raw endpoints.
- Make the common path obvious from `--help`.
- Prefer explicitness over convenience when convenience hides state or resolution.
- Treat automation as a first-class use case. A good CLI must work in terminals, scripts, CI, and pipes.
- Keep config and resolution understandable from the command line alone.

## Code Organization

- Aim for code files below 300 LOC.
- Treat 500 LOC as a hard limit.
- Do not split files with `partial` or equivalent mechanisms just to get under a line-count target. Prefer composition and smaller collaborating types.
- Avoid generic `Utils`, `Helpers`, or pure utility buckets when a feature-local service, formatter, parser, or value object would express the role more clearly.
- Move shared code upward only when multiple features actually need the same abstraction.

## Command Tree

- Prefer branches over flat command names.
  - Good: `tool auth login`, `tool items images list`
  - Avoid: `tool auth-login`, `tool items-images-list`
- Group by user-facing domain, not by internal structure.
  - Service CLI: `auth`, `items`, `playlists`, `sessions`, `server`
  - Build tool: `build`, `test`, `lint`, `publish`, `config`
  - File manager: `files`, `shares`, `trash`, `sync`, `settings`
- Keep low-level escape hatches separate.
  - Use a branch like `raw`, `api`, or `request` for unsupported operations.
- Use a small, repeated verb set at the leaves.
  - `list`, `get`, `create`, `update`, `delete`, `add`, `remove`, `set`, `clear`, `test`, `show`

## Help Contract

Every important help page should answer:

1. What is this branch for?
2. What do I type first?
3. Which commands are destructive or privileged?

Recommended help shape:

```text
NAME
USAGE
DESCRIPTION
COMMON TASKS
COMMANDS
OPTIONS
EXAMPLES
SEE ALSO
```

Rules:

- Put the most common commands first, not alphabetical order.
- Mark privileged or dangerous commands directly in help.
- Keep examples realistic and copy-pasteable.
- If a command branch is mostly for experts, keep it discoverable but visually secondary.

## Reserved Surface Area

Reserve consistent flags and branches for consistent jobs.

- `-h`, `--help`: help
- `-V`, `--version` or `-v` only if your framework already owns it consistently
- `--dry-run`: preview a mutating operation without side effects; prefer this as the primary safety flag
- `-f`, `--force`: bypass safety checks or conflict prompts when that meaning exists in the tool
- `-y`, `--yes`: skip a confirmation prompt when the command already has an explicit, predictable effect
- `--json` or `--output json`: stable machine output
- `--verbose` or `-v`: increase output detail; repeat for more (`-vv`, `-vvv`) if the tool has multiple verbosity tiers
- `--no-color`: disable ANSI formatting; also respect the `NO_COLOR` environment variable (see https://no-color.org/)
- `--quiet`: suppress human-oriented banners and prompts
- `auth`: the branch for authentication, identity, sessions, tokens, and profiles

Do not overload a familiar flag with an unrelated meaning.

## Environment Variables

Environment variables should be a convenience layer, not hidden behavior.

Rules:

- Map env vars directly to existing flags whenever possible.
- Keep naming predictable.
  - `TOOL_CONFIG`
  - `TOOL_OUTPUT`
  - `TOOL_VERBOSE`
- Document precedence and exact fallback behavior.
- Avoid having multiple env vars for the same setting unless compatibility requires it.
- If compatibility aliases exist, say which one is preferred.

For service CLIs that need host, token, or profile env vars, see [service-cli-patterns.md](service-cli-patterns.md).

## Stdin and Interactivity

Default rule: do not block stdin unless the user explicitly asked for it or the command is clearly interactive.

Good patterns:

- `auth set-token --stdin`
- `auth login --password-stdin`
- prompting only inside `auth login` or destructive commands

Bad patterns:

- reading stdin on normal commands just because no argument was provided
- triggering an auth prompt in the middle of a pipeline
- prompting in `--quiet` mode

Rules:

- If a prompt is required and the tool is in `--quiet` mode, fail and tell the user to pass the missing flag, use `--dry-run` to inspect first, or use `--yes` only when bypassing a destructive confirmation is intentional.
- If a command supports interactive prompts, only do so when stdin and stderr are TTYs.
- Prompt on stderr, not stdout.
- Secret prompts must not echo.

## No Surprises

Avoid behavior that makes the CLI feel clever but unpredictable.

- Do not implicitly fetch "latest" or "current" unless the user passed `--latest`, `--current`, or a similar explicit flag.
- Do not let a missing positional argument silently change the meaning of the command.
- Do not perform hidden retries that cross auth or target boundaries.
- Do not mutate config just because a command succeeded once unless the user opted in.

The local cautionary example is the "latest run" style workflow where a convenience flag exists. The convenience flag is fine. Implicitly treating a missing run identifier as "latest" is not.

## Output and Exit Codes

Treat human and machine output as separate contracts.

- Default to readable human output.
- Provide stable machine output with `--json` or `--output json`.
- Keep machine output on stdout.
- Send warnings, banners, and prompts to stderr.
- Redact secrets in human output and config dumps.
- Use explicit exit codes for common failure classes.

Suggested exit code set:

- `0`: success
- `1`: general or unclassified error (catch-all for failures that do not fit a specific category)
- `2`: usage or validation error
- `3`: not authenticated
- `4`: authorization failed
- `5`: not found
- `6`: conflict
- `7`: rate limited
- `8`: network or timeout
- `10`: cancelled

## Confirmation and Dangerous Operations

- Prefer `--dry-run` over `--yes` as the main safety affordance for mutating commands.
- Use `--yes` only to bypass a confirmation prompt for an already explicit action.
- If a destructive command supports `--yes`, it should usually also support `--dry-run`.
- Show `--dry-run` in help and examples before showing the real mutating command.
- In non-interactive contexts, fail instead of prompting.
- Make destructive behavior visible in help and examples.

Flag interaction rules:

| Flags passed | Behavior |
|---|---|
| (none) | Prompt for confirmation if the command is destructive. |
| `--dry-run` | Print a preview of the operation and exit. Never prompt, never mutate. |
| `--yes` | Skip the confirmation prompt and execute. |
| `--dry-run --yes` | `--dry-run` wins. Print the preview and exit without mutating. |
| `--quiet` | If confirmation would be required, fail with an error telling the user to pass `--yes` or `--dry-run`. Never prompt. |
| `--quiet --yes` | Skip the confirmation prompt and execute silently. |
| `--quiet --dry-run` | Print the preview and exit. No prompts, no mutation. |

## Error Messages and Diagnostics

A CLI will encounter unexpected failures. The user needs two things: a clear message explaining what went wrong, and enough diagnostic detail to report or debug the issue.

### User-facing error messages

Every error the user sees should answer three questions:

1. **What failed?** Name the operation: "Failed to compile module 'parser'."
2. **Why?** Include the relevant detail: "File not found: src/parser/grammar.rs"
3. **What now?** Print the recovery step: "Check the path or run 'tool init' to regenerate missing files."

Do not dump raw stack traces, JSON blobs, or internal exception types in default output. Those belong in diagnostic logs.

Rules:

- Lead with the human-readable summary, not the technical detail.
- Include relevant identifiers so the user knows which resource was involved.
- For file-not-found errors, echo back what was looked up so the user can spot typos.
- For permission errors, tell the user which permission is needed and how to grant it.
- Redact secrets (tokens, passwords) in all error output.

For HTTP-specific error handling rules (401/403/500 recovery, network errors), see [service-cli-patterns.md](service-cli-patterns.md).

### Diagnostic logging

Not every detail belongs on stderr. Capture the full context and internal state in a log file so the user can attach it to a bug report.

Recommended approach:

- On every error, write a timestamped diagnostic file containing:
  - The full command line (with secrets redacted).
  - Resolved config sources (flag, env, config file).
  - The full exception or error chain.
- Store diagnostic files in a dedicated logs directory inside the CLI config directory (e.g., `~/.config/tool/logs/` or `%APPDATA%\tool\logs\`).
- Name files with a timestamp so they do not collide: `tool-error-20260316-141523-042.log`.
- Print a hint to stderr when a diagnostic file is written: `Diagnostic log saved to ~/.config/tool/logs/tool-error-20260316-141523-042.log`
- Suggest including the log when reporting issues: `Include this file when reporting a bug.`

For HTTP-specific diagnostic logging (request/response capture, auth header redaction), see [service-cli-patterns.md](service-cli-patterns.md).

### Verbosity levels

Use `--verbose` to surface diagnostic detail on stderr without requiring the user to find the log file. The log file should always be detailed regardless of verbosity.

| Level | What the user sees on stderr |
|---|---|
| Default | Error summary only: what failed, why, what now. |
| `--verbose` (`-v`) | Above plus: resolved config sources, key parameters. |
| `-vv` | Above plus: detailed internal state, intermediate results. |
| `-vvv` | Above plus: full input/output data, timing, retry attempts. |

Rules:

- `--verbose` output goes to stderr, never stdout. Stdout stays clean for data and `--json`.
- `--quiet` suppresses all verbose output. It does not suppress diagnostic file writes.
- The diagnostic log file always captures `-vvv`-level detail regardless of the verbosity flag.
- If the CLI supports retries, log each attempt with its status so the user can see what happened before the final failure.

## Distilled Patterns from Local Repos

The bundled reference repos converge on a small set of patterns worth teaching directly.

- Build one visible command tree in one place, and group by user task rather than transport or backend tags.
  - [../assets/examples/csharp/spectre/command-tree/Program.cs](../assets/examples/csharp/spectre/command-tree/Program.cs) shows the same "auth / projects / server" branch style without repo-specific surface area.
- Treat machine mode as a first-class contract instead of a formatter toggle.
  - The right shape is stable JSON, stderr for warnings, `--dry-run`, `--yes`, `--quiet`, and TTY-aware table headers. See [../assets/examples/rust/clap/run_mode.rs](../assets/examples/rust/clap/run_mode.rs).
- Pair user-facing recovery instructions with saved diagnostics.
  - The distilled runtime pieces are [../assets/examples/csharp/spectre/runtime/ApiCommand.cs](../assets/examples/csharp/spectre/runtime/ApiCommand.cs) and [../assets/examples/csharp/spectre/runtime/DiagnosticLogger.cs](../assets/examples/csharp/spectre/runtime/DiagnosticLogger.cs).

For service-CLI-specific patterns (target/profile/auth resolution, canonical host keys), see [service-cli-patterns.md](service-cli-patterns.md).

## Local Cautions

The same reference repos also show where ergonomic CLIs go wrong:

- Do not print warnings to stdout when the command also supports JSON, piping, or raw output. Use stderr.
- Do not emulate fake subcommands with positional parsing or expose global flags that are not actually wired up.

For service-CLI-specific cautions (plaintext secrets, auth logging, silent profile picking), see [service-cli-patterns.md](service-cli-patterns.md).

## Design Checklist

Before implementation, pin down:

- Top-level command tree
- Reserved global flags
- Output modes
- Config file location and format
- Environment variables and precedence
- Confirmation, `--dry-run`, and the narrow role of `--yes`
- Exit codes
- Error message format and diagnostic log location
- Three to five copy-paste help examples

Before shipping, validate:

- `tool --help`
- one destructive command in normal mode
- one destructive command in `--quiet` mode
- one non-interactive secret flow via stdin
- one JSON output example

For additional service-CLI checklist items (auth, profiles, host resolution), see [service-cli-patterns.md](service-cli-patterns.md).
