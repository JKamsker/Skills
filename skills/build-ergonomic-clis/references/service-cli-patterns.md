# Service CLI Patterns

Supplementary patterns for CLIs that connect to remote services or APIs. Read [cli-patterns.md](cli-patterns.md) first — especially the automation contract and the non-interactive rules.

## Auth Design

Keep authentication explicit and contained.

- Put auth under `auth`.
  - Common commands: `login`, `logout`, `status`, `show`, `whoami`, `set-token`, `test`
- Prefer explicit auth modes over hidden fallback behavior.
  - Good: `tool auth login --device`
  - Avoid: running `tool deploy` and silently opening a login prompt
- Fail fast on missing auth for protected commands and print the exact recovery command.
  - Example: `Authentication required. Run 'tool auth login'.`
- If the tool supports secrets from stdin, make that opt-in and named.
  - `--stdin`, `--password-stdin`, `--token-stdin`

For browser-capable service CLIs:

- Prefer system browser plus PKCE or a service-native device/quick-connect flow.
- Fall back to pasted tokens only when the service actually uses them.
- In resolved machine output modes (selected via flags/env/config/profile defaults; for example: `--json`, `--output json`, `--porcelain=v1`), never auto-launch a browser or show interactive prompts. If auth would require user interaction (browser/device/quick-connect), refuse (exit `2`) and require a truly non-interactive alternative (for example: token via stdin) or instruct re-running in human mode (for example: pass `--output human` / `--no-json`, ensure stdin and stderr are TTYs, and remove `--quiet`; note that env/config/profile defaults may still force machine output unless overridden by flags).
- Protected commands that require authentication but do not perform an auth flow should fail with `not authenticated` (exit `3`) and print the explicit recovery command (for example: `tool auth login`).
- Treat `--quiet` as non-interactive even in human output modes: never prompt and never initiate browser/device/quick-connect flows when `--quiet` is set. Refuse (exit `2`) with a non-interactive alternative or an instruction to re-run without `--quiet`.
- Treat `--output human` / `--no-json` as mutually exclusive with machine selectors (`--json`, `--output json`, porcelain selectors). If combined, refuse (exit `2`) with an actionable error.

For API-token-based CLIs:

- Support `auth set-token TOKEN` and `auth set-token --stdin`.
- Validate tokens by default unless the user passed `--no-validate`.

## Targets, Profiles/Contexts, Defaults

Service-like CLIs (service-native tools and the remote-facing branches of hybrid tools) need an explicit target model. Recommended model:

- A **profile/context** stores non-secret defaults (timeouts, output mode, default scope) plus target binding metadata.
- **Credentials are bound to a derived target identity key** (not just a profile name).
- The active profile/context is one input to resolution, not magic global state.

Useful commands (names may differ; define your vocabulary):

```text
tool auth profiles list
tool auth profiles use <name>
tool auth targets list
tool auth targets set-default <target> <profile>
tool config path
tool config get
tool config set
```

## Target Identity Modes (design choice)

There is no single universal target identity key. Service-like CLIs must choose a target identity mode and document:

- which mode is chosen
- why it is correct for the tool
- normalization rules
- aliases and migration behavior

### A) Hostname key

Examples: `jf.example.com`, `nas.local`, `192.168.1.50`

Use when credentials should follow the logical host regardless of scheme/port/path.

Normalization notes (typical):

- trim
- accept either hostname or URL input, extract hostname
- lowercase hostname before using it as a key
- ports/paths belong in the effective base URL, not the identity key

Risks:

- multiple deployments on different ports/paths share identity unless you model them as separate logical hosts.

### B) Origin key

Examples: `https://jf.example.com`, `https://jf.example.com:8443`

Use when scheme/port boundaries are security or tenancy boundaries.

Normalization notes (typical):

- normalize scheme casing
- lowercase hostname
- include non-default ports
- drop path/query/fragment

Risks:

- “same host, different port” becomes distinct identities (which may be intended).

### C) Full base-URL key

Examples: `https://api.example.com/team-a`, `https://api.example.com/root/admin`

Use when path-scoped tenancy makes path part of identity.

Normalization notes (typical):

- normalize scheme + hostname casing
- normalize path rules (trailing slash, dot-segments)
- document whether query params ever participate (usually: no)

Risks:

- small URL differences can create surprising identity fragmentation unless normalization is strict.

## Resolution Algorithm (template)

Every service-like CLI should present a one-page resolution sequence. A good template:

