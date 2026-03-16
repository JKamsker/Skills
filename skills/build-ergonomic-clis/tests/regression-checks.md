# Build Ergonomic CLIs — Regression / Consistency Checks

Use this checklist when editing this skill. Check items before considering changes “done”.

## Doc ↔ Example alignment

- [ ] `SKILL.md` classification model matches `references/cli-patterns.md` and `references/service-cli-patterns.md`.
- [ ] `references/cli-patterns.md` defines the automation contract (styles + versioning + routing) and other docs do not contradict it.
- [ ] `references/service-cli-patterns.md` treats target identity as a design choice (hostname/origin/base-URL) and does not imply one universal canonical key.
- [ ] Worked examples (e.g. Jellyfin) are explicitly labeled as choices, not universal policy.
- [ ] Hybrid designs state which branches require service patterns and which are local-only (section-loading is explicit).
- [ ] Multi-surface service designs document per-surface defaults and precedence (no implicit cross-surface bleed).

## Target identity + secrets

- [ ] Target identity mode described in docs matches the resolver/runtime example assets.
- [ ] Secret-storage policy in generic docs matches the worked example and code assets (secrets separate by default; inline secrets only as an explicit fallback).

## Exit codes + interactivity

- [ ] Exit-code tables and examples match the code assets.
- [ ] Interaction-required refusal (quiet / non-TTY) maps to exit `2`.
- [ ] Explicit user cancellation maps to exit `10`.
- [ ] Machine output modes are non-interactive everywhere (no prompts, no banners).

## Stdout/stderr routing + machine contracts

- [ ] Machine contract examples match one of the allowed styles (envelope or direct-value) and are versioned.
- [ ] Machine stdout is not polluted by banners/prompts/warnings in machine modes.
- [ ] Direct-value / JSONL pipeline designs keep stdout value-only under failures (errors on stderr + exit codes).
- [ ] Service-specific guidance does not leak HTTP-only assumptions into generic guidance.

## Local-only discovery

- [ ] Local-only discovery rules are deterministic and documented (walking, stop conditions, override flags, and surfaced resolution in `--dry-run`/`--verbose`).
