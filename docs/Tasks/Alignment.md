# Updated Improvement Plan for the Skill

## 1. Goal

Turn the skill from a set of individually good documents into a **single coherent design system for CLI ergonomics**.

The core problem is not just missing content. It is that the skill currently contains **multiple conflicting truths** across:

* `SKILL.md`
* `references/cli-patterns.md`
* `references/service-cli-patterns.md`
* `assets/design/jf-cli-design.md`
* `assets/design/jf-cli-profile-system.md`
* `assets/examples/csharp/...`
* `assets/examples/rust/...`

This plan fixes those contradictions first, then expands the skill to cover hybrid tools, multi-surface service tools, local project tooling, and machine contracts in a consistent way.

---

## 2. Primary objectives

The work should achieve six outcomes.

### A. Remove internal contradictions

Resolve disagreements between docs and examples around:

* target identity / canonical host key behavior
* secret storage policy
* exit-code behavior for confirmation-required failures
* stdout/stderr routing for machine output
* terminology and command naming drift

### B. Add a real CLI classification model

Replace the current binary “local vs service” gate with a model that works for:

* local-only tools
* hybrid tools
* service-native tools
* multi-surface service tools

### C. Make automation behavior explicit

Every designed CLI should have a clearly documented machine contract, TTY behavior, dry-run semantics, quiet-mode behavior, and exit-code policy.

### D. Keep the reference model simple

Do **not** add a new top-level `contracts.md`.
Instead, consolidate automation rules inside `references/cli-patterns.md` and let `references/service-cli-patterns.md` extend them.

### E. Realign the worked Jellyfin example

The Jellyfin worked example should become internally consistent and clearly marked as one concrete design choice, not mistaken for a universal rule.

### F. Add evaluation coverage for the actual weak spots

The routing and prompt-evaluation files should start catching hybrid, non-HTTP, multi-auth, local build-tool, and filter/pipeline scenarios.

---

## 3. Guiding principles

These principles should govern every edit.

### 3.1 Contradiction-first, not additive-first

Do not just add more prose. First remove or reconcile existing conflicts.

### 3.2 Generic rules first, worked examples second

The generic references define the allowed design space.
Worked examples then choose one path inside that space.

### 3.3 Defaults are allowed, hidden assumptions are not

The skill may recommend defaults, but it must force designers to explicitly choose when multiple valid patterns exist.

### 3.4 The skill should be prescriptive where consistency matters

Be explicit about:

* classification
* output routing
* TTY behavior
* exit-code structure
* dry-run guarantees
* confirmation rules

### 3.5 The skill should stay flexible where design space is legitimately plural

Do not force one universal policy for:

* target identity shape
* machine-output shape
* profile/context naming
* service-transport details

Instead, define supported options and require the design to pick one and justify it.

---

## 4. Locked decisions

These decisions should be treated as the foundation for all edits.

## 4.1 CLI classification model

Every CLI designed with the skill must be classified into one of four buckets at the beginning of Design mode.

### 1. Local-only

The CLI operates on local files, local processes, local project state, or local configuration only.

Examples:

* build tools
* code generators
* local project utilities

### 2. Hybrid

The CLI has a local-first or mixed model, but some branches optionally connect to remotes.

Examples:

* VCS-style tools
* developer tools with optional cloud sync
* CLIs that can run fully locally but also talk to services

### 3. Service-native

The CLI primarily exists to interact with one remote service or API surface.

Examples:

* cloud storage CLIs
* SaaS admin CLIs
* service account / data / task management CLIs

### 4. Multi-surface service

The CLI interacts with multiple independent target or auth surfaces.

Examples:

* Docker-like tools
* container tools
* database/admin tools with transport auth plus identity auth
* registry + daemon + context + cert combinations

This classification determines which sections of the references must be applied.

---

## 4.2 Target identity is a chosen mode, not a universal rule

The skill should stop pretending there is one correct canonical host key format.

Instead, service-like designs must choose one of these **target identity modes**:

### A. Hostname key

Examples:

