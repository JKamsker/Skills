#!/usr/bin/env python3
"""Validate the marketplace catalog, plugin manifests, and SKILL.md frontmatter.

Runs without third-party dependencies so CI needs nothing but a Python runtime.
Checks are deliberately strict about the Agent Skills spec (agentskills.io) so the
same skill folders stay uploadable to claude.ai and the Skills API, not just
installable as Claude Code plugins.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
MARKETPLACE = REPO_ROOT / ".claude-plugin" / "marketplace.json"

# The six fields the Agent Skills spec allows. Anything else is Claude Code-only
# and makes `package_skill.py` / the Skills API reject the skill outright.
SPEC_FIELDS = {"name", "description", "license", "compatibility", "metadata", "allowed-tools"}

NAME_RE = re.compile(r"^[a-z0-9]+(-[a-z0-9]+)*$")

errors: list[str] = []


def fail(msg: str) -> None:
    errors.append(msg)


def parse_frontmatter(path: Path) -> dict[str, object] | None:
    """Minimal YAML frontmatter reader: top-level `key: value` and `key:` maps only."""
    text = path.read_text(encoding="utf-8")
    if not text.startswith("---"):
        fail(f"{path}: missing YAML frontmatter")
        return None
    end = text.find("\n---", 3)
    if end == -1:
        fail(f"{path}: unterminated YAML frontmatter")
        return None

    fields: dict[str, object] = {}
    current_map: str | None = None
    for line in text[3:end].splitlines():
        if not line.strip() or line.lstrip().startswith("#"):
            continue
        if line[0] in " \t":
            if current_map is None:
                fail(f"{path}: unexpected indented frontmatter line: {line.strip()!r}")
            continue
        current_map = None
        if ":" not in line:
            fail(f"{path}: malformed frontmatter line: {line.strip()!r}")
            continue
        key, _, value = line.partition(":")
        key = key.strip()
        value = value.strip().strip('"').strip("'")
        if value:
            fields[key] = value
        else:
            fields[key] = {}
            current_map = key
    return fields


def check_skill(skill_dir: Path) -> None:
    rel = skill_dir.relative_to(REPO_ROOT)
    skill_md = skill_dir / "SKILL.md"
    if not skill_md.is_file():
        fail(f"{rel}: no SKILL.md")
        return

    fields = parse_frontmatter(skill_md)
    if fields is None:
        return

    extra = set(fields) - SPEC_FIELDS
    if extra:
        fail(
            f"{rel}/SKILL.md: frontmatter field(s) {sorted(extra)} are outside the "
            f"Agent Skills spec; allowed: {sorted(SPEC_FIELDS)}"
        )

    name = fields.get("name")
    if not isinstance(name, str) or not name:
        fail(f"{rel}/SKILL.md: missing required `name`")
    else:
        if not NAME_RE.match(name):
            fail(f"{rel}/SKILL.md: name {name!r} must be lowercase alphanumeric with single hyphens")
        if len(name) > 64:
            fail(f"{rel}/SKILL.md: name exceeds 64 characters")
        if name != skill_dir.name:
            fail(f"{rel}/SKILL.md: name {name!r} must match its directory name {skill_dir.name!r}")

    description = fields.get("description")
    if not isinstance(description, str) or not description:
        fail(f"{rel}/SKILL.md: missing required `description`")
    elif len(description) > 1024:
        fail(f"{rel}/SKILL.md: description is {len(description)} chars, max 1024")

    compatibility = fields.get("compatibility")
    if isinstance(compatibility, str) and len(compatibility) > 500:
        fail(f"{rel}/SKILL.md: compatibility is {len(compatibility)} chars, max 500")


def main() -> int:
    if not MARKETPLACE.is_file():
        fail(f"missing {MARKETPLACE.relative_to(REPO_ROOT)}")
        print_report()
        return 1

    catalog = json.loads(MARKETPLACE.read_text(encoding="utf-8"))
    for required in ("name", "owner", "plugins"):
        if required not in catalog:
            fail(f"marketplace.json: missing required field {required!r}")

    seen_skills = 0
    for entry in catalog.get("plugins", []):
        entry_name = entry.get("name", "<unnamed>")
        source = entry.get("source")
        if not isinstance(source, str) or not source.startswith("./"):
            fail(f"marketplace.json: plugin {entry_name!r} needs a relative './' source")
            continue

        plugin_dir = (REPO_ROOT / source).resolve()
        if not plugin_dir.is_dir():
            fail(f"marketplace.json: plugin {entry_name!r} source {source} does not exist")
            continue

        manifest = plugin_dir / ".claude-plugin" / "plugin.json"
        if not manifest.is_file():
            fail(f"{source}: missing .claude-plugin/plugin.json")
        else:
            data = json.loads(manifest.read_text(encoding="utf-8"))
            if data.get("name") != entry_name:
                fail(
                    f"{source}/.claude-plugin/plugin.json: name {data.get('name')!r} "
                    f"does not match marketplace entry {entry_name!r}"
                )
            if data.get("version") != entry.get("version"):
                fail(
                    f"{entry_name}: version differs between marketplace.json "
                    f"({entry.get('version')!r}) and plugin.json ({data.get('version')!r})"
                )

        skills_dir = plugin_dir / "skills"
        if not skills_dir.is_dir():
            fail(f"{source}: no skills/ directory")
            continue
        for skill_dir in sorted(p for p in skills_dir.iterdir() if p.is_dir()):
            check_skill(skill_dir)
            seen_skills += 1

    print_report(seen_skills)
    return 1 if errors else 0


def print_report(seen_skills: int = 0) -> None:
    if errors:
        print(f"FAIL: {len(errors)} problem(s)\n", file=sys.stderr)
        for err in errors:
            print(f"  - {err}", file=sys.stderr)
    else:
        print(f"OK: marketplace, plugin manifests, and {seen_skills} skill(s) validated")


if __name__ == "__main__":
    raise SystemExit(main())
