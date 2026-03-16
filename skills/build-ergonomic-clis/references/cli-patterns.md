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
- `--help` prints to stdout and exits `0`.
- `--version` prints to stdout and exits `0`.

## Reserved Surface Area

Reserve consistent flags and branches for consistent jobs.

- `-h`, `--help`: help
- `-V`, `--version` or `-v` only if your framework already owns it consistently
- `--dry-run`: preview a mutating operation without side effects; prefer this as the primary safety flag
- `-y`, `--yes`: skip a confirmation prompt when the command already has an explicit, predictable effect; for destructive commands this is also the explicit consent required in non-interactive contexts
- `--json` or `--output json`: stable machine output (selects the default stable version, currently v1)
- `--output human` (or `--no-json`): force human output (behavioral mode) and override env/config/profile output defaults (prompts/browser flows are allowed only with a TTY; still suppressed by `--quiet`)
- `--verbose` or `-v`: increase output detail; repeat for more (`-vv`, `-vvv`) if the tool has multiple verbosity tiers
- `--no-color`: disable ANSI formatting; also respect the `NO_COLOR` environment variable (see https://no-color.org/)
- `--quiet`: suppress human-oriented banners, status chatter, and prompts
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

## TTY and Non-Interactive Rules

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

- When the **resolved output mode** is a machine output mode (selected via `--json`, `--output json`, porcelain selectors, or env/config/profile defaults), the CLI is non-interactive: no prompts, no browser launches, no banners.
- If a prompt is required and the tool is in `--quiet` mode, fail (exit `2`) and tell the user to pass the missing flag, use `--dry-run` to inspect first, or use `--yes` only when bypassing a destructive confirmation is intentional.
- If a command supports interactive prompts, only do so when stdin and stderr are TTYs. If a prompt would be required but a TTY is not available, refuse (exit `2`) with an actionable message.
- Prompt on stderr, not stdout.
- Secret prompts must not echo.

## No Surprises

Avoid behavior that makes the CLI feel clever but unpredictable.

- Do not implicitly fetch "latest" or "current" unless the user passed `--latest`, `--current`, or a similar explicit flag.
- Do not let a missing positional argument silently change the meaning of the command.
- Do not perform hidden retries that cross auth or target boundaries.
- Do not mutate config just because a command succeeded once unless the user opted in.

The local cautionary example is the "latest run" style workflow where a convenience flag exists. The convenience flag is fine. Implicitly treating a missing run identifier as "latest" is not.

## Automation Contract

Treat automation as a first-class use case. A good CLI must have an explicit machine-facing contract.

### Machine output styles

Support one (or both) of these styles, but the design must say which applies:

- **Envelope contract** (recommended default for administrative/service/stateful CLIs)
  - A stable wrapper with `ok` + `data` + `error` + `meta`.
  - Good when recovery guidance and uniform parsing matter.
- **Direct-value contract** (allowed for filter/pipeline/data-transform commands)
  - Bare object/array/scalar, JSONL streams, or value-only output.
  - Good when the command behaves like a filter and wrappers add friction.

The design must also state whether the choice is:

- global (the whole CLI uses one contract style), or
- command-specific (some commands are envelope while others are direct-value).

If contract style is command-specific, each affected command must document its style and its failure representation.

### Contract versioning (mandatory)

Machine contracts must be versioned:

- In-band: `meta.schemaVersion = 1`
- Selector: `--porcelain=v1`, `--format-version 1`, `--output json-v1`

If the machine output is a **direct-value contract** (arrays/scalars/JSONL), versioning usually cannot be carried in-band. Prefer an explicit selector:

- `--porcelain=v1` (recommended for commands that need structured errors)
- `--format-version 1` or `--output json-v1` (recommended for value-only JSON output)

If the CLI also provides a convenience flag like `--json`, define it precisely (e.g. "`--json` selects the default stable machine contract version, currently v1") so scripts can rely on it.

If the user also passes an explicit version selector (`--porcelain=v1`, `--format-version 1`, `--output json-v1`), the explicit selector wins.

Human output can evolve more freely. Machine output must have an explicit stability boundary.

### Failure representation

- **Envelope contract**: represent expected failures as a JSON envelope on stdout (`ok: false`) with a stable `error.kind` and actionable recovery guidance when possible.
- **Direct-value contract**: keep stdout value-only; represent errors on stderr and via exit codes. If you need structured errors in direct-value mode, use a separate opt-in selector (porcelain vN) instead of silently changing the contract.

Exit codes still matter for automation:

- `ok: true` responses MUST exit `0`.
- `ok: false` responses MUST exit non-zero (use the exit-code taxonomy below).

For direct-value contracts that do not have an `ok` field:

- Success MUST exit `0`.
- Failures MUST exit non-zero (and keep stdout value-only).

### Schema evolution expectations

- Prefer additive changes (new optional fields) within a schema version.
- Do not repurpose existing fields with incompatible meanings.
- Breaking changes require a new schema version or an explicit opt-in selector.

### Stdout vs stderr routing

The design must state its routing rule and keep it consistent.

Recommended defaults:

- **Envelope-style commands**: success and expected failures are emitted as an envelope on stdout; stderr is reserved for cases where you cannot emit the machine contract (early bootstrap failures, malformed output selection flags, etc.).
- **Direct-value/pipeline commands**: value output on stdout; errors on stderr.

Regardless of style:

- Do not print banners, prompts, or **human-formatted warning lines** to stdout when machine mode is enabled.
- Redact secrets in all output (including errors and diagnostics).
- In machine modes **with metadata** (envelope/porcelain), warnings MUST be represented inside the machine contract (`meta.warnings`, porcelain fields, etc.), not as ad hoc stderr chatter.

## Output and Exit Codes

Treat human and machine output as separate contracts.

- Default to readable human output.
- Provide stable machine output with `--json` or `--output json`.
- Keep machine output on stdout.
- Prompt on stderr, not stdout.
- In human modes, send warnings and banners to stderr.
- In machine modes:
  - For envelope-style contracts, avoid ad hoc stderr noise (banners/progress/warnings); prefer structured warnings/diagnostic paths inside machine metadata.
  - For direct-value/pipeline contracts, stderr remains the channel for warnings and errors; keep stdout value-only.
- Redact secrets in human output and config dumps.
- Use explicit exit codes for common failure classes.

Machine mode is determined by the **resolved output mode** (including env/config/profile defaults), not just by whether a user typed `--json`.

Output resolution precedence (recommended):

- Output flags/selectors (`--output …`, `--json`, `--porcelain=…`) win over env/config/profile defaults.
- Env vars win over config/profile defaults.
- If multiple output selectors conflict (e.g. `--output human` plus `--json`), refuse (exit `2`) with an actionable message.

### Base exit codes (all CLI classes)

- `0`: success
- `1`: general or unclassified error (catch-all for failures that do not fit a specific category)
- `2`: usage / validation / interaction-required / non-interactive refusal
- `5`: not found
- `6`: conflict / precondition failed
- `10`: explicit user cancellation / abort / SIGINT-equivalent

### Service extension exit codes (service-like CLIs only)

- `3`: not authenticated
- `4`: authenticated but not authorized
- `7`: rate limited / backpressure / quota refusal
- `8`: transport / connection / TLS / timeout / protocol-unreachable failure

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
| (none) | Human mode: prompt for confirmation if the command is destructive (only when stdin and stderr are TTYs); otherwise refuse (exit `2`). Machine output modes: never prompt; refuse (exit `2`) unless `--yes` or `--dry-run` is present. |
| `--dry-run` | Print a preview of the operation and exit. Never prompt, never mutate. The preview respects the resolved output mode (human-readable vs machine contract). |
| `--yes` | Skip the confirmation prompt and execute. Never prompt; in machine modes stdout still carries the machine contract. |
| `--dry-run --yes` | `--dry-run` wins. Print the preview and exit without mutating. |
| `--quiet` | If confirmation would be required, fail (exit `2`) with an error telling the user to pass `--yes` or `--dry-run`. Never prompt. |
| `--quiet --yes` | Skip the confirmation prompt and execute. In human mode, this may be silent on success; in machine mode, stdout still carries the machine contract. |
| `--quiet --dry-run` | Print the preview and exit. No prompts, no mutation. |

When a prompt is shown and the user declines, treat it as explicit cancellation (exit `10`).

## Quiet Mode (`--quiet`)

Define `--quiet` precisely:

- Suppresses non-essential human-facing output (status chatter, banners, progress).
- Suppresses prompts (never blocks waiting for interaction).
- Never suppresses machine stdout.
- Never turns a failure into a success. If the command would prompt, it must refuse (exit `2`) unless the user passed `--yes` or `--dry-run`.
- Does not suppress primary command output (tables/value output) unless the command explicitly documents that it is safe to do so.
- In human output modes, suppresses warnings unless they affect correctness (for example: partial results, ambiguous target selection, or a fallback that changes which resource is acted on).
- In machine output modes, do not change the machine contract payload; warnings remain represented where the contract expects them (for example: envelope/porcelain metadata such as `meta.warnings`; for direct-value/pipeline commands without metadata, use stderr while keeping stdout value-only).
- Suppresses "diagnostic log saved to ..." hints; the log file may still be written.
- May still write diagnostic artifacts according to policy.

## Dry-Run Semantics (`--dry-run`)

Define `--dry-run` as a guarantee:

- Never mutates state.
- Explains what would happen (the plan), including the resolved target and key parameters.
- Never prompts.
- The preview respects the resolved output mode (human-readable vs machine contract).
- Local reads are allowed.
- Remote reads are allowed only if the command explicitly documents live planning/validation.
- Never silently performs side effects.

## Binary and Stream Output

For commands that emit files or binary payloads (images, archives, logs, downloads), define one of these patterns:

- Reject machine output with a clear validation error (exit `2`), or
- Provide metadata-only machine output while writing bytes to a file/path (or to stdout only when explicitly requested and safe).

For stream/follow/watch commands, document:

- whether the output is line-oriented (JSONL) or human-oriented
- how `--json`/porcelain interacts with streaming
- whether progress indicators are disabled automatically in non-TTY contexts

## Local Project Discovery

Local-only and hybrid CLIs often need project/workspace discovery. If the CLI walks parent directories, make the rules explicit:

- Stop conditions (filesystem root, marker file, VCS root, explicit `--root`).
- Explicit override flags (`--file`, `--manifest`, `--project`, `--cwd`).
- Which resolved path is used, and where it is surfaced (diagnostics, `--dry-run`, `--verbose`).
- Child-process IO passthrough rules for wrappers around build tools (stdin/stdout/stderr).

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

For protocol/service-specific error handling rules (auth recovery, transport errors, server failures), see [service-cli-patterns.md](service-cli-patterns.md).

### Diagnostic logging

Not every detail belongs on stderr. Capture the full context and internal state in a log file so the user can attach it to a bug report.

Recommended approach:

- On every error, write a timestamped diagnostic file containing:
  - The full command line (with secrets redacted).
  - Resolved config sources (flag, env, config file).
  - The full exception or error chain.
- Store diagnostic files in a dedicated logs directory inside the CLI config directory (e.g., `~/.config/tool/logs/` or `%APPDATA%\tool\logs\`).
- Name files with a timestamp so they do not collide: `tool-error-20260316-141523-042.log`.
- In human output modes, print a hint to stderr when a diagnostic file is written: `Diagnostic log saved to ~/.config/tool/logs/tool-error-20260316-141523-042.log`
- In machine output modes, avoid extra stderr noise; include the diagnostic path in machine output metadata (envelope `meta`, porcelain fields, etc.).
- Suggest including the log when reporting issues: `Include this file when reporting a bug.`

For protocol/service-specific diagnostic logging (exchange capture, auth header redaction), see [service-cli-patterns.md](service-cli-patterns.md).

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
- `--quiet` suppresses all verbose stderr chatter. It does not suppress diagnostic file writes.
- The diagnostic log file always captures `-vvv`-level detail regardless of the verbosity flag.
- If the CLI supports retries, log each attempt with its status so the user can see what happened before the final failure.

## Distilled Patterns from Local Repos

The bundled reference repos converge on a small set of patterns worth teaching directly.

- Build one visible command tree in one place, and group by user task rather than transport or backend tags.
  - [../assets/examples/csharp/spectre/command-tree/Program.cs](../assets/examples/csharp/spectre/command-tree/Program.cs) shows the same "auth / projects / server" branch style without repo-specific surface area.
- Treat machine mode as a first-class contract instead of a formatter toggle.
  - The right shape is stable JSON, structured warnings in machine metadata, prompts (human mode) on stderr, `--dry-run`, `--yes`, `--quiet`, and TTY-aware table headers. See [../assets/examples/rust/clap/run_mode.rs](../assets/examples/rust/clap/run_mode.rs).
- Pair user-facing recovery instructions with saved diagnostics.
  - The distilled runtime pieces are [../assets/examples/csharp/spectre/runtime/ApiCommand.cs](../assets/examples/csharp/spectre/runtime/ApiCommand.cs) and [../assets/examples/csharp/spectre/runtime/DiagnosticLogger.cs](../assets/examples/csharp/spectre/runtime/DiagnosticLogger.cs).

For service-CLI-specific patterns (target/profile/auth resolution, chosen target identity keys), see [service-cli-patterns.md](service-cli-patterns.md).

## Local Cautions

The same reference repos also show where ergonomic CLIs go wrong:

- Do not print warnings to stdout when the command also supports JSON, piping, or raw output. In machine modes, prefer structured warnings in machine metadata (envelope `meta` / porcelain fields); for direct-value/pipeline commands without metadata, use stderr.
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
