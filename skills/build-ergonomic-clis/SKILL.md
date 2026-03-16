---
name: build-ergonomic-clis
description: Designs, reviews, and implements product-grade command-line interfaces. Covers command tree structure, flag conventions, auth and profile UX, config precedence, machine-readable output, confirmation and dry-run rules, exit codes, and non-interactive behavior. Use when designing a CLI, reviewing CLI UX/DX, planning command structure, adding argument parsing, or implementing a service CLI. Supports C#/.NET Spectre.Console.Cli and Rust clap.
argument-hint: "[design|review|implement] <description>"
---

# Build Ergonomic CLIs

Use this skill to design a CLI as a product surface instead of a thin dump of API endpoints or internal functions.

## When Not to Use This

- Do not use this skill for shell one-liners, grep or awk pipelines, or other ad hoc shell usage.
- Do not use this skill when the task is only about operating an existing third-party CLI.
- Do not use this skill for TUI or terminal-dashboard work, packaging and distribution work, or raw OpenAPI client generation.

## Task Modes

### Design

- First classify the CLI (required):
  - **Local-only**: operates on local files/processes/project state only.
  - **Hybrid**: local-first, but some commands optionally connect to remotes.
  - **Service-native**: primarily interacts with one remote service/API surface.
  - **Multi-surface service**: multiple independent target/auth surfaces (contexts, daemons + registries, certs + accounts, etc.).
- Always start with [references/cli-patterns.md](references/cli-patterns.md) (generic CLI + automation contract).
- Then load service guidance based on the classification:
  - **Local-only**: `cli-patterns.md` only.
  - **Hybrid**: `cli-patterns.md` + only the relevant sections of [references/service-cli-patterns.md](references/service-cli-patterns.md) for the remote-facing branches.
  - **Service-native**: read [references/service-cli-patterns.md](references/service-cli-patterns.md) fully.
  - **Multi-surface service**: read [references/service-cli-patterns.md](references/service-cli-patterns.md) fully and explicitly apply the multiple-auth/context guidance.
- Define the command tree, help contract, env/config precedence, target/auth/context resolution, automation contract, confirmation rules, machine output, and non-interactive behavior before talking about code structure.
- Generic references are the source of truth. Worked examples are illustrative; if an example conflicts with a reference, the reference wins.
- Use [assets/design/jf-cli-design.md](assets/design/jf-cli-design.md) only as a worked benchmark after the first design pass.
- For the detailed target/profile system asset, load [assets/design/jf-cli-profile-system.md](assets/design/jf-cli-profile-system.md) when the CLI needs **saved remote targets and identities** (profiles/contexts/accounts), defaults or aliases for target selection, and switching behavior — not merely “more than one server”.

### Review

- Extract the current command tree, precedence rules, auth flow, and output contract before proposing changes.
- Compare the current behavior against the default rules and definition of done in this skill.
- Report concrete UX/DX regressions first, then list implementation fixes or missing tests.

### Implementation

- Load exactly one implementation reference after the UX contract is settled.
- Use [references/csharp.md](references/csharp.md) for .NET and Spectre.Console.Cli.
- Use [references/rust.md](references/rust.md) for Rust and clap.
- If you need a small teaching sketch instead of mining a full repository, prefer the canonical examples under [assets/examples/csharp/spectre](assets/examples/csharp/spectre) or [assets/examples/rust/clap](assets/examples/rust/clap).
- If the language is not C#/.NET or Rust, stop after the UX/DX design and translate it into framework-agnostic implementation guidance. Do not invent library-specific patterns.
- Do not load [tests/fixtures/jellyfin-openapi.json](tests/fixtures/jellyfin-openapi.json) unless you are intentionally replaying the bundled Jellyfin benchmark.

## Default Rules

- Prefer branches over flat command lists. `auth login` is better than `auth-login`.
- Do not read from stdin unless the user opted in with an explicit flag such as `--stdin` or `--password-stdin`, or the command is explicitly interactive and a TTY is present.
- Commands without the required arguments should print help or raise a validation error, not guess an implicit target such as "latest".
- Define and document a single precedence order for flags, environment variables, config, and defaults.

#### Additional rules for service CLIs

- Fail fast when auth or target resolution is missing. Do not start an interactive login flow from an unrelated command.
- Keep credentials separate from general config, and bind stored credentials to a canonical host key.

## Definition of Done

- Produce a top-level command tree and justify the grouping in user-facing terms.
- Make global flags, reserved flags, environment variables, and config/default precedence explicit.
- Define human output, machine output, stdout vs stderr rules, confirmation rules, and exit codes.
- Include language-specific implementation notes only when implementation is in scope. For other languages, provide framework-agnostic guidance.
- Include three to five validation checks or tests covering help, target resolution, non-interactive behavior, destructive flows, or machine-readable output.

#### Additional items for service CLIs

- Define auth, host, profile, credential-storage, and fallback-host behavior.
- Define target resolution validation checks.

## Deliverables

When implementing or redesigning a CLI, produce these artifacts unless the user asks for less:

- CLI classification (local-only / hybrid / service-native / multi-surface service).
- A top-level command tree and a short explanation of the grouping.
- Target/auth/context resolution and precedence (flags → env → config/profile/context → defaults).
- Automation contract (machine output style + versioning + stdout/stderr rules).
- TTY / non-interactive behavior (stdin, prompts, `--quiet`, `--yes`, `--dry-run`).
- Destructive action and confirmation rules.
- Output modes and exit codes.
- Error message strategy, diagnostic logging, and verbosity levels.
- Implementation notes only when implementation is in scope; otherwise keep it framework-agnostic.
- Three to five validation checks or tests.

#### Additional deliverables for service CLIs

- Auth, host, profile, and fallback-host behavior.
- Auth storage model and any canonical host-key rules.

## Pre-Implementation Extraction Checklist

Before you redesign a CLI, write down:

- Top-level branches and any expert-only or privileged labels that should survive.
- Exact environment variable names already in use.
- Stdout vs stderr rules for prompts, banners, streamed logs, and machine output.
- Domain-specific verbs or diagnostic commands that are part of the CLI's value, even if they do not fit a tiny generic verb set.

#### Additional extraction for service CLIs

- Exact auth modes and credential stores.
- Target-resolution order, fallback heuristics, and any git or directory inference.