* `jf.example.com`
* `nas.local`

Use when credentials are intended to follow the logical host regardless of scheme, port, or path.

### B. Origin key

Examples:

* `https://jf.example.com`
* `https://jf.example.com:8443`

Use when scheme and/or port matter for identity and credential binding.

### C. Full base-URL key

Examples:

* `https://api.example.com/team-a`
* `https://api.example.com/root/admin`

Use when path-scoped tenancy or deployment boundaries make path part of identity.

### Rule

The skill must require the designer to state:

* which target identity mode is chosen
* why that mode is correct for the tool
* what normalization rules apply
* how aliases and migrations work

The worked Jellyfin example will choose **hostname mode**.

---

## 4.3 Machine output supports two sanctioned contract styles

The skill should not require one single JSON wrapper shape for all CLIs.

Instead, it should define two supported machine-contract styles.

### A. Envelope contract

Recommended default for most administrative, service, or stateful CLIs.

Typical shape:

```json
{
  "ok": true,
  "data": {},
  "error": null,
  "meta": {
    "schemaVersion": 1
  }
}
```

Use when:

* commands are stateful
* recovery guidance matters
* exit/error metadata is useful
* automation benefits from uniformity

### B. Direct-value contract

Allowed for filter-style, pipeline-style, or data-transform commands.

Examples:

* bare object
* bare array
* scalar
* JSONL stream

Use when:

* the command behaves like a filter
* envelope wrappers would add friction in pipelines
* output is intentionally raw data

### Rule

Every CLI must explicitly document:

* which machine contract style it uses
* whether the choice is global or command-specific
* how failures are represented
* how the contract is versioned

Envelope is the strong default. Direct-value is allowed, but must be explicit.

---

## 4.4 Versioning of machine contracts is mandatory

Every machine-facing output contract must be versioned.

Allowed strategies:

### A. In-band versioning

Example:

* `meta.schemaVersion = 1`

### B. Selector versioning

Examples:

* `--porcelain=v1`
* `--format-version 1`
* `--output json-v1`

### Rule

The skill should say:

* human output may evolve more freely
* machine contracts must have an explicit stability boundary
* breaking changes require either a new version or an explicit opt-in selector

---

## 4.5 Exit codes are split into a base set and a service extension

### Base exit codes

Used across all CLI classes.

* `0` success
* `1` general runtime failure
* `2` usage / validation / interaction-required / non-interactive refusal
* `5` not found
* `6` conflict / precondition failed
* `10` explicit user cancellation / abort / SIGINT-equivalent

### Service extension exit codes

Only used where they make sense.

* `3` not authenticated
* `4` authenticated but not authorized
* `7` rate limited / backpressure / quota refusal
* `8` transport / connection / TLS / timeout / protocol-unreachable failure

### Rule

Confirmation-required actions that cannot proceed because of `--quiet`, missing `--yes`, or non-TTY prompting constraints must return **exit 2**, not 10.

Exit 10 is reserved for actual user abort/cancel behavior.

---

## 4.6 Keep the two-reference-file model

Do **not** create a new `references/contracts.md`.

Instead:

* `references/cli-patterns.md` becomes the canonical location for automation contract rules
* `references/service-cli-patterns.md` extends that for remote/service-specific behavior
* `SKILL.md` tells the model when to apply which sections

This preserves the simpler structure that already works well.

---

## 5. File-by-file change plan

## Phase 1 — Stabilize the core spec

This phase defines the new source of truth.

### 5.1 `SKILL.md`

#### Add a required classification step

At the start of Design mode, require the agent to classify the CLI as:

* local-only
* hybrid
* service-native
* multi-surface service

#### Replace the binary remote gate

Current behavior is effectively:

* if remote, also read `service-cli-patterns.md`

Replace with:

* local-only → use `cli-patterns.md`
* hybrid → use `cli-patterns.md` plus only the relevant service sections
* service-native → use both fully
* multi-surface service → use both fully and apply multi-auth/context guidance explicitly

#### Tighten the load conditions for `jf-cli-profile-system.md`

