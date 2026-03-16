---
name: build-ergonomic-clis
description: Design, implement, refactor, or review ergonomic command-line applications with modern command trees, task-first help, clear auth and configuration UX, host and profile resolution, environment variable support, predictable scripting behavior, and no-surprises defaults. Use when working on CLI apps or CLI extensions in any language, especially C#/.NET with Spectre.Console.Cli and Rust with clap, or when a user asks for better CLI UX/DX, auth flows, self-hosted service configuration, reserved flags, non-interactive behavior, or command hierarchy design.
---

# Build Ergonomic Clis

Use this skill to design a CLI as a product surface instead of a thin dump of API endpoints or internal functions.

## Workflow

1. Start with [references/ux-dx.md](references/ux-dx.md).
   Use it to design the command tree, help contract, auth and profile model, environment variable support, reserved flags, confirmation rules, and non-interactive behavior.
2. Load exactly one implementation reference after the UX is settled.
   - Use [references/csharp.md](references/csharp.md) for .NET and Spectre.Console.Cli.
   - Use [references/rust.md](references/rust.md) for Rust and clap.
3. If the user wants independent design exploration, prompt benchmarking, or blind CLI design, do not feed implementation-specific fixtures into the design phase.
4. If comparison or review is requested, perform it only after the design is complete.

## Default Rules

- Prefer branches over flat command lists. `auth login` is better than `auth-login`.
- Fail fast when auth or target resolution is missing. Do not start an interactive login flow from an unrelated command.
- Do not read from stdin unless the user opted in with an explicit flag such as `--stdin` or `--password-stdin`, or the command is explicitly interactive and a TTY is present.
- Commands without the required arguments should print help or raise a validation error, not guess an implicit target such as "latest".
- Keep credentials separate from general config, and bind stored credentials to a canonical host key.
- Define and document a single precedence order for flags, environment variables, config, and defaults.

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
- Language-specific implementation changes and tests.

## Local Context

This skill is distilled from:

- `../raw/Best-Practices-DX-UX.md`

## Pre-Implementation Extraction Checklist

Before you redesign a CLI, write down:

- Top-level branches and any expert-only or privileged labels that should survive.
- Exact auth modes and credential stores.
- Exact environment variable names already in use.
- Target-resolution order, fallback heuristics, and any git or directory inference.
- Stdout vs stderr rules for prompts, banners, streamed logs, and machine output.
- Domain-specific verbs or diagnostic commands that are part of the CLI's value, even if they do not fit a tiny generic verb set.