1. Read **explicit flags** (target + profile/context + auth overrides).
2. Read **environment variables** (mirroring flags).
3. Resolve **explicit target tokens** (exact target key match first, alias scan second, per documented ambiguity policy).
4. If the target is still unresolved, apply **target defaults** (global default, single-target inference).
5. If the target is still unresolved and you support it, apply **context inference** (git remotes, workspace markers, directory discovery) and make it inspectable.
6. Resolve **profile/context/account selection against the selected target**: explicit profile first, then per-target default profile/context, then single-profile inference.
7. Resolve the **effective output mode and prompt eligibility** from flags/env/config/profile defaults before any prompt; if the resolved mode is machine output, never prompt.
8. Derive the **target identity key** (per the chosen mode).
9. Look up **credentials** using the identity key (and the selected profile/context if applicable).
10. Produce the final **effective target** used for network operations (base URL, timeouts, retries).

Rules:

- If profile/context names are only unique within a target, do not attempt final profile resolution until the target is known.
- If multiple profiles match and there is no default mapping, require an explicit selection instead of guessing.
- If repo/directory inference exists, document it as a lower-priority fallback, not the primary contract.
- After the target is known, still apply the target-scoped default profile/context or single-profile inference even if the target came from an explicit flag or context inference.
- Resolve the final output mode before any prompt, because flags/env/config/profile defaults may switch the command into machine output.
- In machine output modes, never prompt; return an actionable error (exit `2`).
- In human output modes, only prompt when `--quiet` is not set and stdin and stderr are attached to a terminal; otherwise refuse (exit `2`) with an actionable message.

## Aliases, Inference, Validation, Migration

### Optional aliases

If you support aliases (e.g. `home`, `prod`), define:

- resolution order (exact target key match first, alias scan second)
- ambiguity behavior (deterministic tie-break + warning, or hard error — choose one and document it)
- shadowing behavior (if alias equals a real target key, target key wins; warn when created)

Warning routing:

- Envelope-style machine modes: represent ambiguity warnings in machine metadata (e.g. `meta.warnings`).
- Direct-value/pipeline machine modes: represent ambiguity warnings on stderr (keep stdout value-only).

### Single-entry inference

One target configured? Use it. One profile on a target? Use it. This yields zero-config behavior for the common single-server, single-account case without hidden global state.

### Validation on load and write

Validate referential integrity on every config load and never write invalid config. Cascade cleanup on delete operations (e.g. deleting the last profile can delete the target entry).

### One-time migration

If the old-format config exists and the new format does not:

- perform an automatic one-time migration on first run
- back up the old file (`.bak`)
- emit a brief stderr note in human mode
- in machine output modes, route the migration note according to the machine contract: envelope/porcelain → structured warning in machine metadata (avoid ad hoc stderr noise); direct-value/pipeline → stderr warning while keeping stdout value-only

## Config Store vs Secret Store (recommended)

Do not store plaintext secrets in the general config by default. Prefer:

- OS credential store / external helper / keyring integration, or
- a separate credential file with clearly documented permissions and redaction rules, or
- a single config file with encrypted credential blobs plus a sidecar key file when that tradeoff is explicitly chosen and documented

### Example: redacted general config (non-secret)

```jsonc
{
  "defaultTarget": "jf.home.example.com",
  "targets": {
    "jf.home.example.com": {
      "baseUrl": "https://jf.home.example.com",
      "aliases": ["home", "jf"],
      "defaultProfile": "main",
      "profiles": {
        "main": {
          "user": "jonas",
          "output": "table"
        },
        "admin": {
          "user": "admin",
          "output": "json"
        }
      }
    }
  }
}
```

### Example: secret binding (conceptual)

Key the secret store by:

- target identity key (per the chosen mode)
- profile/context name (if relevant)
- credential kind (token, api key, cookie)

Example key names:

- `tool:cred:{targetKey}:{profile}:token`
- `tool:cred:{targetKey}:{profile}:apiKey`

If a tool chooses inline secret storage anyway, it must:

- label it explicitly as a tradeoff
- document file permissions expectations
- ensure all diagnostics and config dumps redact secrets

### Sidecar key-file option (explicit fallback)

Some CLIs may choose a single config file for normal settings plus encrypted credential blobs, protected by a sidecar key file such as `tool.key.pem`. This is allowed as an explicit fallback design, not the default recommendation.

If a tool chooses this model, it must:

- label it explicitly as a tradeoff rather than presenting it as equivalent to OS credential storage
- document the exact file layout and permissions for both the config file and the key file
- state whether the key file is generated automatically, where it lives by default, and how to override the path
- keep the encrypted credential material in the config and keep the raw key material only in the sidecar file
- allow app-specific KDF context or namespace material, but do not treat a hard-coded application key as the primary security boundary
- avoid machine-identity or hardware-derived key material unless the design can tolerate hardware churn and recovery complexity
- document the real protection boundary: if an attacker obtains both the config and sidecar key file plus the shipped application, decryption is usually possible
- ensure all diagnostics and config dumps redact encrypted auth blobs and never print the raw sidecar key material

