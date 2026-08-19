---
name: forge-detect
description: Reliably distinguish which software forge a host or git remote points at — Forgejo, Gitea, GitHub, or GitLab — including sub-path installs and locked-down (REQUIRE_SIGNIN_VIEW) instances. Use before picking forge tooling (fj/fj-ex vs tea vs gh vs glab), when a repo's remote host is unfamiliar, or when someone asks "is this Gitea or Forgejo?". Ships tested detection scripts for bash and PowerShell.
license: MIT
---

# Forge Detection: Forgejo vs Gitea (vs GitHub/GitLab)

Forgejo is a hard fork of Gitea; their UIs, APIs, and even SSH banners are nearly
identical, so guessing from looks fails. Wrong guesses route you to the wrong CLI
(`fj`/`fj-ex` for Forgejo, `tea`/REST for Gitea). Detect first, then pick tooling.

## Quick start

Run the bundled script (bash or PowerShell — same logic, same output):

```bash
scripts/detect-forge.sh                       # infer from git remote origin
scripts/detect-forge.sh code.example.com --json
scripts/detect-forge.sh --token <t> host/sub  # locked instances
```

```powershell
scripts/Detect-Forge.ps1 code.example.com -Json
```

Output includes `forge`, `confidence` (`confirmed`/`likely`), `version`, and the
resolved `api_base` (which matters for sub-path installs). Exit code 0 =
detected, 1 = unknown, 2 = usage error.

## Detection signals, most reliable first

Validated empirically against Forgejo 14/15/16, Gitea 1.25/1.27, a
REQUIRE_SIGNIN_VIEW instance, and a reverse-proxy sub-path install.

| Signal | Forgejo | Gitea |
|---|---|---|
| `GET {base}/api/v1/version` body | `"14.0.2+gitea-1.22.0"` — always carries a `+gitea-<compat>` suffix | plain `"1.25.5"` |
| `GET {base}/api/forgejo/v1/version` | 200 (401/403 when locked) | **404 even when locked** — the route does not exist |
| HTML footer of any page, login included | `Powered by Forgejo` | `Powered by Gitea` |

Neither-of-the-two tells: `GET {base}/api/v4/version` answering 200/401 means
GitLab; an `x-github-request-id` response header means GitHub or GHES.

Decision order:

1. **`/api/v1/version`** — 200 with `+gitea-` → Forgejo; 200 with plain `1.x` →
   Gitea. Done for most instances, no auth needed.
2. **401/403?** The instance requires sign-in for all API routes. Probe
   `/api/forgejo/v1/version`: Gitea returns 404 for unregistered routes even
   when locked, so 401/403 there leans Forgejo, 404 leans Gitea. But a blanket
   401/403 can also be GitLab or an auth proxy — check `/api/v4/version`
   (200/401 → GitLab) before concluding.
3. **Confirm with HTML branding** — the `Powered by …` footer is served on the
   login page, so it works unauthenticated. Treat API + branding agreement as
   confirmed; branding alone as likely.
4. **404 at `/api/v1/version`?** You're probing the wrong base path. Strip one
   path segment and retry (repo → owner → reverse-proxy sub-path → bare host).

## Traps that break naive detection

- **Sub-path installs**: `https://host/gitea/owner/repo` — the API is at
  `https://host/gitea/api/v1`, not `https://host/api/v1`. Always walk the path
  segments; never assume the API sits at the host root.
- **SSH remotes tell you nothing**: `ssh://forgejo@host/...` vs `git@host:...`
  is a configured username, not a product signal. Convert to `https://host` and
  probe HTTP.
- **Version numbers alone mislead**: Forgejo major versions (7–16+) look nothing
  like Gitea's 1.x, but old Forgejo (≤ v1.21, pre-2024) also used 1.x — rely on
  the `+gitea-` suffix and the `/api/forgejo/` route, not the number's shape.
- **`REQUIRE_SIGNIN_VIEW` hides the version** but not the routing table (404 vs
  401/403) or the login-page footer.
- **Do not use `/api/v1/nodeinfo`**: 404 on Forgejo unless federation is on,
  "Not implemented" on gitea.com. Useless in practice.

## After detection, route the work

- **Forgejo** → use the `forgejo-ops` skill — [`fj`](https://codeberg.org/forgejo-contrib/forgejo-cli)
  (`cargo install forgejo-cli`) + [`fj-ex`](https://github.com/JKamsker/forgejo-cli-ex)
  (`cargo install forgejo-cli-ex`).
- **Gitea** → use the `gitea-ops` skill — [`tea`](https://gitea.com/gitea/tea)
  (binaries at [dl.gitea.com/tea](https://dl.gitea.com/tea/)) + bundled REST
  scripts; beware `tea` hangs in non-interactive shells.
- **GitHub** → [`gh`](https://cli.github.com). **GitLab** → [`glab`](https://gitlab.com/gitlab-org/cli).

Each ops skill has a "Where to get them" section with the full install matrix.

Both scripts print `api_base` — pass it to whatever tooling you pick so
sub-path installs keep working.
