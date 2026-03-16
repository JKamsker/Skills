# Build Ergonomic CLIs — Behavioral Validation Checklist

Apply this checklist to a designed CLI contract (and to implementations where applicable).

## Non-interactive and safety

- [ ] Non-TTY destructive command without `--yes` refuses (exit `2`) and explains how to proceed.
- [ ] `--quiet` destructive command without override refuses (exit `2`) and explains how to proceed.
- [ ] `--dry-run` on a mutating command prints a preview and performs no mutations.
- [ ] Explicit user cancellation (answering “no”, Ctrl+C) returns exit `10` (not exit `2`).
- [ ] Machine output modes (`--json`, porcelain) never prompt or block for interaction (including on stderr); if interaction would be required, they refuse (exit `2`) with actionable guidance.

## Machine output

- [ ] `--output json` / `--json` success produces valid, versioned machine output on stdout.
- [ ] `--output json` / `--json` expected failure produces valid, versioned machine output on stdout (for envelope-style designs).
- [ ] Banners/prompts/warnings do not appear on stdout in machine modes.
- [ ] In machine modes, avoid ad hoc stderr chatter; warnings and diagnostic paths are represented in the machine contract metadata when possible.
- [ ] For direct-value / JSONL pipeline commands, stdout stays value-only even on expected failures (errors go to stderr + exit codes).
- [ ] Direct-value machine contracts are still versioned via an explicit selector (`--porcelain=v1`, `--format-version 1`, etc.).

## Binary / file outputs

- [ ] Binary-producing commands either reject machine mode (exit `2`) or emit metadata-only JSON while writing bytes elsewhere.

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
