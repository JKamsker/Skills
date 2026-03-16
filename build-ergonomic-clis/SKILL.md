---
name: build-ergonomic-clis
description: Design, implement, refactor, or review ergonomic command-line applications with modern command trees, task-first help, clear auth and configuration UX, host and profile resolution, environment variable support, predictable scripting behavior, and no-surprises defaults. Use when working on CLI apps or CLI extensions in any language, especially C#/.NET with Spectre.Console.Cli and Rust with clap, or when a user asks for better CLI UX/DX, auth flows, self-hosted service configuration, reserved flags, non-interactive behavior, or command hierarchy design.
---

# Build Ergonomic Clis

Use this skill to design a CLI as a product surface instead of a thin dump of API endpoints or internal functions.

## Workflow

1. Start with [references/ux-dx.md](references/ux-dx.md).
   Use it to design the command tree, help contract, auth and profile model, environment variable support, reserved flags, confirmation rules, and non-interactive behavior.
2. If the task touches one of the local reference CLIs, extract its product-defining contracts before redesigning anything.
   Treat these as required inputs, not optional polish:
   - branch inventory and privileged labeling
   - auth storage model and host binding
   - cross-tool dependencies
   - exact environment variable names and compatibility aliases
   - target-resolution and fallback heuristics
   - stdout vs stderr behavior for prompts, logs, banners, and machine output
   - repo-specific diagnostic or expert verbs that are part of the product surface
3. Load exactly one implementation reference after the UX is settled.
   - Use [references/csharp.md](references/csharp.md) for .NET and Spectre.Console.Cli.
   - Use [references/rust.md](references/rust.md) for Rust and clap.
4. If the task touches one of the local reference CLIs, inspect that repo directly after reading the relevant reference file.
   - `C:\Users\Jonas\repos\private\JKamsker\Jellyfin-Cli`
   - `C:\Users\Jonas\repos\private\JKamsker\forgejo-cli-ext`
   - `C:\Users\Jonas\repos\private\JKamsker\ztnet-cli`
5. When a redesign intentionally differs from a local reference CLI, call out the deviation explicitly and explain why it is an improvement rather than drift.

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
- Exact environment variable names, compatibility aliases, and whether they mirror a flag or a legacy behavior.
- Auth, host, profile, and fallback-host behavior.
- Auth storage model, cross-tool dependencies, and any canonical host-key rules.
- Human output, machine output, confirmation, and exit code behavior.
- Stdout vs stderr rules for prompts, warnings, streamed logs, and machine-readable output.
- Error message strategy, diagnostic logging, and verbosity levels.
- Language-specific implementation changes and tests.

## Local Context

This skill is distilled from:

- `C:\Users\Jonas\repos\private\JKamsker\JKamsker-Skills\raw\Best-Practices-DX-UX.md`
- `jf`
- `fj-ex`
- `ztnet`

Reuse those projects as source material when the task matches them. Otherwise, use the references in this skill as the default playbook.

## Local Reference Contracts

When one of these CLIs is the target, preserve these contracts unless you are deliberately improving them and can explain why.

### `jf`

- Keep a broad, task-first Jellyfin surface. Do not over-compress the tree just to make `--help` shorter if that hides real Jellyfin workflows.
- Preserve Jellyfin-native diagnostic and expert verbs when they are genuinely useful, such as `explain-latest`, `playback-info`, `themes`, and similar domain-specific commands.
- Preserve explicit privileged labeling in help for admin-only actions.
- Preserve `auth quick`, `auth keys`, and `raw` as first-class surfaces.
- Improve target/config ergonomics, but do not throw away the hand-curated branch inventory that makes the actual CLI discoverable.

### `fj-ex`

- Treat it as a companion CLI that extends `fj`, not as a standalone service CLI.
- Preserve the dual-auth model when relevant:
  - UI session cookies and stored UI credentials for web-only endpoints.
  - Reuse of `fj`'s stored API token when that is part of the workflow.
- Preserve git-remote inference, explicit `--latest`, `FJ_FALLBACK_HOST`, and automatic re-login behavior.
- Preserve the concrete env vars and aliases used by the product, including `FJ_USER`, `FJ_PASS`, and `FJ_FALLBACK_HOST`, unless there is a compatibility migration plan.
- Preserve output-channel contracts for log streaming and similar workflows. For example, streamed log content may belong on stdout while separators or progress hints belong on stderr.
- Do not collapse product-defining behavior into a generic `auth`/`config` pattern if that hides the relationship with `fj`.

### `ztnet`

- Preserve canonical host-key matching, host defaults, and host-bound credential reuse.
- Preserve the distinction between token auth and session auth, including TOTP and session-auth-only surfaces where relevant.
- Preserve the concrete env vars and compatibility aliases used by the product, including `ZTNET_HOST`, `API_ADDRESS`, `ZTNET_EMAIL`, and `ZTNET_PASSWORD`, unless there is a compatibility migration plan.
- Preserve the profile-driven UX, `auth profiles`, `auth hosts`, multiple output modes, and API escape hatches.
- Note that the current reference may auto-pick the first matching profile for a host. The preferred ergonomic direction from this skill is stricter: require disambiguation instead of guessing when multiple profiles match.

## Pre-Implementation Extraction Checklist

Before you redesign a local reference CLI, write down:

- Top-level branches and any expert-only or privileged labels that should survive.
- Exact auth modes, credential stores, and whether another CLI is part of the auth story.
- Exact environment variable names and aliases already in use.
- Target-resolution order, fallback heuristics, and any git or directory inference.
- Stdout vs stderr rules for prompts, banners, streamed logs, and machine output.
- Domain-specific verbs or diagnostic commands that are part of the CLI's value, even if they do not fit a tiny generic verb set.
