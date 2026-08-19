---
name: forgejo-ops
description: Work with Forgejo instances (self-hosted, Codeberg) through the fj and fj-ex CLIs instead of hand-rolled curl — PRs, issues, releases, Actions runs, full CI log download, artifacts, cancel/rerun, workflow dispatch, and secrets. Use for any repo whose remote is a Forgejo host, when CI fails on Forgejo Actions, or when fj argument-order errors appear ("unexpected argument found"). Not for Gitea (use gitea-ops) or GitHub (use gh).
license: MIT
---

# Forgejo Operations (`fj` + `fj-ex`)

Two CLIs split the work:

- **`fj`** — repo, issue, pr, wiki, actions (tasks/variables/secrets/dispatch),
  release, tag, user, org, auth, whoami. Talks the token API.
- **`fj-ex`** — Actions detail `fj` cannot reach because Forgejo has no API for
  it: run/job **logs**, **artifacts**, **cancel/rerun**, run/job listing. It
  scrapes web-UI endpoints, so it authenticates with a **UI session (username +
  password)**, not the PAT: `fj-ex auth login` once per host.

Both infer host + repo from the git remote when run inside the repo, take
`--json` for parsing, and accept `--host`/`--repo`/`--remote` when outside one.
Unsure which forge you're on? Run the `forge-detect` skill first.

## Where to get them

| | `fj` (forgejo-cli) | `fj-ex` (forgejo-cli-ex) |
|---|---|---|
| Source | [codeberg.org/forgejo-contrib/forgejo-cli](https://codeberg.org/forgejo-contrib/forgejo-cli) | [github.com/JKamsker/forgejo-cli-ex](https://github.com/JKamsker/forgejo-cli-ex) · [mirror](https://codeberg.org/JKamsker/forgejo-cli-ex) |
| Crate | [crates.io/crates/forgejo-cli](https://crates.io/crates/forgejo-cli) | [crates.io/crates/forgejo-cli-ex](https://crates.io/crates/forgejo-cli-ex) |
| Docs | [wiki](https://codeberg.org/forgejo-contrib/forgejo-cli/wiki) · [install page](https://codeberg.org/forgejo-contrib/forgejo-cli/wiki/Installation) | repo README · [build write-up](https://blog.kamsker.at/blog/how-fj-ex-was-built/) |
| License | Apache-2.0 or MIT | LGPL-3.0-or-later |

```bash
cargo install forgejo-cli      # installs the `fj` binary
cargo install forgejo-cli-ex   # installs the `fj-ex` binary
fj version && fj-ex --version  # note: fj has no --version, fj-ex does
```

Prefer not to build from source? `fj` ships prebuilt binaries for x86_64 Windows
and x86_64/aarch64 Linux-GNU on its
[releases tab](https://codeberg.org/forgejo-contrib/forgejo-cli/releases/latest),
and is packaged in several distros ([repology](https://repology.org/project/forgejo-cli/versions)).
`fj-ex` is crates.io-only — `cargo install` is the supported path, so a Rust
toolchain ([rustup.rs](https://rustup.rs)) is a prerequisite for it either way.

## Argument order is the #1 failure mode

`fj` treats several flags as scoped options that must sit in exact positions:

```bash
fj -H forge.example.com whoami -r origin     # ✅ -H/--host is GLOBAL: before the command
fj whoami -H forge.example.com               # ❌ "unexpected argument '-H' found"

fj pr -R origin create --base main --head feat/x -A   # ✅ -R after `pr`, before `create`
fj pr create -R origin --base main --head feat/x      # ❌ fails

fj version                                   # ✅ (fj --version / -V do not exist)
```

`whoami -r` takes a **git remote name** (`origin`), never `owner/repo`.

## Everyday commands

```bash
fj auth add-key <user> <token>       # after: fj -H <host> auth add-key ...
fj auth list                         # verify: <user>@<host>
fj whoami -r origin

fj pr search -s open
fj pr status <n>                     # mergeability + CI state
fj pr status <n> --wait              # block until CI settles
fj pr view <n>
fj actions tasks -R origin           # high-level run status only — NO step logs
fj actions dispatch -R origin build.yml <branch> -I key=value
fj actions secrets set -R origin NAME   # secrets list may print nothing even when secrets exist
fj repo browse                       # open repo in browser
```

## CI failed — get the actual logs (fj-ex)

```bash
fj-ex auth login                                  # once per host (UI credentials)
fj-ex actions runs --latest                       # after every push: check, don't assume
fj-ex actions logs run --latest --out-dir tmp/ci-logs
fj-ex actions jobs --run-index <n>
fj-ex actions logs job --run-index <n> --job-index <m> --out-file tmp/job.log
fj-ex actions artifacts list --latest
fj-ex actions artifacts get --run-index <n> --artifact <name> --out-file a.zip
fj-ex actions rerun --run-index <n> [--job-index <m>] [--dry-run]
fj-ex actions cancel --run-index <n>
```

Downloaded logs may contain secrets — treat them as sensitive, keep them out of
commits.

## Forgejo-specific traps

- **Push ≠ green CI.** On repos that auto-deploy from a branch the webhook fires
  on push regardless of CI outcome. After any push, `fj-ex actions runs --latest`
  and read the result; a live deploy and a green pipeline are separate facts.
- **Dispatch wants the bare filename**: `fj actions dispatch -R origin build.yml
  <ref>` — passing `.forgejo/workflows/build.yml` returns a non-JSON 404 that
  `fj` fails to parse.
- **Colons in job names break dispatch**: `name: Smoke: bash` → 500 on
  workflow_dispatch. Rename the job.
- **Runner rejects `node24` actions**: `runs.using: node24` fails validation
  (allowed: composite, docker, node12/16/20, go, sh). Pin node-based actions to
  a `node20` major or replace them with composite actions.
- **Secrets delete on a missing name errors**; for idempotent delete-then-create
  flows discard it (`fj actions secrets delete ... 2>/dev/null || true`).
- Workflows live in `.forgejo/workflows/` (`.github/workflows/` also works);
  syntax is GitHub-Actions-compatible.
- Never fall back to `gh` (wrong platform) or hand-rolled `curl` — `fj --json` /
  `fj-ex --json` cover parsing needs.
