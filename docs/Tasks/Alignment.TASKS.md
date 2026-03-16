# Alignment — Tasklist

Derived from `docs/Tasks/Alignment.md`.

## Phase 1 — Stabilize the core spec

- [x] Update `skills/build-ergonomic-clis/SKILL.md` with CLI classification, load rules, deliverable order, and precedence.
- [x] Update `skills/build-ergonomic-clis/references/cli-patterns.md` to centralize the automation contract, non-interactive rules, and exit-code policy.
- [x] Rewrite `skills/build-ergonomic-clis/references/service-cli-patterns.md` as a true service extension (target identity modes, multiple auth surfaces, protocol-level guidance, secret store examples).

## Phase 2 — Align the worked examples and code assets

- [x] Update `skills/build-ergonomic-clis/assets/design/jf-cli-profile-system.md` (hostname-key identity, explicit normalization rules, pseudocode, secret-store-first model, migration wording).
- [x] Update `skills/build-ergonomic-clis/assets/design/jf-cli-design.md` (explicit chosen policies, envelope JSON examples + versioning, secret storage, updated schema snippet).
- [x] Update C# runtime examples:
  - [x] `skills/build-ergonomic-clis/assets/examples/csharp/spectre/runtime/TargetResolver.cs` (base URL vs identity split; hostname-key identity; secret-store lookup).
  - [x] `skills/build-ergonomic-clis/assets/examples/csharp/spectre/runtime/DangerousActionGuard.cs` (interaction refusal = exit 2).
  - [x] `skills/build-ergonomic-clis/assets/examples/csharp/spectre/runtime/ApiCommand.cs` (JSON-envelope-safe errors; stderr for human errors).
  - [x] `skills/build-ergonomic-clis/assets/examples/csharp/spectre/runtime/DiagnosticLogger.cs` (log base URL + identity key).
- [x] Update Rust runtime examples:
  - [x] `skills/build-ergonomic-clis/assets/examples/rust/clap/profile_context.rs` (base URL vs identity split; hostname-key identity; secret-store lookup).
  - [x] `skills/build-ergonomic-clis/assets/examples/rust/clap/run_mode.rs` (interaction refusal = exit 2).

## Phase 3 — Improve language-specific references

- [x] Update `skills/build-ergonomic-clis/references/csharp.md` to separate generic/local baseline from service add-on guidance; add local discovery/passthrough guidance.
- [x] Update `skills/build-ergonomic-clis/references/rust.md` to separate generic/local baseline from service add-on guidance; add local discovery/passthrough guidance.

## Phase 4 — Expand evaluation and regression coverage

- [x] Expand `skills/build-ergonomic-clis/tests/routing-evals.csv` with hybrid, multi-surface, non-HTTP, local-only, pipeline, and envelope prompts.
- [x] Add `skills/build-ergonomic-clis/tests/regression-checks.md`.
- [x] Add `skills/build-ergonomic-clis/tests/scenario-checklist.md`.
