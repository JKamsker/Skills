# Build Ergonomic CLIs — Behavioral Validation Checklist

Apply this checklist to a designed CLI contract (and to implementations where applicable).

## Non-interactive and safety

- [ ] Destructive command refuses (exit `2`) when prompting is not allowed (stdin is not a TTY or stderr is not a TTY, or `--quiet`) unless `--yes` or `--dry-run` is present.
- [ ] `--dry-run` on a mutating command prints a preview and performs no mutations.
- [ ] Explicit user cancellation (answering “no”, Ctrl+C) returns exit `10` (not exit `2`).
- [ ] Machine output modes (resolved output mode: flags/env/config, e.g. `--json` or `--porcelain=v1`) never prompt, wait for input, or launch browsers; if interaction would be required, they refuse (exit `2`) with actionable guidance.

## Machine output

- [ ] `--output json` / `--json` success produces valid, versioned machine output on stdout.
- [ ] “Expected failures” (domain errors like not-found/not-authenticated/conflict) are represented per the contract style; they are not treated like unclassified crashes.
- [ ] `--output json` / `--json` expected failure produces valid, versioned machine output on stdout (for envelope-style designs).
- [ ] Banners/prompts/human-formatted warning lines do not appear on stdout in machine modes.
- [ ] In machine modes, avoid ad hoc stderr chatter: envelope-style uses machine metadata (e.g. `meta.warnings`) for warnings/diagnostic paths; direct-value / pipeline keeps stdout value-only and uses stderr for warnings/errors.
- [ ] For direct-value / JSONL pipeline commands, stdout stays value-only even on expected failures (errors go to stderr + exit codes).
- [ ] Direct-value machine contracts are still versioned via an explicit selector (`--porcelain=v1`, `--format-version 1`, etc.).
- [ ] Explicit selectors override convenience flags (e.g. `--porcelain=v2` wins over `--json`).
- [ ] Interaction-required refusal is represented per contract style: envelope emits a versioned error envelope on stdout; direct-value emits a stderr error + exit code while keeping stdout value-only.

## Binary / file outputs

- [ ] Binary-producing commands either reject `--json` (exit `2`) and write bytes to stdout (raw mode), or emit metadata-only JSON and write bytes to a separate destination (file path, temp file, or explicit `--output-file`).

## Target / profile / alias behavior (service-like CLIs)

- [ ] Single configured target resolves with zero flags (single-entry inference).
- [ ] Multiple targets require explicit selection or a defined default mapping (no silent guessing).
- [ ] Alias rewrites follow documented precedence and ambiguity behavior.
- [ ] Credential lookup uses the chosen target identity mode.

## Hybrid and multi-surface

- [ ] Hybrid CLIs clearly separate local-only behavior from remote-facing behavior (no hidden network calls in “local” branches).
- [ ] Multi-surface CLIs keep per-surface defaults separate (no implicit cross-surface bleed of auth/target selection).
- [ ] Non-HTTP transports document their protocol-specific failure modes and diagnostics (not HTTP-only assumptions).

## Local-only discovery

- [ ] Workspace/project discovery rules are deterministic (stop conditions, overrides, and surfaced resolved paths in `--dry-run`/`--verbose`).