Replace vague wording like “multi-server profile management” with a clearer trigger:

Load the profile-system design asset when the CLI needs:

* saved remote targets
* saved user identities or credentials
* defaults or aliases for target selection
* host/context/account switching

Not merely when “more than one server” exists.

#### Standardize deliverable order

Require the generated design output to present sections in this order:

1. CLI classification
2. command tree
3. target/auth/context resolution
4. automation contract
5. TTY / non-interactive behavior
6. destructive action and confirmation rules
7. output and exit codes
8. implementation notes
9. tests / validation checklist

#### Clarify artifact precedence

If generic references and worked examples conflict, generic references win.
Worked examples must be treated as examples, not implicit policy.

---

### 5.2 `references/cli-patterns.md`

This file should become the canonical automation-spec document.

#### Add a major section: `Automation Contract`

This section should define:

* machine contract choice:

  * envelope
  * direct-value
* versioning requirement
* stdout/stderr routing rules
* success/failure representation
* expectations for parseability
* schema evolution expectations

#### Add `TTY and Non-Interactive Rules`

Gather all current scattered rules into one section.

Define:

* when prompts are allowed
* when prompts are forbidden
* how non-TTY contexts behave
* how `--yes` changes behavior
* how `--quiet` changes behavior
* how stdin-based input flags should behave

#### Add `Quiet Mode`

Define `--quiet` precisely:

* suppresses non-essential human-facing chatter
* suppresses prompts
* does not suppress machine stdout
* does not change exit codes
* does not prevent diagnostic artifacts from being written if already specified by policy

#### Add `Dry-Run Semantics`

Define the guarantee:

* dry-run never mutates
* local reads are allowed
* remote reads are only allowed if the command explicitly documents live validation/planning
* dry-run should explain what would happen
* dry-run should not silently perform side effects

#### Split output and exit codes

Separate:

* base exit codes
* service extension exit codes

and clarify when each table applies.

#### Add `Binary and Stream Output`

Define:

* how commands that emit files/binary payloads behave
* whether JSON is disallowed or metadata-only
* when JSONL is appropriate
* expectations for follow/watch/stream commands

#### Add `Help and Version Output`

Define:

* `--help` prints to stdout and exits 0
* `--version` prints to stdout and exits 0
* help pages should identify destructive commands clearly
* help pages should include brief “how to start” guidance where appropriate

#### Add `Local Project Discovery`

This section should cover:

* walking up parent directories
* stop conditions
* explicit file override flags
* workspace/root behavior
* showing resolved project/file in diagnostics or dry-run
* child-process stdio passthrough rules for local tooling

This is the main missing local-tool pattern.

---

### 5.3 `references/service-cli-patterns.md`

This file should become a service extension, not an HTTP-shaped parallel rulebook.

#### Replace `Hostname Normalization and Canonical Keys`

Rename and rewrite as `Target Identity Modes`.

Document:

* hostname key
* origin key
* full base-URL key

For each, include:

* what it looks like
* what it is good for
* what risks it has
* example normalization notes

#### Add `Resolution Algorithm`

Include a one-page template describing the sequence:

* flags
* env
* explicit profile/context/account selection
* defaults
* alias rewrite
* target identity derivation
* credential lookup
* final effective target

The file should require that every designed service-like CLI state its precedence explicitly.

#### Add `Multiple Auth Surfaces`

Cover tools where auth is not one flat concept.

Examples:

* endpoint context vs user login
* daemon auth vs registry auth
* transport certs vs account tokens
* DSN/connection-string auth vs saved profiles

#### Add `Context / Profile / Account Vocabulary`

State that names differ by ecosystem, but the design must define:

* what a target is
* what a profile/context is
* what a credential binding is
* what defaults exist
* whether names are globally unique or unique-within-target

#### Rename HTTP-specific sections

Rename:

* `HTTP-Specific Error Handling` → `Protocol-Level Error Handling`
* `HTTP Diagnostic Logging` → `Protocol-Level Diagnostic Logging`

Then rewrite examples so HTTP is one example, not the default assumption.

