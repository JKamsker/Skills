# Service CLI Patterns

Supplementary patterns for CLIs that connect to remote services or APIs. Read [cli-patterns.md](cli-patterns.md) first.

## Auth Design

Keep authentication explicit and contained.

- Put auth under `auth`.
  - Common commands: `login`, `logout`, `status`, `show`, `whoami`, `set-token`, `test`
- Prefer explicit auth modes over hidden fallback behavior.
  - Good: `tool auth login --device`
  - Avoid: running `tool deploy` and silently opening a login prompt
- Fail fast on missing auth for protected commands and print the exact recovery command.
  - Example: `Authentication required. Run 'tool auth login'.`
- Separate user auth from service auth when they differ.
  - Example: UI session cookie vs API token
- If the tool supports secrets from stdin, make that opt-in.
  - `--stdin`
  - `--password-stdin`

For browser-capable service CLIs:

- Prefer system browser plus PKCE or a service-native device or quick-connect flow.
- Fall back to pasted tokens only when the service actually uses them.

For API-token-based CLIs:

- Support `auth set-token TOKEN`
- Also support `auth set-token --stdin`
- Validate tokens by default unless the user passed `--no-validate`

## Self-Hosted Services: Host, Profiles, and Fallbacks

Self-hosted service CLIs need an explicit target model.

Recommended model:

- A profile stores non-secret defaults plus host binding.
- Credentials are bound to the canonical host key, not just a profile name.
- The active profile is only one input to target resolution, not magic global state.

Useful commands:

```text
tool auth profiles list
tool auth profiles use <name>
tool auth hosts list
tool auth hosts set-default <host> <profile>
tool config path
tool config get
tool config set
```

Resolution rules should be documented and enforced:

1. CLI flags
2. Environment variables
3. Config file or selected profile
4. Hardcoded defaults

If the host flag is set without `--profile`, pick a profile using a host-default mapping or a single matching profile. If multiple profiles match, require the user to choose instead of guessing. The flag name should reflect whether the value is always a hostname (`--host`) or can also be a full URL (`--server`).

If repo or directory inference exists, document it as a lower-priority fallback, not the primary contract.

Good examples from the local references:

- The [Jellyfin CLI profile system](../assets/design/jf-cli-profile-system.md) binds host defaults to profiles and only reuses stored auth when the profile host matches the target host. It is the worked example for the patterns below.
- Another local reference can infer host and repo from git remotes or an environment fallback, but that behavior stays visible in docs and errors.

### Hostname Normalization and Canonical Keys

Lowercase hostnames before using them as config keys. Strip port and path from the key; store those in `baseUrl`. Two URLs that share a hostname resolve to the same host entry; URL differences become per-profile overrides. Example: `https://myserver.com` and `https://myserver.com:8096/path` both key on `myserver.com`.

### Optional Hostname Aliases

Allow each host to declare short aliases (e.g. `home`, `nas`). Resolution order: exact host key match first, alias scan second. If multiple hosts share an alias:

- Use the first match (config file order) and emit a warning.
- The tie-break is deterministic so scripts behave predictably.

If an alias is identical to an existing host key, the host key always wins — the alias is effectively shadowed. Warn when this situation is created.

### Single-Entry Inference

One host configured? Use it. One profile on a host? Use it. This gives zero-config behavior for the common single-server, single-account case without adding hidden global state.

### Validation on Load and Write

Validate referential integrity on every config load: default pointers exist, every host has at least one profile, credential fields are consistent. Never write a config that violates these rules; cascade cleanup instead (e.g. deleting the last profile also deletes the host entry).

### Migration from Legacy Formats

If the old-format config exists and the new format does not, migrate silently on first run. Back up the old file (`.bak`). Print a one-line notice to stderr. The backup is never read again.

## Environment Variable Naming for Services

Service CLIs should use predictable env var names for host, token, and profile:

- `TOOL_HOST`
- `TOOL_TOKEN`
- `TOOL_PROFILE`

These map to `--host`/`--server`, `--token`, and `--profile` flags respectively. Document precedence: flags override env vars override config file.

## HTTP-Specific Error Handling

Service CLIs that talk to HTTP APIs should follow these additional error message rules:

- Include the target host and relevant IDs so the user knows which server and resource was involved.
- For auth errors (401, 403), always print the exact recovery command (`tool auth login --host <URL>`).
- For not-found errors (404), echo back what was looked up so the user can spot typos.
- For server errors (500+), tell the user the problem is on the server side, not a CLI bug.
- For network errors (DNS, timeout, connection refused), name the host and suggest checking connectivity.

Example error messages:

- "Failed to refresh library 'Movies'. Server returned 403 Forbidden. This may require admin privileges. Check your user policy."
- "Server returned 500 Internal Server Error. This is a server-side problem, not a CLI bug. Try again or check the server logs."

## HTTP Diagnostic Logging

In addition to the generic diagnostic logging in [cli-patterns.md](cli-patterns.md), service CLIs should capture HTTP exchange details:

- On every error, include in the diagnostic file:
  - Resolved host, profile, and auth source (flag, env, config).
  - The HTTP request: method, URL, headers (auth header redacted), and body (truncated if large).
  - The HTTP response: status code, headers, and body (truncated to a reasonable limit such as 64 KB).

### Verbosity levels for HTTP detail

| Level | What the user sees on stderr |
|---|---|
| Default | Error summary only: what failed, why, what now. |
| `--verbose` (`-v`) | Above plus: resolved host/profile/auth source, HTTP method and URL, response status code. |
| `-vv` | Above plus: request and response headers (auth redacted), response body (truncated). |
| `-vvv` | Above plus: full request body, full response body, timing, retry attempts. |

## Distilled Patterns (Service)

The bundled reference repos converge on service-specific patterns worth teaching directly.

- Resolve target, profile, and auth once in shared runtime code instead of scattering that logic through commands.
  - See [../assets/examples/csharp/spectre/runtime/TargetResolver.cs](../assets/examples/csharp/spectre/runtime/TargetResolver.cs) and [../assets/examples/rust/clap/profile_context.rs](../assets/examples/rust/clap/profile_context.rs).
- Make target inference layered, inspectable, and reversible.
  - A good model is explicit flags first, then git context, then environment fallback, with clear errors when nothing resolves. See [../assets/examples/rust/clap/target_resolution.rs](../assets/examples/rust/clap/target_resolution.rs).
- Bind stored credentials to canonical host keys and refuse to silently reuse them across mismatched targets.
  - The canonical sketch is [../assets/examples/rust/clap/profile_context.rs](../assets/examples/rust/clap/profile_context.rs).

## Local Cautions (Service)

Service CLIs have additional pitfalls beyond the generic cautions in [cli-patterns.md](cli-patterns.md):

- Do not store plaintext secrets in generic JSON stores unless the product explicitly requires that tradeoff.
- Do not log raw `Authorization`, cookie, or token-bearing command-line arguments in diagnostics.
- Do not prompt, spin, or wait for secret input unless stdin and stderr are attached to a terminal. `--quiet` alone is not a sufficient guard.
- Do not silently pick the first matching profile when multiple profiles map to the same host. Require an explicit `--profile` or a host-default mapping.

## Service CLI Design Checklist Additions

In addition to the generic checklist in [cli-patterns.md](cli-patterns.md), service CLIs should pin down before implementation:

- Auth commands and auth failure behavior
- Profile and host resolution
- Credential storage model and canonical host-key rules
- Target-resolution order, fallback heuristics, and any git or directory inference

Before shipping, also validate:

- one auth help page
- one privileged command with auth failure recovery
- one secret flow (token set via stdin or browser)
