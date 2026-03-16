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

> **Related documents:** For the broader CLI design (command tree, output modes, exit codes), see [`jf-cli-design.md`](jf-cli-design.md). For generic self-hosted service patterns (hostname normalization, single-entry inference, migration), see [`../../references/service-cli-patterns.md`](../../references/service-cli-patterns.md).
 
The profile system allows the CLI to manage credentials for multiple Jellyfin servers and multiple accounts per server. Profiles are organized by hostname, with a two-level resolution chain: first resolve the host, then resolve the profile within that host.
 
### Design Principles
 
- **Hostname-keyed**: Hosts are identified by their network hostname (e.g. `nas.local`, `jf.home.example.com`). This provides short, human-readable identifiers.
- **Base URL inheritance**: Each host declares a `baseUrl`. Profiles inherit it by default but may override it (e.g. different port or path on the same hostname).
- **Profile names are unique per host, not globally**. Two hosts may each have a profile named `admin`.
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
 
Overridable with `--config <path>` or `JF_CONFIG` env var.
 
### Schema
 
```jsonc
{
  // Hostname of the default server. Must be a key in "hosts".
  "defaultHost": "<hostname>",
 
  "hosts": {
    "<hostname>": {
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
 
          // Authentication — one of the following combinations:
          "token": "<access-token>",
          "username": "<string>",
          "userId": "<guid>",
 
          // OR api key auth:
          "apiKey": "<api-key>"
        }
      }
    }
  }
}
```
 
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
          "token": "eyJhbGciOi...",
          "username": "jonas",
          "userId": "f692a3c1-0498-4a6e-b596-1a45cb918037"
        },
        "admin": {
          "apiKey": "abc123def456"
        }
      }
    },
    "nas.local": {
      "baseUrl": "https://nas.local:8096/jellyfin",
      "aliases": ["nas"],
      "defaultProfile": "admin",
      "profiles": {
        "admin": {
          "token": "xyz789...",
          "username": "admin"
        },
        "legacy": {
          "baseUrl": "http://nas.local:8920",
          "token": "old123...",
          "username": "admin"
        }
      }
    }
  }
}
```
 
### Field Reference
 
| Field | Required | Description |
|-------|----------|-------------|
| `defaultHost` | No | Hostname of the default server. If absent, requires explicit `--server` or `JF_SERVER`. |
| `hosts` | Yes | Map of hostname → host object. |
| `hosts[].baseUrl` | Yes | Default base URL for all profiles under this host. |
| `hosts[].aliases` | No | List of short aliases for this host. Non-unique across hosts. Used during host lookup. |
| `hosts[].defaultProfile` | No | Default profile name for this host. If absent: inferred if there is exactly one profile, otherwise error. |
| `hosts[].profiles` | Yes | Map of profile name → profile object. At least one required. |
| `profiles[].baseUrl` | No | Overrides the host-level `baseUrl` for this profile. |
| `profiles[].token` | No | Jellyfin access token (from authentication). |
| `profiles[].username` | No | Username associated with the token. |
| `profiles[].userId` | No | Jellyfin user ID associated with the token. |
| `profiles[].apiKey` | No | Jellyfin API key (alternative to token auth). |
 
---
 
## 3. Resolution
 
Resolution is a two-step process: resolve the host, then resolve the profile within it.
 
### 3.1 Host Resolution
 
Evaluated in order, first match wins:
 
| Priority | Source | Behavior |
|----------|--------|----------|
| 1 | `--server <value>` flag | If a full URL: extract hostname for lookup, use the URL as runtime `baseUrl` override (see §3.3). If a bare hostname or alias: see lookup order below. |
| 2 | `JF_SERVER` env var | Same rules as `--server`. |
| 3 | `defaultHost` in config | Used as-is. |
| 4 | Single host | If `hosts` contains exactly one entry, use it implicitly. |
| 5 | *(none)* | Error: `No server specified. Use --server or set a default host with: jf auth host use <hostname>` |

**Lookup order for a bare hostname/alias value:**

1. Exact match against `hosts` keys (hostname). If found, use it — alias scan is skipped entirely, even if another host has a matching alias.
2. Alias match: scan all hosts for one whose `aliases` array contains the value.
   - If exactly one host matches: use it.
   - If multiple hosts match: use the first match (config file order) and emit a warning:
     ```
     Warning: alias "home" is defined on multiple hosts: jf.home.example.com, backup.example.com
     Using jf.home.example.com. To suppress, make aliases unique or use the full hostname.
     ```
3. No match: error.
 
### 3.2 Profile Resolution
 
Given a resolved host, evaluated in order:
 
| Priority | Source | Behavior |
|----------|--------|----------|
| 1 | `--profile <name>` flag | Must exist under the resolved host. Error if not found. |
| 2 | `JF_PROFILE` env var | Must exist under the resolved host. Error if not found. |
| 3 | `defaultProfile` for the resolved host | Used as-is. |
| 4 | Single profile | If the resolved host has exactly one profile, use it implicitly. |
| 5 | *(none)* | Error: `No profile specified for host "<hostname>". Use --profile or set a default with: jf auth profiles use <name>` |
 
### 3.3 Base URL Resolution
 
Given a resolved host and profile:
 
| Priority | Source | Description |
|----------|--------|-------------|
| 1 | `--server` full URL | Used for this invocation only. Not persisted. |
| 2 | Profile-level `baseUrl` | Override for this specific profile. |
| 3 | Host-level `baseUrl` | Default for all profiles under this host. |
 
The final resolved base URL is used for all API requests in that invocation.
 
### 3.4 Resolution Summary
 
```
--server / JF_SERVER ──→ extract hostname ──→ exact host key match
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
  --server full URL > profile.baseUrl > host.baseUrl ──→ final base URL
