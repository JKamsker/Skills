# Jellyfin CLI — Host-Specific Profile System

## Table of Contents

- [1. Overview](#1-overview)
- [2. Config File](#2-config-file)
- [3. Resolution](#3-resolution)
  - [3.1 Host Resolution](#31-host-resolution)
  - [3.2 Profile Resolution](#32-profile-resolution)
  - [3.3 Base URL Resolution](#33-base-url-resolution)
  - [3.4 Resolution Summary](#34-resolution-summary)
- [4. Hostname Extraction](#4-hostname-extraction)
- [5. CLI Commands](#5-cli-commands)
  - [5.1 Authentication](#51-authentication)
  - [5.2 Profile Management](#52-profile-management)
  - [5.3 Host Management](#53-host-management)
- [6. Global Flags](#6-global-flags)
- [7. Migration](#7-migration)
- [8. Validation Rules](#8-validation-rules)
- [9. Edge Cases](#9-edge-cases)
- [10. Environment Variable Summary](#10-environment-variable-summary)

---

## 1. Overview

> **Related documents:** For the broader CLI design (command tree, output modes, exit codes), see [`jf-cli-design.md`](jf-cli-design.md). For generic self-hosted service patterns (target identity modes, inference, migration, diagnostics), see [`../../references/service-cli-patterns.md`](../../references/service-cli-patterns.md).
 
The profile system allows the CLI to manage credentials for multiple Jellyfin servers and multiple accounts per server. Profiles are organized by hostname key (lowercased hostname; IP addresses supported), with a two-level resolution chain: first resolve the host, then resolve the profile within that host.
 
### Design Principles
 
- **Hostname-keyed**: Hosts are identified by their network hostname (or IP address) (e.g. `nas.local`, `jf.home.example.com`, `192.168.1.50`). This provides short, human-readable identifiers.
- **Base URL inheritance**: Each host declares a `baseUrl`. Profiles inherit it by default but may override it (e.g. different port or path on the same hostname).
- **Profile names are unique per host, not globally**. Two hosts may each have a profile named `admin`.
- **Secrets are stored separately**: Config stores non-secret metadata; tokens/API keys live in a separate secret store keyed by hostname key (lowercased hostname; IP addresses supported) + profile + credential kind.
- **Zero-config default**: A single server with a single profile requires no flags or env vars — it just works.
- **Optional hostname aliases**: Each host may declare short aliases (e.g. `home`, `nas`). Aliases are not globally unique — multiple hosts may share an alias — but the CLI warns on ambiguity.
 
---
 
## 2. Config File
 
### Location
 
| Platform | Path |
|----------|------|
| Windows  | `%APPDATA%/jf/config.json` |
| macOS    | `~/Library/Application Support/jf/config.json` |
| Linux    | `$XDG_CONFIG_HOME/jf/config.json` (default `~/.config/jf/config.json`) |
 
Overridable with `--config <PATH>` or `JF_CONFIG` env var.
 
### Schema

```jsonc
{
  // Hostname key (lowercased hostname; IP addresses supported) of the default server. Must be a key in "hosts".
  "defaultHost": "<host-key>",

  "hosts": {
    "<host-key>": {
      // Full URL used to connect to this server.
      // All profiles under this host inherit this unless they override it.
      "baseUrl": "<url>",
 
      // Optional short aliases for this host (e.g. ["home", "main-server"]).
      // Non-unique across hosts — multiple hosts may share an alias.
      "aliases": ["<alias>"],

      // Name of the default profile for this host.
      // Must be a key in this host's "profiles".
      "defaultProfile": "<profile-name>",
 
      "profiles": {
        "<profile-name>": {
          // Optional. Overrides the host-level baseUrl for this profile only.
          "baseUrl": "<url>",

          // Non-secret profile metadata (used for display/debugging only).
          "user": "<string>",
          "userId": "<guid>",

          // Non-secret declaration of which credential kind is stored for this profile.
          // The credential itself lives in the secret store.
          "authKind": "token" // or "apiKey"
        }
      }
    }
  }
}
```
 
### Secret Store (preferred)

Tokens and API keys are stored separately from `config.json` (OS credential store / keyring / external helper).

Recommended keying model:

- `jf:cred:{hostKey}:{profile}:token`
- `jf:cred:{hostKey}:{profile}:apiKey`

Where `{hostKey}` is the **hostname key** (lowercased hostname; IP addresses supported) and `{profile}` is the profile name within that host.

Fallback (allowed but discouraged): a separate secrets file (e.g. `secrets.json`) with strict permissions and explicit redaction rules. If this fallback is used, it must be clearly labeled as a tradeoff in docs and help.

Legacy note: `credentials.json` is treated as a legacy format to migrate away from (§7), not as a preferred ongoing secret store.

### Example
 
```json
{
  "defaultHost": "jf.home.example.com",
  "hosts": {
    "jf.home.example.com": {
      "baseUrl": "https://jf.home.example.com",
      "aliases": ["home", "jf"],
      "defaultProfile": "main",
      "profiles": {
        "main": {
          "user": "jonas",
          "userId": "f692a3c1-0498-4a6e-b596-1a45cb918037",
          "authKind": "token"
        },
        "admin": {
          "user": "admin",
          "authKind": "apiKey"
        }
      }
    },
    "nas.local": {
      "baseUrl": "http://nas.local:8096/jellyfin",
      "aliases": ["nas"],
      "defaultProfile": "admin",
      "profiles": {
        "admin": {
          "user": "admin",
          "authKind": "token"
        },
        "legacy": {
          "baseUrl": "https://nas.local:8920",
          "user": "admin",
          "authKind": "token"
        }
      }
    }
  }
}
```
 
### Field Reference
 
| Field | Required | Description |
|-------|----------|-------------|
| `defaultHost` | No | Hostname key of the default server. If absent and multiple hosts exist, requires explicit `--server` or `JF_SERVER` (single-host inference still applies). |
| `hosts` | Yes | Map of hostname key (lowercased hostname; IP addresses supported) → host object. |
| `hosts[].baseUrl` | Yes | Default base URL for all profiles under this host. |
| `hosts[].aliases` | No | List of short aliases for this host. Non-unique across hosts. Used during host lookup. |
| `hosts[].defaultProfile` | No | Default profile name for this host. If absent: inferred if there is exactly one profile, otherwise error. |
| `hosts[].profiles` | Yes | Map of profile name → profile object. At least one required. |
| `profiles[].baseUrl` | No | Overrides the host-level `baseUrl` for this profile. |
| `profiles[].user` | No | Username for display/debugging only (non-secret). |
| `profiles[].userId` | No | Jellyfin user ID for display/debugging only (non-secret). |
| `profiles[].authKind` | Yes | Declares which credential kind is stored for this profile in the secret store (`token` or `apiKey`). |
 
---
 
## 3. Resolution
 
Resolution is a two-step process: resolve the host, then resolve the profile within it.
 
### 3.1 Host Resolution
 
Evaluated in order, first match wins:
 
| Priority | Source | Behavior |
|----------|--------|----------|
| 1 | `--server <VALUE>` flag | If a full URL (or scheme-less `host:port`): extract hostname for lookup, use the URL as runtime `baseUrl` override (see §3.3). If a bare hostname (or IP address) or alias: see lookup order below. |
| 2 | `JF_SERVER` env var | Same rules as `--server`. |
| 3 | `defaultHost` in config | Used as-is. |
| 4 | Single host | If `hosts` contains exactly one entry, use it implicitly. |
| 5 | *(none)* | Error: `No server specified. Use --server or set a default host with: jf auth host use <host-key>` |

**CI/automation escape hatch:** If `JF_TOKEN` is set and the selected server input is a **full URL (or scheme-less `host:port`)**, the CLI may allow an ephemeral, config-less context:

- If no configured host/alias matches the extracted hostname, treat it as an implicit host for this invocation.
- Derive `hostKey` from the URL hostname and use the URL as the effective base URL.
- Select `profileName` from `--profile`/`JF_PROFILE`, or default to `"default"`.
- Do not read or write config and do not touch the secret store.

**Lookup order for a bare hostname (or IP address) or alias value:**

1. Exact match against `hosts` keys (hostname key). If found, use it — alias scan is skipped entirely, even if another host has a matching alias.
2. Alias match: scan all hosts for one whose `aliases` array contains the value.
   - If exactly one host matches: use it.
   - If multiple hosts match: use the first match (config file order) and emit a warning:
     - Human mode: warning is printed to stderr.
     - `--json` mode: warning is included in `meta.warnings` in the JSON envelope (no stderr noise).
     ```
     Warning: alias "home" is defined on multiple hosts: jf.home.example.com, backup.example.com
     Using jf.home.example.com. To suppress, make aliases unique or use the full hostname.
     ```
3. No match: error.
 
### 3.2 Profile Resolution
 
Given a resolved host, evaluated in order:
 
| Priority | Source | Behavior |
|----------|--------|----------|
| 1 | `--profile <NAME>` flag | For most commands: must exist under the resolved host. For `jf auth login`: may create the profile if it does not exist yet. |
| 2 | `JF_PROFILE` env var | For most commands: must exist under the resolved host. For `jf auth login`: may create the profile if it does not exist yet. |
| 3 | `defaultProfile` for the resolved host | Used as-is. |
| 4 | Single profile | If the resolved host has exactly one profile, use it implicitly. |
| 5 | *(none)* | Error: `No profile specified for host "<host-key>". Use --profile or set a default with: jf auth profiles use <name>` |
 
### 3.3 Base URL Resolution
 
Given a resolved host and profile:
 
| Priority | Source | Description |
|----------|--------|-------------|
| 1 | `--server` full URL (or scheme-less `host:port`) / `JF_SERVER` full URL (or scheme-less `host:port`) | Used for this invocation only. Not persisted (except `jf auth login`, which writes/updates config). |
| 2 | Profile-level `baseUrl` | Override for this specific profile. |
| 3 | Host-level `baseUrl` | Default for all profiles under this host. |
 
The final resolved base URL is used for all API requests in that invocation.
 
### 3.4 Resolution Summary
 
```
--server / JF_SERVER ──→ extract hostname ──→ exact hostname key match
                                                   │ (no match)
                                                   ▼
                                            alias scan across hosts
                                           ├── 1 match  ──→ use it
                                           ├── 2+ match ──→ warn, use first
                                           └── 0 match  ──→ error
       ┌──────────────────────────────────────────┘
       ▼
  --profile / JF_PROFILE ──→ profile lookup within host
                                      │
       ┌──────────────────────────────┘
       ▼
  --server full URL (or scheme-less host:port) > profile.baseUrl > host.baseUrl ──→ final base URL
```
 
---
 
## 4. Hostname Extraction
 
When `--server` or `JF_SERVER` provides a full URL, the hostname is extracted for the config lookup key.
 
| Input | Extracted Hostname |
|-------|--------------------|
| `https://jf.home.example.com` | `jf.home.example.com` |
| `http://nas.local:8096/jellyfin` | `nas.local` |
| `http://192.168.1.50:8096` | `192.168.1.50` |
| `nas.local` (bare) | `nas.local` |
 
Extraction uses standard URL parsing: `new URL(input).hostname` (or equivalent). If parsing fails because the input has no scheme, treat the input as a bare hostname (or IP address) (and if it looks like `host:port`, first add a default scheme before parsing).

Port and path are **not** part of the hostname key. They are preserved only in `baseUrl`.

**Note:** Scheme-less `host:port` inputs (e.g. `nas.local:8096`) are treated as a full URL by first adding a default scheme (e.g. `http://nas.local:8096`) before extracting the hostname. This keeps the hostname key host-only while still allowing ports in the runtime base URL.

### Exact normalization rules (hostname key)

The hostname key is derived with these rules:

- Trim whitespace.
- If the input looks like a URL, parse it and extract `.hostname`.
- Otherwise treat the input as a bare hostname (or IP address).
- Lowercase the hostname for the identity key.
- Do not include scheme, port, path, query, or fragment in the identity key.

The effective base URL used for network calls is derived separately:

- Preserve scheme/port/path when the user provided a full URL.
- Drop query/fragment.
- Trim a trailing `/`.

### Pseudocode (resolution skeleton)

```text
resolve(serverArg, profileArg):
  hostInput = serverArg ?? env.JF_SERVER ?? config.defaultHost ?? singleHostOrError()

  // Host lookup uses the hostname key (lowercased hostname; IP addresses supported).
  // If the input is scheme-less host:port, treat it as URL-like by adding a default scheme first.
  urlInput = withDefaultSchemeIfNeeded(hostInput)
  hostKey =
    if looksLikeUrlInput(urlInput) then lower(parseUrl(urlInput).hostname)
    else lower(hostInput)

  // CI/automation escape hatch: allow an ephemeral context with env token + full URL.
  // This bypasses config and the secret store and does not create or modify profiles.
  if env.JF_TOKEN is set and looksLikeUrlInput(urlInput) and (config.hosts[hostKey] is missing) and (resolveAlias(hostKey) is missing):
    profileName = profileArg ?? env.JF_PROFILE ?? "default"
    effectiveBaseUrl = normalizeBaseUrl(urlInput)
    return (hostKey, profileName, effectiveBaseUrl, env.JF_TOKEN)

  host = config.hosts[hostKey] ?? resolveAlias(hostKey) ?? error()

  profileName = profileArg ?? env.JF_PROFILE ?? host.defaultProfile ?? singleProfileOrError()
  profile = host.profiles[profileName] ?? error()

  effectiveBaseUrl =
    if looksLikeUrlInput(urlInput) then normalizeBaseUrl(urlInput) // invocation-only override
    else normalizeBaseUrl(profile.baseUrl ?? host.baseUrl)

  credentialKind = profile.authKind
  secret =
    if env.JF_TOKEN is set then env.JF_TOKEN
    else secretStore.get("jf:cred:{hostKey}:{profile}:{credentialKind}") ?? missingAuthError()

  return (hostKey, profileName, effectiveBaseUrl, secret)
```
 
---
 
## 5. CLI Commands
 
### 5.1 Authentication

This section documents only the auth commands that create/use profiles and stored credentials (`login`/`logout`). For other auth commands (e.g. `status`, `whoami`, `set-token`, `test`, `api-keys`), see [`jf-cli-design.md`](jf-cli-design.md).

#### `jf auth login`

Interactive login to a Jellyfin server. Creates or updates a host entry and profile.

```
jf auth login [--server <VALUE>] [--profile <NAME>] [--username <USER>] [--password-stdin] [--quick-connect]
```

| Flag | Required | Description |
|------|----------|-------------|
| `--server` | Only if a server cannot be resolved | Server URL (or scheme-less `host:port`) when creating a new host entry. For existing hosts, this flag may also be a bare hostname (or IP address) or alias (uses stored `baseUrl` unless a full URL is supplied for login; if it differs, it may be saved as a profile-level `baseUrl` override). |
| `--profile` | No | Profile name. Default: for existing hosts, follows standard profile resolution (§3.2). When creating a new host entry, prompts in human mode when a TTY is present; otherwise uses `"default"` (including in machine output modes). |
| `--username` | No | Username for password-based login. If absent, prompts in human mode when TTY is present. Required in `--json` mode (no prompts). |
| `--password-stdin` | No | Read password from stdin (non-interactive). Required in `--json` mode for password-based login (no prompts). |
| `--quick-connect` | No | Use the Quick Connect device-flow-style login (interactive). Refuses in machine output modes. |

**Flow:**

1. Resolve the server input using standard host resolution (§3.1). If a full URL (or scheme-less `host:port`) was provided, extract the hostname for the hostname key.
2. Perform the selected auth flow (password-based or quick-connect) and obtain an access token (see `jf-cli-design.md` for the detailed interaction contract).
3. Create or update `hosts[hostKey]`:
   - Set `baseUrl` on the host if this is a new host entry.
   - Create/update `profiles[name]` with non-secret metadata (`authKind`, `user`, optional `userId`).
   - Store the credential in the secret store under `jf:cred:{hostKey}:{profile}:{credentialKind}` (where `credentialKind` is `profile.authKind`).
   - If this is the only host, set as `defaultHost`.
   - If this is the only profile on the host, set as `defaultProfile`.
4. Write config.

**Profile `baseUrl` override:** If the host already exists and the login URL differs from the host's `baseUrl`, store the login URL as a profile-level `baseUrl` override.

#### `jf auth logout`

Best-effort revoke and remove the stored credential for a profile.

```
jf auth logout [--server <VALUE>] [--profile <NAME>]
```

Resolves host and profile via standard resolution (§3). Removes the stored credential from the secret store. If the profile uses token auth and the server supports revocation, performs a best-effort server-side revoke. For API keys, use `jf auth api-keys delete` to revoke server-side.

This command does not remove the profile metadata from config. Use `jf auth profiles delete <name>` to remove a profile entirely.
 
### 5.2 Profile Management
 
#### `jf auth profiles list`
 
List all hosts and their profiles.
 
```
jf auth profiles list [--server <VALUE>]
```
 
Without `--server`: lists all hosts and all profiles. With `--server`: lists profiles for that host only.
 
**Output format:**
 
```
jf.home.example.com (default host)
  baseUrl: https://jf.home.example.com
  * main (default) — jonas
    admin — API key
 
nas.local
  baseUrl: http://nas.local:8096/jellyfin
  * admin (default) — admin
    legacy — admin (baseUrl: https://nas.local:8920)
```
 
- `*` marks the default profile.
- `(default host)` marks the global default host.
- Profile-level `baseUrl` shown only if it overrides the host.
 
#### `jf auth profiles show`
  
Show profile details.
  
```
jf auth profiles show [<name>] [--server <VALUE>]
```
  
Without `<name>`, resolves the host and profile via standard resolution and prints the effective host, profile name, base URL, username, and auth method. Useful for debugging which profile a command would use.

With `<name>`, resolves the host via standard resolution and then prints the named profile under that host (errors if it does not exist).
 
**Output format:**
 
```
Host:     nas.local
Profile:  admin
Base URL: http://nas.local:8096/jellyfin
Username: admin
Auth:     token (stored in secret store)
```

If `JF_TOKEN` is set, show it as an override source (do not print the token itself):

```text
Auth:     token (from JF_TOKEN override)
```
 
#### `jf auth profiles use <name>`
 
Set the default profile for a host.
 
```
jf auth profiles use <name> [--server <VALUE>]
```
 
Resolves the host via standard resolution (§3.1). Sets `defaultProfile` for that host to `<name>`. Errors if the profile does not exist on that host.
 
#### `jf auth profiles rename <old> <new>`
 
Rename a profile.
 
```
jf auth profiles rename <old> <new> [--server <VALUE>]
```
 
Resolves the host. Renames the profile key. Updates `defaultProfile` if it pointed to the old name. Re-keys any stored credentials from `jf:cred:{hostKey}:{old}:{credentialKind}` to `jf:cred:{hostKey}:{new}:{credentialKind}`. Errors if `<new>` already exists on that host.
 
#### `jf auth profiles delete <name>`
 
Remove a profile without revoking the token server-side.
 
```
jf auth profiles delete <name> [--server <VALUE>]
```
 
Resolves the host. Removes the profile. Deletes any stored credentials keyed under `jf:cred:{hostKey}:{name}:{credentialKind}`. If it was `defaultProfile`, clears `defaultProfile` (next resolution will require explicit selection or fall through to single-profile inference). If it was the last profile, removes the host entry.
 
### 5.3 Host Management
 
#### `jf auth host list`
 
List all configured hosts and their base URLs.
 
```
jf auth host list
```
 
**Output format:**
 
```
* jf.home.example.com  https://jf.home.example.com       (2 profiles)  [home, jf]
  nas.local             http://nas.local:8096/jellyfin     (2 profiles)  [nas]
```
 
Aliases column is omitted if no host has any aliases.
 
#### `jf auth host use <host-key>`
 
Set the global default host.
 
```
jf auth host use <host-key>
```

Sets `defaultHost` in config. Errors if the host key is not in `hosts`.

#### `jf auth host rename <old-host-key> <new-host-key>`

Rename a host key.

```
jf auth host rename <old-host-key> <new-host-key>
```

Renames the key in `hosts`. Updates `defaultHost` if it pointed to the old name. Re-keys any stored credentials from `jf:cred:{oldHostKey}:{profile}:{credentialKind}` to `jf:cred:{newHostKey}:{profile}:{credentialKind}`. Does not change any `baseUrl` values.
 
#### `jf auth host delete <host-key>`
 
Remove a host and all its profiles.
 
```
jf auth host delete <host-key> [--yes] [--dry-run]
```
 
Removes the host entry and all profiles within it. Follows the global confirmation rules:

- without `--yes`, prompts for confirmation (TTY-only, human mode only)
- with `--quiet` or without a TTY, refuses with exit `2` unless `--yes` or `--dry-run` is provided
- in `--json`, never prompts; refuses with exit `2` unless `--yes` or `--dry-run` is provided

Clears `defaultHost` if it pointed to this host.
 
#### `jf auth host alias add <host-key> <alias>`
 
Add an alias for an existing host.
 
```
jf auth host alias add <host-key> <alias>
```
 
Appends `<alias>` to the host's `aliases` list. If the alias already exists on another host, a warning is emitted:

```
Warning: alias "home" is already used by jf.home.example.com
```

The alias is still added — duplicates are allowed but discouraged.

Routing:

- Human mode: warning is printed to stderr.
- `--json` mode: warning is included in `meta.warnings` in the JSON envelope (no stderr noise).

Example (`--json`):

```json
{
  "ok": true,
  "data": { "aliasAdded": true },
  "error": null,
  "meta": {
    "schemaVersion": 1,
    "warnings": [
      { "code": "alias_duplicate", "message": "alias \"home\" is also set on jf.home.example.com" }
    ]
  }
}
```

If `<alias>` matches an existing hostname key (e.g. adding alias `nas.local` to some other host), an additional warning is emitted:

```
Warning: alias "nas.local" matches an existing hostname key and will always be shadowed by it.
```

The alias is stored but will never be reachable via that value as long as the hostname key exists.
 
#### `jf auth host alias remove <host-key> <alias>`
 
Remove an alias from a host.
 
```
jf auth host alias remove <host-key> <alias>
```
 
Removes `<alias>` from the host's `aliases` list. Errors if the alias is not set on that host.
 
#### `jf auth host alias list [<host-key>]`
 
List aliases.
 
```
jf auth host alias list [<host-key>]
```
 
Without `<host-key>`: lists all hosts with their aliases. With `<host-key>`: lists aliases for that host only. Flags duplicate aliases across hosts:
 
```
jf.home.example.com  [home, jf]
nas.local             [nas, home]
```

If duplicates exist, a warning is emitted:

```
Warning: alias "home" is also set on jf.home.example.com
```

Routing:

- Human mode: warning is printed to stderr.
- `--json` mode: warning is included in `meta.warnings` in the JSON envelope (no stderr noise).
 
---
 
## 6. Global Flags
 
These flags are available on all commands, not just `auth`:
 
| Flag | Env Var | Description |
|------|---------|-------------|
| `--server <VALUE>` | `JF_SERVER` | Hostname (or IP address), alias, scheme-less `host:port`, or full URL to select/override the target server. |
| `--profile <NAME>` | `JF_PROFILE` | Profile name to use on the resolved host. |
| `--config <PATH>` | `JF_CONFIG` | Path to config file (overrides default location). |
| `--json` | `JF_OUTPUT` | Output the versioned JSON envelope contract to stdout (non-interactive). |
| `--quiet` | | Suppress non-essential human-facing output and prompts; if a confirmation prompt would be required, refuse with exit `2` unless `--yes` or `--dry-run` is provided. Does not suppress primary command output such as a documented dry-run preview or JSON envelope. |
| `--dry-run` | | Preview a mutating operation without mutating. |
| `--yes` | | Skip confirmation prompts for destructive actions. |
| `--no-color` | `NO_COLOR` | Disable ANSI formatting. |
| `--verbose` | | Increase diagnostic detail. |
| `--help` | | Show help. |
| `--version` | | Show version. |
 
---
 
## 7. Migration
 
### From `credentials.json`
 
If `config.json` does not exist but a legacy `credentials.json` is found in the same directory, the CLI performs an automatic one-time migration on first run:
 
1. Read `credentials.json`.
2. Extract the server URL and credentials.
3. Parse the hostname from the server URL.
4. Create `config.json` with a single host and a single profile named `"default"`.
5. Set that host as `defaultHost` and `"default"` as `defaultProfile`.
6. Set `profiles["default"].authKind` to the migrated credential kind and store the credential in the secret store under `jf:cred:{hostKey}:default:{credentialKind}`.
7. Rename `credentials.json` to `credentials.json.bak`.
8. In human mode, print a one-line note to stderr: `Migrated credentials to new profile format. Backup: credentials.json.bak` (in `--json`, include as a `meta.warnings` item; no stderr noise).
 
No data is lost. The backup file is never read by the CLI again.
 
---
 
## 8. Validation Rules
 
### On Config Load
 
- `defaultHost`, if set, must be a key in `hosts`.
- `defaultProfile`, if set on a host, must be a key in that host's `profiles`.
- Every host must have a non-empty `baseUrl`.
- Every host must have at least one profile.
- Each profile must have a valid `authKind` (`token` or `apiKey`).
 
Invalid config produces a clear error message pointing to the specific issue, e.g.:
 
```
Config error: hosts["nas.local"].defaultProfile "admin" does not exist.
Available profiles: legacy, backup
```
 
### On Config Write
 
The CLI must never write a config that violates the above rules. Commands that would cause a violation (e.g. deleting the last profile without deleting the host) must handle the cascading cleanup.
 
---
 
## 9. Edge Cases
 
### Duplicate hostnames from different URLs
 
`https://myserver.com` and `http://myserver.com:8096/jellyfin` both resolve to hostname `myserver.com`. They share a host entry. The first login sets the host-level `baseUrl`. A subsequent login with a different URL stores the difference as a profile-level `baseUrl` override.
 
### IP addresses as hostname keys
 
IP addresses (e.g. `192.168.1.50`) are valid hostname keys. They follow all the same rules.
 
### Hostname normalization
 
Hostnames are lowercased before use as config keys. `NAS.local` and `nas.local` resolve to the same entry.
 
### Token expiry
 
Token expiry detection is outside the scope of this spec. The CLI should handle HTTP 401 responses gracefully and require re-authentication (exit `3` with an actionable recovery command), but it must never start an interactive login flow from non-auth commands and must never prompt in `--json` mode.
 
### Alias shadowed by hostname key
 
If an alias value is identical to an existing hostname key, the hostname key always wins — the alias scan is never reached. The alias is effectively unreachable while that hostname key exists. This is intentional: hostname keys are authoritative identifiers; aliases are convenience shortcuts.
 
`jf auth host alias add` warns when this situation is created (see §5.3).
 
### Duplicate aliases
 
Multiple hosts may share an alias. When a lookup matches more than one host:
 
- The first matching host in config file order is used.
- A warning is printed to stderr (human mode):
  ```
  Warning: alias "home" is defined on multiple hosts: jf.home.example.com, backup.example.com
  Using jf.home.example.com. To suppress, make aliases unique or use the full hostname.
  ```
  In `--json` mode, the warning is included in `meta.warnings` in the JSON envelope (no stderr noise).
 
This is by design — the user is informed rather than blocked, and the tie-break is deterministic.
 
### Concurrent config writes
 
The CLI uses atomic file writes (write to temp file, then rename) to prevent corruption from concurrent processes. No file locking is implemented; last write wins.
 
---
 
## 10. Environment Variable Summary
 
| Variable | Description | Equivalent Flag |
|----------|-------------|-----------------|
| `JF_SERVER` | Default server hostname (or IP address), alias, scheme-less `host:port`, or URL | `--server` |
| `JF_PROFILE` | Default profile name | `--profile` |
| `JF_CONFIG` | Config file path | `--config` |
| `JF_TOKEN` | Access token override (bypasses secret store) | *(auth override)* |
| `JF_OUTPUT` | Set to `json` for machine output | `--json` |
| `NO_COLOR` | Disable ANSI formatting (standard) | `--no-color` |
