<#
.SYNOPSIS
    Install these skills into any Agent Skills-compatible agent that reads a skills directory.

.DESCRIPTION
    Works with Cursor, Gemini CLI, opencode, Claude Code personal skills, and any other
    client that follows the agentskills.io layout.

    Claude Code and Codex both support this repo as a plugin marketplace, which is simpler
    than cloning and needs no script:
        /plugin marketplace add JKamsker/Skills          # Claude Code
        codex plugin marketplace add JKamsker/Skills     # Codex

    Symlinks are used when the session is allowed to create them (Developer Mode or an
    elevated shell); otherwise the script falls back to copying, and you re-run it after
    pulling to refresh.

.PARAMETER TargetDir
    Skills directory to install into. Defaults to ~/.agents/skills, the cross-agent
    user-level location from the Agent Skills spec.

.EXAMPLE
    ./scripts/install-skills.ps1
    ./scripts/install-skills.ps1 -TargetDir ~/.claude/skills
#>
[CmdletBinding()]
param(
    [string]$TargetDir = (Join-Path $HOME '.agents/skills')
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
}

$installed = 0
Get-ChildItem -Path (Join-Path $repoRoot 'plugins') -Directory | ForEach-Object {
    $skillsRoot = Join-Path $_.FullName 'skills'
    if (-not (Test-Path $skillsRoot)) { return }

    Get-ChildItem -Path $skillsRoot -Directory | ForEach-Object {
        # Capture the pipeline item: inside a catch block $_ is rebound to the
        # ErrorRecord, so $_.FullName would be $null there.
        $skill = $_
        if (-not (Test-Path (Join-Path $skill.FullName 'SKILL.md'))) { return }

        $dest = Join-Path $TargetDir $skill.Name
        # Stage into a temp path first so an existing install is only removed once
        # the replacement exists.
        $staged = "$dest.tmp-$PID"
        if (Test-Path $staged) { Remove-Item -Recurse -Force $staged }

        $mode = 'linked'
        try {
            New-Item -ItemType SymbolicLink -Path $staged -Target $skill.FullName -ErrorAction Stop | Out-Null
        } catch {
            # Symlinks need Developer Mode or an elevated shell; fall back to a copy.
            Copy-Item -Recurse -Path $skill.FullName -Destination $staged
            $mode = 'copied'
        }

        if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
        Move-Item -Path $staged -Destination $dest

        if ($mode -eq 'linked') {
            Write-Host "linked  $($skill.Name) -> $dest"
        } else {
            Write-Host "copied  $($skill.Name) -> $dest   (re-run after 'git pull' to refresh)"
        }
        $script:installed++
    }
}

Write-Host ''
Write-Host "Installed $installed skill(s) into $TargetDir"