```
 
---
 
## 4. Hostname Extraction
 
When `--server` or `JF_SERVER` provides a full URL, the hostname is extracted for the config lookup key.
 
| Input | Extracted Hostname |
|-------|--------------------|
| `https://jf.home.example.com` | `jf.home.example.com` |
| `https://nas.local:8096/jellyfin` | `nas.local` |
| `http://192.168.1.50:8096` | `192.168.1.50` |
| `nas.local` (bare) | `nas.local` |
 
Extraction uses standard URL parsing: `new URL(input).hostname` (or equivalent). If parsing fails (no scheme), treat the input as a bare hostname.
 
Port and path are **not** part of the hostname key. They are preserved only in `baseUrl`.
 
---
 
## 5. CLI Commands
 
### 5.1 Authentication
 
#### `jf auth login`
 
Interactive login to a Jellyfin server. Creates or updates a host entry and profile.
 
```
jf auth login --server <url> [--profile <name>] [--api-key <key>]
```
 
| Flag | Required | Description |
|------|----------|-------------|
| `--server` | Yes (for first login) | Full server URL. Hostname extracted as config key, URL stored as `baseUrl`. |
| `--profile` | No | Profile name. Default: prompts interactively, or `"default"` in non-interactive mode. |
| `--api-key` | No | Use API key auth instead of username/password. |
 
**Flow:**
 
1. Extract hostname from `--server` URL.
2. POST to `<baseUrl>/Users/AuthenticateByName` (or store API key directly).
3. Create or update `hosts[hostname]`:
   - Set `baseUrl` on the host if this is a new host entry.
   - Create `profiles[name]` with credentials.
   - If this is the only host, set as `defaultHost`.
   - If this is the only profile on the host, set as `defaultProfile`.
4. Write config.
 
**Profile `baseUrl` override:** If the host already exists and the login URL differs from the host's `baseUrl`, store the login URL as a profile-level `baseUrl` override.
 
#### `jf auth logout`
 
Revoke token and remove a profile.
 
```
jf auth logout [--server <host>] [--profile <name>]
```
 
Resolves host and profile via standard resolution (§3). Revokes the token server-side if possible, then removes the profile from config. If it was the last profile on the host, removes the host entry. If the host was `defaultHost` and is removed, clears `defaultHost`.
 