#### Add config-store and secret-store examples

Show a redacted example of:

* general config
* target/profile metadata
* separately stored credentials

Also document fallback behavior if a tool chooses inline secret storage.

#### Clarify migration wording

Replace any notion of “silent migration” with:

* automatic one-time migration
* brief stderr note in human mode
* no unexpected noisy output in machine mode

---

## Phase 2 — Align the worked examples and code assets

This is the highest-value and highest-effort phase after core-spec stabilization.

## 6. Jellyfin worked example policy

The worked Jellyfin example should explicitly choose:

* **CLI class:** service-native
* **target identity mode:** hostname key
* **machine contract style:** envelope as the default
* **secret storage policy:** separate secret store preferred
* **confirmation refusal code:** exit 2

This choice is allowed to be Jellyfin-specific, but it must be clearly labeled as such.

---

### 6.1 `assets/design/jf-cli-profile-system.md`

#### Keep hostname-keyed behavior

Do not convert Jellyfin to origin-based identity just because the examples currently do that.

Instead, make the doc explicit that Jellyfin is choosing hostname-keyed behavior because:

* the service is conceptually host-centric
* host entries provide the default base URL
* profiles may override base URLs
* credentials are intended to follow the logical host identity

#### Add exact normalization rules

Document precisely:

* lowercase behavior
* trimming
* handling of schemes
* handling of ports
* handling of paths
* handling of IPs
* handling of aliases
* how defaults interact with host selection

#### Add pseudocode

Include explicit pseudocode for:

* deriving the target identity key
* resolving current host/profile/default selection
* applying aliases
* choosing the effective base URL

#### Clarify uniqueness model

Make explicit whether:

* hostnames are globally unique
* profile names are unique within a host only
* defaults are global, per-host, or both

#### Update secret-storage examples

Move secrets out of inline config in the preferred design.
If inline secrets remain shown, label them clearly as fallback or simplified examples.

---

### 6.2 `assets/design/jf-cli-design.md`

#### Align with the canonical machine-contract language

Update all JSON output examples to match the chosen envelope style and versioning story.

#### Clarify routing behavior

Document exactly:

* where success JSON goes
* where expected structured failures go
* when stderr may still be used
* what happens in human mode

#### Clarify interactivity

State explicitly:

* `--json` / `--output json` is non-interactive
* no prompts
* no browser launches
* no banners
* no progress spinners unless documented separately for human mode

#### Fix exit-code drift

Update all confirmation and cancellation examples so:

* non-interactive refusal / missing confirmation → exit 2
* explicit cancellation → exit 10

#### Normalize naming

Choose one branch naming style and apply it consistently:

* `auth hosts` vs `auth host`
* `profile` vs `context` where applicable

For Jellyfin, keep one chosen naming convention and use it everywhere.

---

### 6.3 Resolver and runtime example code

This is the most labor-intensive part of the whole plan and must be treated as a first-class task.

#### `assets/examples/csharp/spectre/runtime/TargetResolver.cs`

Change the implementation so it no longer treats runtime base URL normalization and target identity generation as the same thing.

Introduce separate concepts:

* `NormalizeBaseUrl(...)`
* `CanonicalTargetIdentity(...)`

For the Jellyfin example:

* `CanonicalTargetIdentity(...)` should return lowercase hostname only
* runtime base URL can still preserve scheme/port/path as needed for actual requests

#### `assets/examples/rust/clap/profile_context.rs`

Make the same split:

* one function for normalizing the effective URL used for network operations
* one function for deriving the hostname-key identity used for profile/credential binding

#### `DangerousActionGuard.cs`

Update refusal behavior:

* missing `--yes`
* `--quiet` with confirmation-required action
* non-TTY prompt attempts

All of these should map to exit 2, not cancel 10.

#### `ApiCommand.cs`

Make the same exit-code correction and ensure machine-mode behavior matches the docs.

#### `run_mode.rs`

Align the same refusal/cancellation split:

* refusal to proceed because of interaction policy → exit 2
* explicit cancel/abort → exit 10

