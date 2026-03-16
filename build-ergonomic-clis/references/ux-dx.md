# CLI UX and DX

## Purpose

Use this guide first. It defines the product shape of the CLI before framework-specific implementation details.

## Core Principles

- Design for tasks, not backend controllers or raw endpoints.
- Make the common path obvious from `--help`.
- Prefer explicitness over convenience when convenience hides state or target selection.
- Treat automation as a first-class use case. A good CLI must work in terminals, scripts, CI, and pipes.
- Keep auth, config, and target resolution understandable from the command line alone.

## Code Organization

- Aim for code files below 300 LOC.
- Treat 500 LOC as a hard limit.
- Do not split files with `partial` or equivalent mechanisms just to get under a line-count target. Prefer composition and smaller collaborating types.
- Avoid generic `Utils`, `Helpers`, or pure utility buckets when a feature-local service, formatter, parser, or value object would express the role more clearly.
- Move shared code upward only when multiple features actually need the same abstraction.

## Command Tree

- Prefer branches over flat command names.
  - Good: `tool auth login`, `tool auth profiles use`, `tool items images list`
  - Avoid: `tool auth-login`, `tool profile-use`, `tool items-images-list`
- Group by user-facing domain, not by internal API tags.
  - `jf` is a good model: `auth`, `items`, `playlists`, `sessions`, `server`.
- Keep low-level escape hatches separate.
  - Use a branch like `raw`, `api`, or `request` for unsupported endpoints.
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
- `--no-color`: disable ANSI formatting
- `--quiet`: suppress human-oriented banners and prompts
- `auth`: the branch for authentication, identity, sessions, tokens, and profiles

Do not overload a familiar flag with an unrelated meaning.

## Auth Design

Keep authentication explicit and contained.

- Put auth under `auth`.
  - Common commands: `login`, `logout`, `status`, `show`, `whoami`, `set-token`, `test`
- Prefer explicit auth modes over hidden fallback behavior.
  - Good: `tool auth login --device`
  - Avoid: running `tool deploy` and silently opening a login prompt
- Fail fast on missing auth for protected commands and print the exact recovery command.
  - Example: `Authentication required. Run 'tool auth login'.`
- Separate user auth from service auth when they differ.
  - Example: UI session cookie vs API token
- If the tool supports secrets from stdin, make that opt-in.
  - `--stdin`
  - `--password-stdin`

For browser-capable service CLIs:

- Prefer system browser plus PKCE or a service-native device or quick-connect flow.
- Fall back to pasted tokens only when the service actually uses them.

For API-token-based CLIs:

- Support `auth set-token TOKEN`
- Also support `auth set-token --stdin`
- Validate tokens by default unless the user passed `--no-validate`

## Self-Hosted Services: Host, Profiles, and Fallbacks

Self-hosted service CLIs need an explicit target model.

Recommended model:

- A profile stores non-secret defaults plus host binding.
- Credentials are bound to the canonical host key, not just a profile name.
- The active profile is only one input to target resolution, not magic global state.

Useful commands:

```text
tool auth profiles list
tool auth profiles use <name>
tool auth hosts list
tool auth hosts set-default <host> <profile>
tool config path
tool config get
tool config set
```

Resolution rules should be documented and enforced:

1. CLI flags
2. Environment variables
3. Config file or selected profile
4. Hardcoded defaults

If `--host` is set without `--profile`, pick a profile using a host-default mapping or a single matching profile. If multiple profiles match, require the user to choose instead of guessing.

If repo or directory inference exists, document it as a lower-priority fallback, not the primary contract.

Good examples from the local references:

- `ztnet` binds host defaults to profiles and only reuses stored auth when the profile host matches the target host.
- `fj-ex` can infer host and repo from git remotes or `FJ_FALLBACK_HOST`, but that behavior should stay visible in docs and errors.

## Environment Variables

Environment variables should be a convenience layer, not hidden behavior.

Rules:

- Map env vars directly to existing flags whenever possible.
- Keep naming predictable.
  - `TOOL_HOST`
  - `TOOL_TOKEN`
  - `TOOL_PROFILE`
  - `TOOL_OUTPUT`
- Document precedence and exact fallback behavior.
- Avoid having multiple env vars for the same setting unless compatibility requires it.
- If compatibility aliases exist, say which one is preferred.

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

The local cautionary example is the "latest run" style workflow in `fj-ex`. The convenience flag is fine. Implicitly treating a missing run identifier as "latest" is not.

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

## Design Checklist

Before implementation, pin down:

- Top-level command tree
- Reserved global flags
- Output modes
- Auth commands and auth failure behavior
- Config file location and format
- Profile and host resolution
- Environment variables and precedence
- Confirmation, `--dry-run`, and the narrow role of `--yes`
- Exit codes
- Three to five copy-paste help examples

Before shipping, validate:

- `tool --help`
- one auth help page
- one destructive command in normal mode
- one destructive command in `--quiet` mode
- one non-interactive secret flow via stdin
- one JSON output example
