#!/usr/bin/env python3
"""Validate the marketplace catalog, plugin manifests, and SKILL.md frontmatter.

Checks are deliberately strict about the Agent Skills spec (agentskills.io) so the
same skill folders stay uploadable to claude.ai and the Skills API, not just
installable as Claude Code / Codex plugins.

Requires PyYAML: frontmatter is real YAML, and hand-rolling a parser silently
accepts things the spec rejects (block scalars, lists, duplicate keys, quoting).
    pip install pyyaml
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

try:
    import yaml
except ImportError:  # pragma: no cover - environment guard
    sys.exit(
        "error: PyYAML is required.\n"
        "  pip install pyyaml   (or: python -m pip install pyyaml)"
    )

REPO_ROOT = Path(__file__).resolve().parent.parent
MARKETPLACE = REPO_ROOT / ".claude-plugin" / "marketplace.json"

# The six fields the Agent Skills spec allows. Anything else is Claude Code-only
# and makes claude.ai / Skills API uploads fail with an unexpected-key error.
SPEC_FIELDS = {"name", "description", "license", "compatibility", "metadata", "allowed-tools"}

NAME_RE = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)*$")

errors: list[str] = []


def fail(msg: str) -> None:
    errors.append(msg)


def rel(path: Path) -> str:
    try:
        return str(path.relative_to(REPO_ROOT)).replace("\\", "/")
    except ValueError:
        return str(path)


def load_json(path: Path) -> dict | None:
    """Read JSON, reporting a diagnosis instead of raising a traceback in CI."""
    try:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
    except json.JSONDecodeError as exc:
        fail(f"{rel(path)}: invalid JSON at line {exc.lineno} col {exc.colno}: {exc.msg}")
        return None
    except OSError as exc:
        fail(f"{rel(path)}: cannot read ({exc.strerror})")
        return None
    if not isinstance(data, dict):
        fail(f"{rel(path)}: expected a JSON object, got {type(data).__name__}")
        return None
    return data


def parse_frontmatter(path: Path) -> dict | None:
    # utf-8-sig strips a BOM, which would otherwise hide the opening '---'.
    text = path.read_text(encoding="utf-8-sig")
    if not text.startswith("---"):
        fail(f"{rel(path)}: missing YAML frontmatter")
        return None

    # Find the closing delimiter, tolerating CRLF.
    match = re.search(r"^---[ \t]*$", text[3:], re.MULTILINE)
    if not match:
        fail(f"{rel(path)}: unterminated YAML frontmatter")
        return None

    block = text[3 : 3 + match.start()]
    try:
        # safe_load rejects duplicate keys only in newer PyYAML; check explicitly below.
        data = yaml.safe_load(block)
    except yaml.YAMLError as exc:
        fail(f"{rel(path)}: frontmatter is not valid YAML: {str(exc).splitlines()[0]}")
        return None

    if data is None:
        fail(f"{rel(path)}: frontmatter is empty")
        return None
    if not isinstance(data, dict):
        fail(f"{rel(path)}: frontmatter must be a mapping, got {type(data).__name__}")
        return None

    keys = re.findall(r"^([A-Za-z0-9_-]+):", block, re.MULTILINE)
    dupes = {k for k in keys if keys.count(k) > 1}
    if dupes:
        fail(f"{rel(path)}: duplicate frontmatter key(s): {sorted(dupes)}")

    return data


def check_str(path: Path, fields: dict, key: str, *, required: bool, maxlen: int) -> None:
    if key not in fields:
        if required:
            fail(f"{rel(path)}: missing required `{key}`")
        return
    value = fields[key]
    if not isinstance(value, str):
        # A YAML list or a bare `description:` resolves to list/None, not str.
        fail(f"{rel(path)}: `{key}` must be a string, got {type(value).__name__}")
        return
    if required and not value.strip():
        fail(f"{rel(path)}: `{key}` is empty")
        return
    if len(value) > maxlen:
        fail(f"{rel(path)}: `{key}` is {len(value)} characters, max {maxlen}")


def check_skill(skill_dir: Path) -> None:
    skill_md = skill_dir / "SKILL.md"
    if not skill_md.is_file():
        fail(f"{rel(skill_dir)}: no SKILL.md")
        return

    fields = parse_frontmatter(skill_md)
    if fields is None:
        return

    extra = set(fields) - SPEC_FIELDS
    if extra:
        fail(
            f"{rel(skill_md)}: frontmatter field(s) {sorted(extra)} are outside the "
            f"Agent Skills spec; allowed: {sorted(SPEC_FIELDS)}"
        )

    check_str(skill_md, fields, "name", required=True, maxlen=64)
    check_str(skill_md, fields, "description", required=True, maxlen=1024)
    check_str(skill_md, fields, "license", required=False, maxlen=200)
    check_str(skill_md, fields, "compatibility", required=False, maxlen=500)

    name = fields.get("name")
    if isinstance(name, str) and name:
        if not NAME_RE.match(name):
            fail(f"{rel(skill_md)}: name {name!r} must be lowercase alphanumeric with single hyphens")
        if name != skill_dir.name:
            fail(f"{rel(skill_md)}: name {name!r} must match its directory name {skill_dir.name!r}")

    metadata = fields.get("metadata")
    if metadata is not None and not isinstance(metadata, dict):
        fail(f"{rel(skill_md)}: `metadata` must be a mapping, got {type(metadata).__name__}")

    # A skill that points at a file it no longer ships is a dead reference.
    body = skill_md.read_text(encoding="utf-8-sig")
    for target in re.findall(r"\]\(([^)#:]+\.(?:md|json|ya?ml|py|sh|ps1|cs|rs))\)", body):
        if not (skill_dir / target).exists():
            fail(f"{rel(skill_md)}: links to missing file {target!r}")


def main() -> int:
    catalog = load_json(MARKETPLACE) if MARKETPLACE.is_file() else None
    if catalog is None:
        if not MARKETPLACE.is_file():
            fail(f"missing {rel(MARKETPLACE)}")
        print_report()
        return 1

    for required in ("name", "owner", "plugins"):
        if required not in catalog:
            fail(f"{rel(MARKETPLACE)}: missing required field {required!r}")

    plugin_root = (catalog.get("metadata") or {}).get("pluginRoot", "")
    entries = catalog.get("plugins")
    if not isinstance(entries, list):
        fail(f"{rel(MARKETPLACE)}: `plugins` must be an array")
        print_report()
        return 1

    seen_skills = 0
    catalogued_dirs: set[Path] = set()

    for entry in entries:
        if not isinstance(entry, dict):
            fail(f"{rel(MARKETPLACE)}: plugin entry must be an object, got {type(entry).__name__}")
            continue
        entry_name = entry.get("name", "<unnamed>")
        source = entry.get("source")
        if not isinstance(source, str):
            # Remote sources (github/git/npm/archive) are objects and live outside this repo.
            continue

        # A relative source resolves against the marketplace root, optionally
        # prefixed by metadata.pluginRoot.
        base = REPO_ROOT / plugin_root if plugin_root else REPO_ROOT
        plugin_dir = (base / source).resolve()
        if not plugin_dir.is_dir():
            fail(f"{rel(MARKETPLACE)}: plugin {entry_name!r} source {source!r} does not exist")
            continue
        catalogued_dirs.add(plugin_dir)

        manifest_path = plugin_dir / ".claude-plugin" / "plugin.json"
        if not manifest_path.is_file():
            fail(f"{rel(plugin_dir)}: missing .claude-plugin/plugin.json")
        else:
            data = load_json(manifest_path)
            if data is not None:
                if data.get("name") != entry_name:
                    fail(
                        f"{rel(manifest_path)}: name {data.get('name')!r} does not match "
                        f"marketplace entry {entry_name!r}"
                    )
                if data.get("version") != entry.get("version"):
                    fail(
                        f"{entry_name}: version differs between marketplace.json "
                        f"({entry.get('version')!r}) and plugin.json ({data.get('version')!r})"
                    )

        skills_dir = plugin_dir / "skills"
        if not skills_dir.is_dir():
            fail(f"{rel(plugin_dir)}: no skills/ directory")
            continue
        for skill_dir in sorted(p for p in skills_dir.iterdir() if p.is_dir()):
            check_skill(skill_dir)
            seen_skills += 1

    # A plugin directory on disk that no catalog entry points at ships to nobody.
    plugins_parent = REPO_ROOT / (plugin_root.strip("./") or "plugins")
    if plugins_parent.is_dir():
        for candidate in sorted(p for p in plugins_parent.iterdir() if p.is_dir()):
            if (candidate / ".claude-plugin" / "plugin.json").is_file():
                if candidate.resolve() not in catalogued_dirs:
                    fail(
                        f"{rel(candidate)}: plugin exists on disk but no {rel(MARKETPLACE)} "
                        f"entry points at it, so nobody can install it"
                    )

    print_report(seen_skills, len(catalogued_dirs))
    return 1 if errors else 0


def print_report(seen_skills: int = 0, plugins: int = 0) -> None:
    if errors:
        print(f"FAIL: {len(errors)} problem(s)\n", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
    else:
        print(f"OK: marketplace, {plugins} plugin manifest(s), and {seen_skills} skill(s) validated")


if __name__ == "__main__":
    raise SystemExit(main())