#### Add comments/docstrings

Mark these example assets as implementing the **Jellyfin hostname-mode variant**, not a universal rule for all service CLIs.

---

## Phase 3 — Improve language-specific references

These changes keep the implementation references from forcing service/HTTP assumptions into generic design work.

### 7.1 `references/csharp.md`

Split into two conceptual parts.

#### Generic/local baseline

Cover:

* command modeling
* config loading
* option parsing
* output routing
* local file discovery
* process execution
* stdin/stdout passthrough
* testing approach for local commands

#### Service add-on

Cover:

* HTTP client setup
* auth injection
* retry strategy
* diagnostics
* target/profile resolution
* service-specific exception mapping

Do not present `IHttpClientFactory` as the default baseline for every CLI.

---

### 7.2 `references/rust.md`

Do the same split.

#### Generic/local baseline

Cover:

* clap structure
* config loading
* local path/project discovery
* process spawning
* passthrough IO
* testing strategy for command parsing and resolution

#### Service add-on

Cover:

* reqwest / networking
* auth handling
* retries/timeouts
* target/profile resolution
* diagnostics and service failure mapping

Do not present `reqwest` and async networking as the assumed starting point for every CLI.

---

## Phase 4 — Expand evaluation and regression coverage

The current tests do not target the failure modes the reviews exposed.

### 8.1 Expand `tests/routing-evals.csv`

Add prompts that force the skill to demonstrate correct routing and section loading for:

#### Hybrid CLI

Example:

* Git-like tool with optional remotes
* local operations that do not require service patterns
* remote sync/push branches that do

#### Multi-surface service CLI

Example:

* Docker-like tool with contexts, daemon transport, registry auth, certificates

#### Non-HTTP service CLI

Example:

* database or socket-based administrative CLI
* TLS/cert transport concerns
* no HTTP status-code framing

#### Local-only build tool

Example:

* manifest discovery
* parent walking
* explicit file overrides
* child process passthrough

#### Filter/pipeline tool

Example:

* command where direct-value JSON is the correct machine contract
* should not be forced into an envelope

#### Envelope-style service CLI

Example:

* administrative service operations
* should choose envelope and versioning

---

### 8.2 Add consistency/regression checks

Add review checks that explicitly verify:

* target identity mode in docs matches target identity mode in code
* secret-storage policy in generic docs matches worked examples
* exit-code examples match runtime code snippets
* TTY/non-interactive rules are consistent everywhere
* machine contract examples match the documented allowed styles
* service extensions do not bleed into local-only guidance

---

### 8.3 Add behavioral validation checklist

Each example design should be testable against scenarios like:

* non-TTY destructive command without `--yes`
* `--quiet` destructive command without override
* `--output json` success
* `--output json` expected failure
* dry-run on mutating command
* binary-producing command with machine-output flag
* single configured target resolution
* multiple targets with explicit selection
* alias rewrite behavior
* credential lookup for current target identity mode

---

## 9. Detailed policy clarifications to write into the docs

These should appear explicitly, not remain implicit.

## 9.1 `--quiet`

Define it as:

* suppress human-facing non-essential output
* suppress prompts
* never suppress machine stdout
* never alter success/failure semantics
* never silently convert an unsafe command into a safe command
* may still allow diagnostic files/logs according to policy

---

## 9.2 `--dry-run`

Define it as:

* never mutates state
* should explain intended action
* local inspection is allowed
* remote reads are allowed only if the command explicitly documents live planning/validation
* if dry-run still requires network access, that must be stated clearly

---

## 9.3 Binary output commands

Define one of two patterns:

* reject JSON mode with a clear validation error
* or provide metadata-only JSON while writing the binary/file elsewhere

Do not leave this ambiguous.

---

## 9.4 Machine contract routing

The docs should not claim one universal stdout/stderr policy for all CLIs.

Instead, require the design to declare the chosen rule.

Recommended defaults:

### Envelope-style commands

* success and expected structured failures on stdout
* stderr reserved for pre-contract or infrastructural failure cases

