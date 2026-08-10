#!/usr/bin/env bash
# Install these skills into any Agent Skills-compatible agent that reads a skills
# directory (Codex, Cursor, Gemini CLI, opencode, Claude Code personal skills, ...).
#
# Claude Code users should prefer the plugin marketplace instead:
#   /plugin marketplace add JKamsker/Skills
#
# Usage:
#   ./scripts/install-skills.sh [target-dir]
#
# Defaults to ~/.codex/skills. Examples:
#   ./scripts/install-skills.sh ~/.claude/skills
#   ./scripts/install-skills.sh ~/.cursor/skills

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="${1:-$HOME/.codex/skills}"

mkdir -p "$TARGET"

installed=0
for skill in "$REPO_ROOT"/plugins/*/skills/*/; do
  [ -f "$skill/SKILL.md" ] || continue
  name="$(basename "$skill")"
  dest="$TARGET/$name"

  rm -rf "$dest"
  # Filesystems without symlink support fall back to a copy. Note that MSYS/Git Bash
  # `ln -s` silently copies and still exits 0, so check the result rather than $?.
  ln -s "${skill%/}" "$dest" 2>/dev/null || cp -R "${skill%/}" "$dest"
  if [ -L "$dest" ]; then
    echo "linked  $name -> $dest"
  else
    echo "copied  $name -> $dest   (re-run after 'git pull' to refresh)"
  fi
  installed=$((installed + 1))
done

echo
echo "Installed $installed skill(s) into $TARGET"
