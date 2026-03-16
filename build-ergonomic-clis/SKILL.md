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

- Start with [references/ux-dx.md](references/ux-dx.md).
- Define the command tree, help contract, auth and profile model, target resolution, env/config precedence, reserved flags, confirmation rules, machine output, and non-interactive behavior before talking about code structure.
- Use [assets/design/jf-cli-design.md](assets/design/jf-cli-design.md) only when a worked self-hosted-service example would help benchmark the design after the first pass.

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
- Fail fast when auth or target resolution is missing. Do not start an interactive login flow from an unrelated command.
- Do not read from stdin unless the user opted in with an explicit flag such as `--stdin` or `--password-stdin`, or the command is explicitly interactive and a TTY is present.
- Commands without the required arguments should print help or raise a validation error, not guess an implicit target such as "latest".
- Keep credentials separate from general config, and bind stored credentials to a canonical host key.
- Define and document a single precedence order for flags, environment variables, config, and defaults.

## Definition of Done

- Produce a top-level command tree and justify the grouping in user-facing terms.
- Make global flags, reserved flags, environment variables, and config/default precedence explicit.
- Define auth, host, profile, credential-storage, and fallback-host behavior.
- Define human output, machine output, stdout vs stderr rules, confirmation rules, and exit codes.
- Include language-specific implementation notes only when implementation is in scope. For other languages, provide framework-agnostic guidance.
- Include three to five validation checks or tests covering help, target resolution, non-interactive behavior, destructive flows, or machine-readable output.

## Deliverables

When implementing or redesigning a CLI, produce these artifacts unless the user asks for less:

- A top-level command tree and a short explanation of the grouping.
- Global flags, reserved flags, environment variables, and config precedence.
- Exact environment variable names and whether they mirror a flag or a legacy behavior.
- Auth, host, profile, and fallback-host behavior.
- Auth storage model and any canonical host-key rules.
- Human output, machine output, confirmation, and exit code behavior.
- Stdout vs stderr rules for prompts, warnings, streamed logs, and machine-readable output.
- Error message strategy, diagnostic logging, and verbosity levels.
- An implementation or review checklist that maps the contract to code changes or follow-up work.
- Language-specific implementation changes and tests only when implementation is in scope. For other languages, provide framework-agnostic implementation guidance.
- Three to five validation checks or tests.

## Pre-Implementation Extraction Checklist

Before you redesign a CLI, write down:

- Top-level branches and any expert-only or privileged labels that should survive.
- Exact auth modes and credential stores.
- Exact environment variable names already in use.
- Target-resolution order, fallback heuristics, and any git or directory inference.
- Stdout vs stderr rules for prompts, banners, streamed logs, and machine output.
- Domain-specific verbs or diagnostic commands that are part of the CLI's value, even if they do not fit a tiny generic verb set.
