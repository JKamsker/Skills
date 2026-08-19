---
name: gitea-ops
description: Work with Gitea instances via the tea CLI and a bundled REST-backed PowerShell toolkit — issues, labels, milestones, pull requests, comments, Actions runs, failure logs, and workflow dispatch. Use for any repo whose remote is a Gitea host, when tea hangs in a non-interactive shell, or when Gitea Actions CI needs diagnosing. Not for Forgejo (use forgejo-ops) or GitHub (use gh).
license: MIT
---

# Gitea Operations (`tea` + REST scripts)

Two tool layers, chosen by context:

- **`tea`** ([gitea/tea](https://gitea.com/gitea/tea)) — official CLI: issues,
  pulls, labels, milestones, releases, repos, actions, comments, `tea api` for
  raw calls. Good **interactively**.
- **Bundled PowerShell toolkit** ([scripts/](scripts/GiteaApi.psm1)) — a plain
  REST client + task scripts. Use it for **anything scripted or headless**,
  because of the hang problem below.

Unsure which forge you're on? Run the `forge-detect` skill first — Gitea and
Forgejo look identical but diverge in API surface and tooling.

## Where to get `tea`

- Source + docs: [gitea.com/gitea/tea](https://gitea.com/gitea/tea) (MIT)
- Prebuilt binaries: [dl.gitea.com/tea](https://dl.gitea.com/tea/) — the simplest
  route on Windows and the one to use in CI images
- macOS: `brew install tea` (official formula)
- Windows: MSYS2 [`mingw-w64-tea`](https://packages.msys2.org/base/mingw-w64-tea)
  (third-party); Arch [`tea`](https://archlinux.org/packages/extra/x86_64/tea/),
  Alpine [`tea`](https://pkgs.alpinelinux.org/packages?name=tea&branch=edge)
- Docker: [`gitea/tea`](https://hub.docker.com/r/gitea/tea)
- From source: `git clone https://gitea.com/gitea/tea && cd tea && make`
  (needs Go 1.26+ and GNU Make)

The bundled PowerShell toolkit needs no install — it is `scripts/` in this skill
and only requires PowerShell 5.1+ plus the env vars under [Setup](#setup).

## The `tea` hang problem (critical)

`tea` commands — including `tea --version` — **hang indefinitely in
non-interactive shells**: agent harnesses, CI, pipes, and Windows git-bash/MSYS
terminals (no winpty). This is reproducible and documented upstream in tooling
that abandoned `tea api` as a backend for exactly this reason.

Rules:

- From an agent or script on Windows: run `tea` **via PowerShell**, never via
  git-bash. `pwsh -c "tea pr ls"` works; bash `tea pr ls` hangs.
- In fully headless contexts (CI, cron): don't shell out to `tea` at all — use
  the bundled REST scripts with a token.
- If a `tea` call produces no output for ~10s, kill it and switch layers;
  it will not recover.

## Setup

`tea login add` stores credentials at `$XDG_CONFIG_HOME/tea/config.yml`
(Windows: `%LOCALAPPDATA%\tea\config.yml`). The REST toolkit needs only env
vars — parameters win over env:

```powershell
$env:GITEA_URL   = 'https://code.example.com/gitea'   # include any sub-path!
$env:GITEA_REPO  = 'owner/repo'
$env:GITEA_TOKEN = '<token>'   # read scopes for queries; issue-write to create
```

To reuse a tea login token for git-over-HTTP without printing it:

```powershell
scripts/Set-GiteaGitCredentialFromTea.ps1 -TeaLogin my-login
```

## REST toolkit usage

```powershell
Import-Module ./scripts/GiteaApi.psm1 -Force
$ctx = Get-GiteaContext                    # resolves URL/repo/token from env

Get-GiteaRepository -Context $ctx
Get-GiteaIssues     -Context $ctx          # follows Link pagination — returns ALL pages
Get-GiteaLabels     -Context $ctx
Get-GiteaMilestones -Context $ctx
New-GiteaIssue      -Context $ctx -Title '...' -Body '...' -LabelIds $ids -MilestoneId $ms.id
Add-GiteaIssueDependency -Context $ctx -Index 12 -BlockedByIndex 5
Get-GiteaPullRequestByHead -Context $ctx -Head feat/my-branch
Update-GiteaPullRequest    -Context $ctx -Number 143 -BodyPath ./pr-body.md
New-GiteaIssueComment      -Context $ctx -Number 143 -Body 'Validation passed.'
```

Task scripts (each has comment-based help, `Get-Help <script> -Full`):

| Script | Does |
|---|---|
| [Get-GiteaActionFailures.ps1](scripts/Get-GiteaActionFailures.ps1) | List recent failing Actions runs/jobs, `-IncludeLogs -LogTail 80` for log tails |
| [Invoke-GiteaWorkflowDispatch.ps1](scripts/Invoke-GiteaWorkflowDispatch.ps1) | Trigger `workflow_dispatch` via REST |
| [New-GiteaPullRequest.ps1](scripts/New-GiteaPullRequest.ps1) | Open (or reuse) a PR for the current branch |
| [Update-GiteaPullRequest.ps1](scripts/Update-GiteaPullRequest.ps1) | Edit PR title/body/base/state; resolves PR from current branch if `-Number` omitted |
| [Add-GiteaPullRequestComment.ps1](scripts/Add-GiteaPullRequestComment.ps1) | Post markdown comment from `-Body`/`-BodyPath` |
| [Connect-GiteaWebSession.ps1](scripts/Connect-GiteaWebSession.ps1) | Web login → cookie jar for web-only endpoints |
| [Get-GiteaActionWebLog.ps1](scripts/Get-GiteaActionWebLog.ps1) | Read Actions job state/logs through the web session (`-RunIndex n -ExpandAllSteps`) |
| [Set-GiteaGitCredentialFromTea.ps1](scripts/Set-GiteaGitCredentialFromTea.ps1) | Copy a tea login token into git's credential helper |

Diagnose failing CI, end to end:

```powershell
scripts/Get-GiteaActionFailures.ps1 -Branch feat/my-branch -IncludeLogs -LogTail 80
# web session route when the token API lacks the log detail you need:
scripts/Connect-GiteaWebSession.ps1 -Username my-user
scripts/Get-GiteaActionWebLog.ps1 -RunIndex 35 -ExpandAllSteps
```

## Gitea traps

- **Sub-path installs are common** (`https://host/gitea`). `GITEA_URL` must
  include the sub-path; probing `https://host/api/v1` 404s.
- **Gitea's Actions log API is thinner than GitHub's** — full step logs
  sometimes exist only behind web-UI endpoints; that's what the web-session
  scripts are for.
- **Issue dependencies** need Settings → Repository → Issues → dependencies
  enabled, or `Add-GiteaIssueDependency` fails; an existing dependency answers
  HTTP 409 (treat as already-present, not error).
- **PowerShell 5.1 mangles em-dashes** to ISO-8859-1 in request bodies; the
  bundled module forces UTF-8 — do the same in any hand-written caller.
- Labels created at repo level; org-level labels must be created in the UI but
  are then picked up by name.