## Environment Variable Naming for Services

Prefer predictable env vars that mirror flags:

- `TOOL_HOST` / `TOOL_TARGET`
- `TOOL_PROFILE`
- `TOOL_TOKEN` / `TOOL_API_KEY`

Document precedence: flags override env vars override config/profile defaults.

## Multiple Auth Surfaces

Some tools have more than one auth surface. Examples:

- daemon transport auth vs registry auth
- TLS client certificates vs account tokens
- connection-string (DSN) auth vs saved profiles

Rules:

- Name each auth surface explicitly (flags, env vars, config sections).
- Do not overload a single `--token` to mean different things depending on subcommand.
- Define how auth surfaces interact with contexts/profiles and defaults.

## Context / Profile / Account Vocabulary

Ecosystems use different names. The design must define:

- what a **target** is (host/origin/base URL/DSN/etc.)
- what a **profile/context** is (defaults + binding metadata)
- what a **credential binding** is (what key(s) secrets are bound to)
- what defaults exist (global vs per-target vs per-profile)
- uniqueness model (globally unique names vs unique-within-target)

## Protocol-Level Error Handling

Service CLIs should extend the generic error rules with protocol-aware recovery hints:

- Always include the effective target and relevant IDs.
- For missing/invalid credentials or unauthenticated sessions, print the recovery command (e.g. `tool auth login --target <...>`).
- For permission/authorization failures, explain that the current identity lacks access and suggest switching profile/account or requesting the missing permission.
- For not-found errors, echo back what was looked up so typos are obvious.
- For server-side failures, say it is server-side and suggest server logs.
- For transport failures (DNS, timeout, TLS, refused), name the target and suggest connectivity checks.

HTTP is one example:

- 401 usually means not authenticated / missing or invalid credentials (exit `3`) with login/token recovery guidance
- 403 often means authenticated but not authorized (exit `4`) with permission or profile/account-switch guidance, but if the service uses `403` for missing/invalid credentials, still classify it as `not authenticated` (exit `3`)
- 404 → not found (exit `5`)
- 409/412 → conflict/precondition (exit `6`)
- 429 → rate limited (exit `7`)
- network/timeouts → transport failure (exit `8`)

Non-HTTP examples:

- TLS handshake failure → transport failure (exit `8`)
- socket connection refused → transport failure (exit `8`)
- protocol reports missing/invalid credentials during handshake → not authenticated (exit `3`) with login/token recovery guidance
- protocol reports permission denial for an authenticated identity → not authorized (exit `4`) with permission or profile/account-switch guidance

## Protocol-Level Diagnostic Logging

In addition to [cli-patterns.md](cli-patterns.md), capture protocol exchange details in diagnostics:

- resolved target + profile/context + auth source (flag/env/config/secret-store)
- redacted credential hints (never log tokens/cookies/authorization headers)
- request/response metadata appropriate to the protocol

HTTP example:

- method, URL, headers (auth redacted), body (truncated)
- status, headers, body (truncated, e.g. 64 KB)

Non-HTTP example:

- endpoint, negotiated protocol/version, handshake failures
- TLS peer cert subject/issuer (no private key material)

## Distilled Patterns (Service)

- Resolve target/profile/auth once in shared runtime code instead of scattering through commands.
- Make inference layered, inspectable, and reversible:
  - resolve explicit inputs first (flags, env, explicit profile/context selection)
  - then apply config/profile defaults
  - then apply context inference (git remotes, directory markers, etc.) only when needed
  - if you support fallback env vars (not mirroring explicit flags), document where they sit relative to other inference sources
- Bind stored credentials to the chosen target identity key and refuse to silently reuse across mismatched targets.

## Local Cautions (Service)

- Do not store plaintext secrets in general config stores unless explicitly chosen and documented.
- Do not log raw `Authorization`, cookies, tokens, or token-bearing CLI args in diagnostics.
- Only prompt when the resolved output mode is human, `--quiet` is not set, and stdin and stderr are attached to a terminal; otherwise refuse (exit `2`). Machine output modes never prompt.
- Do not silently pick the first matching profile when multiple profiles match a target; require `--profile` or a default mapping.

## Service CLI Design Checklist Additions

In addition to the generic checklist in [cli-patterns.md](cli-patterns.md), service-like CLIs should pin down:

- auth commands and auth failure behavior
- target identity mode + normalization rules
- resolution order and ambiguity handling
- config-store vs secret-store model
- protocol-level diagnostics and exit-code mapping

Before shipping, validate at least:

- one auth help page
- one privileged command with auth failure recovery
- one secret flow (stdin or browser/device flow)
