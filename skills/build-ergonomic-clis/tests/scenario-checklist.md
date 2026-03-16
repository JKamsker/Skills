# Build Ergonomic CLIs — Behavioral Validation Checklist

Apply this checklist to a designed CLI contract (and to implementations where applicable).

## Non-interactive and safety

- [ ] Non-TTY destructive command without `--yes` refuses (exit `2`) and explains how to proceed.
- [ ] `--quiet` destructive command without override refuses (exit `2`) and explains how to proceed.
- [ ] `--dry-run` on a mutating command prints a preview and performs no mutations.
- [ ] Explicit user cancellation (answering “no”, Ctrl+C) returns exit `10` (not exit `2`).

## Machine output

- [ ] `--output json` / `--json` success produces valid, versioned machine output on stdout.
- [ ] `--output json` / `--json` expected failure produces valid, versioned machine output on stdout (for envelope-style designs).
- [ ] Banners/prompts/warnings do not appear on stdout in machine modes.

## Binary / file outputs

- [ ] Binary-producing commands either reject machine mode (exit `2`) or emit metadata-only JSON while writing bytes elsewhere.

## Target / profile / alias behavior (service-like CLIs)

- [ ] Single configured target resolves with zero flags (single-entry inference).
- [ ] Multiple targets require explicit selection or a defined default mapping (no silent guessing).
- [ ] Alias rewrites follow documented precedence and ambiguity behavior.
- [ ] Credential lookup uses the chosen target identity mode.