### 5.2 Profile Management
 
#### `jf auth profiles list`
 
List all hosts and their profiles.
 
```
jf auth profiles list [--server <host>]
```
 
Without `--server`: lists all hosts and all profiles. With `--server`: lists profiles for that host only.
 
**Output format:**
 
```
jf.home.example.com (default host)
  baseUrl: https://jf.home.example.com
  * main (default) — jonas
    admin — API key
 
nas.local
  baseUrl: https://nas.local:8096/jellyfin
  * admin (default) — admin
    legacy — admin (baseUrl: http://nas.local:8920)
```
 
- `*` marks the default profile.
- `(default host)` marks the global default host.
- Profile-level `baseUrl` shown only if it overrides the host.
 
#### `jf auth profiles show`
 
Show the fully resolved profile for the current context.
 
```
jf auth profiles show [--server <host>] [--profile <name>]
```
 
Resolves using standard resolution and prints the effective host, profile name, base URL, username, and auth method. Useful for debugging which profile a command would use.
 
**Output format:**
 
```
Host:     nas.local
Profile:  admin
Base URL: https://nas.local:8096/jellyfin
Username: admin
Auth:     token
```
 
#### `jf auth profiles use <name>`
 
Set the default profile for a host.
 
```
jf auth profiles use <name> [--server <host>]
```
 
Resolves the host via standard resolution (§3.1). Sets `defaultProfile` for that host to `<name>`. Errors if the profile does not exist on that host.
 
#### `jf auth profiles rename <old> <new>`
 
Rename a profile.
 
```
jf auth profiles rename <old> <new> [--server <host>]
```
 
Resolves the host. Renames the profile key. Updates `defaultProfile` if it pointed to the old name. Errors if `<new>` already exists on that host.
 
#### `jf auth profiles delete <name>`
 
Remove a profile without revoking the token server-side.
 
```
jf auth profiles delete <name> [--server <host>]
```
 
Resolves the host. Removes the profile. If it was `defaultProfile`, clears `defaultProfile` (next resolution will require explicit selection or fall through to single-profile inference). If it was the last profile, removes the host entry.
 
### 5.3 Host Management
 
#### `jf auth host list`
 
List all configured hostnames and their base URLs.
 
```
jf auth host list
```
 
**Output format:**
 
```
* jf.home.example.com  https://jf.home.example.com       (2 profiles)  [home, jf]
  nas.local             https://nas.local:8096/jellyfin    (2 profiles)  [nas]
```
 
Aliases column is omitted if no host has any aliases.
 
#### `jf auth host use <hostname>`
 
Set the global default host.
 
```
jf auth host use <hostname>
```
 
Sets `defaultHost` in config. Errors if the hostname is not in `hosts`.
 
#### `jf auth host rename <old> <new>`
 
Rename a host key.
 
```
jf auth host rename <old-hostname> <new-hostname>
```
 
Renames the key in `hosts`. Updates `defaultHost` if it pointed to the old name. Does not change any `baseUrl` values.
 
#### `jf auth host delete <hostname>`
 
Remove a host and all its profiles.
 
```
jf auth host delete <hostname> [--force]
```
 
Removes the host entry and all profiles within it. Without `--force`, prompts for confirmation if the host has more than one profile. Clears `defaultHost` if it pointed to this host.
 
#### `jf auth host alias add <hostname> <alias>`
 
Add an alias for an existing host.
 
```
jf auth host alias add <hostname> <alias>
```
 
Appends `<alias>` to the host's `aliases` list. If the alias already exists on another host, a warning is emitted:
 
```
Warning: alias "home" is already used by jf.home.example.com
```
 
The alias is still added — duplicates are allowed but discouraged.

If `<alias>` matches an existing host key (e.g. adding alias `nas.local` to some other host), an additional warning is emitted:

```
Warning: alias "nas.local" matches an existing host key and will always be shadowed by it.
```

The alias is stored but will never be reachable via that value as long as the host key exists.
 
#### `jf auth host alias remove <hostname> <alias>`
 
