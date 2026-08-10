# JKamsker Skills

[Agent Skills](https://agentskills.io) for software design work, distributed as a Claude Code plugin marketplace and usable directly by any skills-compatible agent.

| Skill | What it does |
| :---- | :----------- |
| [`build-ergonomic-clis`](plugins/build-ergonomic-clis/skills/build-ergonomic-clis/SKILL.md) | Design, review, and implement product-grade CLIs: command trees, flag conventions, auth and profile UX, config precedence, human-first output with opt-in `--json`, confirmation rules, and exit codes. Includes C#/.NET Spectre.Console.Cli and Rust clap references. |
| [`incremental-source-generator`](plugins/incremental-source-generator/skills/incremental-source-generator/SKILL.md) | Design, review, debug, and fix Roslyn incremental source generators: pipeline incrementality, equatable models, marker attributes, MSBuild inputs, snapshot testing, and analyzer packaging. |

## Install

### Claude Code (recommended)

```
/plugin marketplace add JKamsker/Skills
/plugin install build-ergonomic-clis@jkamsker-skills
/plugin install incremental-source-generator@jkamsker-skills
```

Install only the plugins you want. Skills are namespaced, so they appear as
`/build-ergonomic-clis:build-ergonomic-clis` and
`/incremental-source-generator:incremental-source-generator`, and Claude also loads them
automatically when a request matches their description.

Pull later updates with `/plugin marketplace update jkamsker-skills`.

### Codex, Cursor, Gemini CLI, opencode, and other agents

A skill is just a folder with a `SKILL.md`, so any agent that reads a skills directory can
use these directly. Clone the repo and point your agent's skills directory at it:

```bash
git clone https://github.com/JKamsker/Skills.git
cd Skills

./scripts/install-skills.sh                      # defaults to ~/.codex/skills
./scripts/install-skills.sh ~/.cursor/skills     # or any other target
```

On Windows:

```powershell
./scripts/install-skills.ps1
./scripts/install-skills.ps1 -TargetDir ~/.claude/skills
```

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
  incremental-source-generator/
    .claude-plugin/plugin.json
    skills/incremental-source-generator/
      SKILL.md
      agents/openai.yaml
      references/
scripts/
  install-skills.sh / .ps1               # install into any agent's skills directory
  validate_skills.py                     # CI gate
```

Claude Code copies only a plugin's own directory into its cache on install, so unrelated
top-level directories in this repo (`src/`, `raw/`, `docs/`) are never shipped to users.

## Contributing

Add a skill under `plugins/<plugin-name>/skills/<skill-name>/`, add a
`plugins/<plugin-name>/.claude-plugin/plugin.json`, and register it in
`.claude-plugin/marketplace.json`. Then:

```bash
python scripts/validate_skills.py
```

This checks catalog/manifest wiring, version agreement between `marketplace.json` and each
`plugin.json`, and that every `SKILL.md` stays within the Agent Skills spec. CI runs the
same script, plus `claude plugin validate` as a non-blocking second opinion.

Bump the `version` in **both** the marketplace entry and the plugin manifest on every
release — Claude Code only offers an update to users when that string changes.

To test changes locally before pushing:

```bash
claude plugin marketplace add ./
claude plugin install build-ergonomic-clis@jkamsker-skills
```

## License

MIT — see [LICENSE](LICENSE).
