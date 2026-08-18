# JKamsker Skills

[![Validate skills](https://github.com/JKamsker/Skills/actions/workflows/validate.yml/badge.svg)](https://github.com/JKamsker/Skills/actions/workflows/validate.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

[Agent Skills](https://agentskills.io) for software design work, distributed as a Claude Code plugin marketplace and usable directly by any skills-compatible agent.

| Skill | What it does |
| :---- | :----------- |
| [`build-ergonomic-clis`](plugins/build-ergonomic-clis/skills/build-ergonomic-clis/SKILL.md) | Design, review, and implement product-grade CLIs: command trees, flag conventions, auth and profile UX, config precedence, human-first output with opt-in `--json`, confirmation rules, and exit codes. Includes C#/.NET Spectre.Console.Cli and Rust clap references. |
| [`forge-detect`](plugins/git-forges/skills/forge-detect/SKILL.md) | Reliably tell Forgejo from Gitea (from GitHub and GitLab too) for any host or git remote — sub-path installs and locked-down instances included. Ships tested bash and PowerShell detection scripts. |
| [`forgejo-ops`](plugins/git-forges/skills/forgejo-ops/SKILL.md) | Operate Forgejo instances with `fj` and `fj-ex`: PRs, Actions runs, full CI log download, artifacts, rerun/cancel, dispatch, secrets — plus the argument-order gotchas and runner traps that break first attempts. |
| [`gitea-ops`](plugins/git-forges/skills/gitea-ops/SKILL.md) | Operate Gitea instances with `tea` and a bundled REST-backed PowerShell toolkit: issues, PRs, Actions failure diagnosis, workflow dispatch — and how to avoid `tea`'s non-interactive-shell hangs. |
| [`incremental-source-generator`](plugins/incremental-source-generator/skills/incremental-source-generator/SKILL.md) | Design, review, debug, and fix Roslyn incremental source generators: pipeline incrementality, equatable models, marker attributes, MSBuild inputs, snapshot testing, and analyzer packaging. |

## Install

### Claude Code (recommended)

```
/plugin marketplace add JKamsker/Skills
/plugin install build-ergonomic-clis@jkamsker-skills
/plugin install git-forges@jkamsker-skills
/plugin install incremental-source-generator@jkamsker-skills
```

The `owner/repo` shorthand clones over SSH. If you do not have GitHub SSH keys set up,
use the HTTPS URL instead (everything after this is identical):

```
/plugin marketplace add https://github.com/JKamsker/Skills.git
```

The same commands work from your shell as `claude plugin marketplace add ...` and
`claude plugin install ...` if you prefer not to start a session first.

Install only the plugins you want. Skills are namespaced, so they appear as
`/build-ergonomic-clis:build-ergonomic-clis` and
`/incremental-source-generator:incremental-source-generator`, and Claude also loads them
automatically when a request matches their description.

Updating takes **two** commands. Refreshing the marketplace only updates the catalog listing;
the installed plugin stays on its cached version until you update the plugin itself:

```
/plugin marketplace update jkamsker-skills
/plugin update build-ergonomic-clis@jkamsker-skills
```

(Third-party marketplaces have auto-update disabled by default, so this is not automatic.
A restart is required for the new version to load.)

### Codex

Codex reads this repository's `.claude-plugin/marketplace.json` directly, so no clone and no
separate manifest are needed:

```bash
codex plugin marketplace add JKamsker/Skills
codex plugin add build-ergonomic-clis@jkamsker-skills
codex plugin add git-forges@jkamsker-skills
codex plugin add incremental-source-generator@jkamsker-skills
```

This registers the marketplace and enables both plugins in `~/.codex/config.toml`. Pin a ref
with `JKamsker/Skills@main` if you want to track something other than the default branch, and
refresh later with `codex plugin marketplace upgrade jkamsker-skills`.

### Cursor, Gemini CLI, opencode, and other agents

A skill is just a folder with a `SKILL.md`, so any agent that reads a skills directory can
use these directly. Clone the repo and point your agent's skills directory at it:

```bash
git clone https://github.com/JKamsker/Skills.git
cd Skills

./scripts/install-skills.sh                      # defaults to ~/.agents/skills
./scripts/install-skills.sh ~/.cursor/skills     # or any other target
```

On Windows:

```powershell
./scripts/install-skills.ps1
./scripts/install-skills.ps1 -TargetDir ~/.claude/skills
```

The Agent Skills spec defines the *skill folder format*, not where agents look for skills, so
discovery paths are per-agent. `~/.agents/skills` is the default here because Codex reads it and
several other agents have adopted it, but point the script wherever your agent looks
(`~/.cursor/skills`, `~/.claude/skills`, `~/.codex/skills`, or a repo-local `.agents/skills`).

The script symlinks each skill when the platform allows it, so `git pull` is enough to stay
current. Where symlinks are unavailable (Windows without Developer Mode, some filesystems)
it copies instead and tells you to re-run it after pulling.

Each skill also ships an `agents/openai.yaml` with Codex interface metadata (display name,
short description, default prompt), which travels with the skill folder.

### claude.ai and the Skills API

Zip a single skill directory and upload it under **Settings → Features** on claude.ai, or
`POST /v1/skills`. Frontmatter is kept within the six fields the Agent Skills spec allows
(`name`, `description`, `license`, `compatibility`, `metadata`, `allowed-tools`) so these
uploads do not fail on unexpected keys.

## Repository layout

Each skill exists in exactly one place. There is no generation step, no sync job, and no
duplicated copies to drift apart — the plugin directory *is* the source of truth, and every
distribution channel reads the same folder.

```
.claude-plugin/marketplace.json          # the catalog users add
plugins/
  build-ergonomic-clis/
    .claude-plugin/plugin.json           # per-plugin manifest and version
    skills/build-ergonomic-clis/         # the skill itself
      SKILL.md
      agents/openai.yaml                 # Codex interface metadata
      references/  assets/  tests/
  git-forges/
    .claude-plugin/plugin.json
    skills/
      forge-detect/                      # SKILL.md + detection scripts (bash + PowerShell)
      forgejo-ops/                       # SKILL.md (fj / fj-ex workflows)
      gitea-ops/                         # SKILL.md + REST-backed PowerShell toolkit
  incremental-source-generator/
    NOTICE                               # third-party licensing, ships with the plugin
    .claude-plugin/plugin.json
    skills/incremental-source-generator/
      SKILL.md
      agents/openai.yaml
      references/
scripts/
  install-skills.sh / .ps1               # install into any agent's skills directory
  validate_skills.py                     # CI gate
```

Adding the marketplace clones the whole repository, so `src/`, `raw/`, and `docs/` do land on
disk once (~21 MB). Installing a plugin is narrower: only that plugin's own directory is copied
into the plugin cache, so the unrelated directories are never part of an installed plugin.

## Contributing

Add a skill under `plugins/<plugin-name>/skills/<skill-name>/`, add a
`plugins/<plugin-name>/.claude-plugin/plugin.json`, and register it in
`.claude-plugin/marketplace.json`. Then:

```bash
pip install pyyaml
python scripts/validate_skills.py
```

This checks catalog/manifest wiring, version agreement between `marketplace.json` and each
`plugin.json`, that no plugin directory is orphaned from the catalog, that every `SKILL.md`
stays within the Agent Skills spec, and that no `SKILL.md` links to a file it does not ship.
CI runs the same script, checks both installer scripts parse, and adds `claude plugin validate`
as a non-blocking second opinion.

If you bundle third-party material in a skill, record it in [NOTICE](NOTICE) and in a `NOTICE`
file at that plugin's root, since only the plugin directory travels to installers.

Bump the `version` in **both** the marketplace entry and the plugin manifest on every
release — Claude Code only offers an update to users when that string changes.

To test changes locally before pushing:

```bash
claude plugin marketplace add ./
claude plugin install build-ergonomic-clis@jkamsker-skills
```

## License and attribution

The original content of this repository — the skills, their instructions and references written
for this project, the manifests, and the scripts — is MIT licensed. See [LICENSE](LICENSE).

Bundled third-party material and its licensing is listed in [NOTICE](NOTICE). In short:

- The Roslyn design docs under `references/roslyn/` are copied from
  [dotnet/roslyn](https://github.com/dotnet/roslyn) and remain MIT © .NET Foundation and
  Contributors; the full notice ships with the plugin at
  [plugins/incremental-source-generator/NOTICE](plugins/incremental-source-generator/NOTICE).
- Andrew Lock's "Creating a source generator" series is **summarised, not reproduced**. Those
  articles are all-rights-reserved, so `references/andrew-lock-series/` contains original
  per-article digests written for this skill, each linking to the canonical post. Short
  attributed quotations appear where they earn their place, and every one is verified verbatim
  against the source.

If you think something here is attributed incorrectly, please open an issue.
