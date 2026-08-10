#!/usr/bin/env bash
# Install these skills into any Agent Skills-compatible agent that reads a skills
# directory (Cursor, Gemini CLI, opencode, Claude Code personal skills, ...).
#
# Claude Code and Codex both support this repo as a plugin marketplace, which is
# simpler than cloning and needs no script:
#   /plugin marketplace add JKamsker/Skills          # Claude Code
#   codex plugin marketplace add JKamsker/Skills     # Codex
#
# Usage:
#   ./scripts/install-skills.sh [target-dir]
#
# Defaults to ~/.agents/skills, the cross-agent user-level location from the
# Agent Skills spec. Examples:
#   ./scripts/install-skills.sh ~/.claude/skills
#   ./scripts/install-skills.sh ~/.cursor/skills
#   ./scripts/install-skills.sh .agents/skills      # repo-local

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="${1:-$HOME/.agents/skills}"

mkdir -p "$TARGET"

installed=0
for skill in "$REPO_ROOT"/plugins/*/skills/*/; do
  [ -f "$skill/SKILL.md" ] || continue
  name="$(basename "$skill")"
  dest="$TARGET/$name"

  # Stage into a temp path first so an existing install is only removed once the
  # replacement exists.
  staged="$dest.tmp-$$"
  rm -rf "$staged"
  # Filesystems without symlink support fall back to a copy. Note that MSYS/Git Bash
  # `ln -s` silently copies and still exits 0, so check the result rather than $?.
  ln -s "${skill%/}" "$staged" 2>/dev/null || cp -R "${skill%/}" "$staged"

  if [ -L "$staged" ]; then mode=linked; else mode=copied; fi
  rm -rf "$dest"
  mv "$staged" "$dest"

  if [ "$mode" = linked ]; then
    echo "linked  $name -> $dest"
  else
    echo "copied  $name -> $dest   (re-run after 'git pull' to refresh)"
  fi
  installed=$((installed + 1))
done

echo
echo "Installed $installed skill(s) into $TARGET"
