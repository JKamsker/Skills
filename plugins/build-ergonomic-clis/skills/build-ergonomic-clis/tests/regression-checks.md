# Build Ergonomic CLIs — Regression / Consistency Checks

Use this checklist when editing this skill. Check items before considering changes “done”.

## Doc ↔ Example alignment

- [ ] `SKILL.md` classification model matches `references/cli-patterns.md` and `references/service-cli-patterns.md`.
- [ ] `references/cli-patterns.md` defines the automation contract (styles + versioning + routing) and other docs do not contradict it.
- [ ] `references/service-cli-patterns.md` treats target identity as a design choice (hostname/origin/base-URL) and does not imply one universal key format.
- [ ] Worked examples (e.g. Jellyfin) are explicitly labeled as choices, not universal policy.
- [ ] Hybrid designs state which branches require service patterns and which are local-only (section-loading is explicit).
- [ ] Multi-surface service designs document per-surface defaults and precedence (no implicit cross-surface bleed).

## Target identity + secrets

- [ ] Target identity mode described in docs matches the resolver/runtime example assets.
- [ ] Secret-storage policy in generic docs matches the worked example and code assets (secrets separate by default; sidecar key-file or inline encrypted secrets only as explicit fallbacks).

## Exit codes + interactivity

- [ ] Exit-code tables and examples match the code assets.
- [ ] Interaction-required refusal (quiet, or stdin/stderr not a TTY) maps to exit `2`.
- [ ] Explicit user cancellation maps to exit `10`.
- [ ] Machine output modes (resolved output mode: flags/env/config) are non-interactive everywhere (no prompts, no banners, no browser launches).

## Stdout/stderr routing + machine contracts

- [ ] Machine contract examples match one of the allowed styles (envelope or direct-value) and are versioned.
- [ ] Machine stdout is not polluted by banners/prompts/human-formatted warning lines in machine modes.
- [ ] “Machine metadata” means structured fields inside the machine contract (e.g. an envelope `meta` section), not free-form log lines.
- [ ] In envelope-style machine modes, avoid ad hoc stderr chatter (banners/progress/human warnings); represent warnings/diagnostic paths in machine metadata (e.g. `meta.warnings`).
- [ ] Direct-value / JSONL pipeline designs keep stdout value-only under failures (errors on stderr + exit codes).
- [ ] Service-specific guidance does not leak HTTP-only assumptions into generic guidance.

## Local-only discovery

- [ ] Local-only discovery rules are deterministic and documented (walking, stop conditions, override flags, and surfaced resolution in `--dry-run`/`--verbose`).