### Direct-value/pipeline-style commands

* value output on stdout
* errors on stderr

The key is explicitness and consistency.

---

## 9.5 Secret storage

Define the recommended policy as:

* preferred: separate secret store / OS credential store / external helper
* fallback: inline secrets only if clearly documented and justified

Then make the Jellyfin worked example conform.

---

## 10. Priority order

This is the recommended execution order.

## Priority 1 — Core contradictions

Do these first.

* lock the six decisions
* update `SKILL.md`
* update `references/cli-patterns.md`
* update `references/service-cli-patterns.md`

This phase creates the new source of truth.

---

## Priority 2 — Worked example alignment

Do these second.

* update `assets/design/jf-cli-profile-system.md`
* update `assets/design/jf-cli-design.md`
* rewrite resolver/runtime examples
* fix exit-code mappings in code assets
* align naming and secret-storage examples

This is the highest-effort phase after core policy stabilization.

---

## Priority 3 — Implementation reference cleanup

Do these third.

* split `references/csharp.md`
* split `references/rust.md`
* remove service assumptions from the generic baselines
* ensure code examples are consistent with the new Jellyfin choices

---

## Priority 4 — Evaluation expansion

Do these fourth.

* add new routing evals
* add regression/consistency checks
* expand scenario checklist coverage

---

## 11. Risks and mitigation

## Risk 1: More prose but still more drift

If edits are done piecemeal, contradictions will persist.

### Mitigation

Treat Phase 1 as a lockstep rewrite of the governing rules before touching examples.

---

## Risk 2: Resolver code changes get underestimated

This is the main high-effort item.

### Mitigation

Treat resolver/runtime code alignment as a separate tracked subproject inside Phase 2, not as incidental cleanup.

---

## Risk 3: Over-prescription of JSON shape

Forcing one wrapper format on all CLIs would make the skill less honest.

### Mitigation

Define envelope as the default, direct-value as allowed, and versioning as mandatory.

---

## Risk 4: Reference sprawl reduces usability

Adding too many files would worsen discoverability.

### Mitigation

Keep the two-reference-file model and consolidate automation rules into `cli-patterns.md`.

---

## Risk 5: Service guidance remains HTTP-biased

Even renamed sections can remain conceptually HTTP-centric if examples are not rewritten.

### Mitigation

Use examples from HTTP, socket/TCP, TLS/cert, and DSN-style tools.

---

## 12. Definition of done

The improvement plan should be considered complete only when all of the following are true.

### Core doc consistency

* `SKILL.md`, `cli-patterns.md`, and `service-cli-patterns.md` no longer contradict each other
* classification rules are explicit
* automation contract rules are centralized
* target identity is documented as a design choice, not a universal assumption

### Worked example consistency

* Jellyfin docs and example code agree on hostname-mode identity
* secret-storage guidance is aligned
* exit-code examples match runtime code
* naming is consistent across docs and examples

### Implementation reference consistency

* C# and Rust references distinguish generic/local from service-specific guidance
* service networking is no longer presented as the universal baseline

### Evaluation coverage

* routing evals cover hybrid, local-only, multi-surface, non-HTTP, direct-value, and envelope cases
* consistency checks catch the major drift categories

### Usability

* the skill remains easy to follow
* the two-reference-file model is preserved
* worked examples are clearly examples, not accidental policy

---

## 13. Final recommended execution summary

The complete updated plan is:

1. Rewrite the core spec around four CLI classes, explicit automation contracts, split exit codes, and target identity modes.
2. Keep the reference model simple by making `cli-patterns.md` the canonical automation-contract document and `service-cli-patterns.md` the service extension.
3. Realign the Jellyfin worked example around a clearly chosen hostname-key identity model.
4. Treat resolver/runtime example rewrites as a major tracked task, not as minor cleanup.
5. Split language references into generic/local baseline plus service add-on.
6. Expand evaluations so the skill is tested against the exact categories it currently underserves.
7. Do not declare the work done until docs, examples, code assets, and tests all agree.