Remove an alias from a host.
 
```
jf auth host alias remove <hostname> <alias>
```
 
Removes `<alias>` from the host's `aliases` list. Errors if the alias is not set on that host.
 
#### `jf auth host alias list [<hostname>]`
 
List aliases.
 
```
jf auth host alias list [<hostname>]
```
 
Without `<hostname>`: lists all hosts with their aliases. With `<hostname>`: lists aliases for that host only. Flags duplicate aliases across hosts:
 
```
jf.home.example.com  [home, jf]
nas.local             [nas, home]  ← WARNING: "home" is also set on jf.home.example.com
```
 
---
 
## 6. Global Flags
 
These flags are available on all commands, not just `auth`:
 
| Flag | Env Var | Description |
|------|---------|-------------|
| `--server <value>` | `JF_SERVER` | Hostname or full URL to select/override the target server. |
| `--profile <name>` | `JF_PROFILE` | Profile name to use on the resolved host. |
| `--config <path>` | `JF_CONFIG` | Path to config file (overrides default location). |
 
---
 
## 7. Migration
 
### From `credentials.json`
 
If `config.json` does not exist but a legacy `credentials.json` is found in the same directory, the CLI performs automatic silent migration on first run:
 
1. Read `credentials.json`.
2. Extract the server URL and credentials.
3. Parse the hostname from the server URL.
4. Create `config.json` with a single host and a single profile named `"default"`.
5. Set that host as `defaultHost` and `"default"` as `defaultProfile`.
6. Rename `credentials.json` to `credentials.json.bak`.
7. Print: `Migrated credentials to new profile format. Backup: credentials.json.bak`
 
No data is lost. The backup file is never read by the CLI again.
 
---
 
## 8. Validation Rules
 
### On Config Load
 
- `defaultHost`, if set, must be a key in `hosts`.
- `defaultProfile`, if set on a host, must be a key in that host's `profiles`.
- Every host must have a non-empty `baseUrl`.
- Every host must have at least one profile.
- Each profile must have either `token` or `apiKey` (not both, not neither).
 
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
 
`https://myserver.com` and `https://myserver.com:8096/jellyfin` both resolve to hostname `myserver.com`. They share a host entry. The first login sets the host-level `baseUrl`. A subsequent login with a different URL stores the difference as a profile-level `baseUrl` override.
 
### IP addresses as hostnames
 
IP addresses (e.g. `192.168.1.50`) are valid hostname keys. They follow all the same rules.
 
### Hostname normalization
 
Hostnames are lowercased before use as config keys. `NAS.local` and `nas.local` resolve to the same entry.
 
### Token expiry
 
Token expiry detection is outside the scope of this spec. The CLI should handle HTTP 401 responses gracefully and prompt re-authentication, but the profile system itself does not track expiry.
 
### Alias shadowed by host key
 
If an alias value is identical to an existing host key, the host key always wins — the alias scan is never reached. The alias is effectively unreachable while that host key exists. This is intentional: host keys are authoritative identifiers; aliases are convenience shortcuts.
 
`jf auth host alias add` warns when this situation is created (see §5.3).
 
### Duplicate aliases
 
Multiple hosts may share an alias. When a lookup matches more than one host:
 
- The first matching host in config file order is used.
- A warning is printed to stderr:
  ```
  Warning: alias "home" is defined on multiple hosts: jf.home.example.com, backup.example.com
  Using jf.home.example.com. To suppress, make aliases unique or use the full hostname.
  ```
 
This is by design — the user is informed rather than blocked, and the tie-break is deterministic.
 
### Concurrent config writes
 
The CLI uses atomic file writes (write to temp file, then rename) to prevent corruption from concurrent processes. No file locking is implemented; last write wins.
 
---
 
## 10. Environment Variable Summary
 
| Variable | Description | Equivalent Flag |
|----------|-------------|-----------------|
| `JF_SERVER` | Default server hostname or URL | `--server` |
| `JF_PROFILE` | Default profile name | `--profile` |
| `JF_CONFIG` | Config file path | `--config` |