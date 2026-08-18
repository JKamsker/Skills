<#
.SYNOPSIS
    Show recent failing Gitea Actions runs, jobs, failed steps, and optional log tails.
.DESCRIPTION
    Uses the shared Gitea REST wrapper to inspect repository Actions. BaseUrl
    and Repository can be passed explicitly or inferred from a HTTPS git remote.
    Token is read from -Token/GITEA_TOKEN, or HTTP Basic auth is built from git
    credential helper only when -UseGitCredential is supplied. Credential values
    are never printed.
.EXAMPLE
    .\scripts\gitea\Get-GiteaActionFailures.ps1 -Branch codex/my-branch -IncludeLogs
.EXAMPLE
    .\scripts\gitea\Get-GiteaActionFailures.ps1 -UseGitCredential -Limit 3 -LogTail 80
#>
[CmdletBinding()]
param(
    [string]$BaseUrl,
    [string]$Repository,
    [string]$Token,
    [string]$Remote = 'origin',
    [string]$Branch,
    [string]$HeadSha,
    [string]$Status = 'failure',
    [int]$Limit = 5,
    [int]$LogTail = 120,
    [switch]$IncludeLogs,
    [switch]$UseGitCredential,
    [switch]$UseTea,
    [string]$TeaLogin,
    [string]$TeaWorkingDirectory
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'GiteaApi.psm1') -Force

function Write-LogTail {
    param(
        [Parameter(Mandatory)][string]$Log,
        [Parameter(Mandatory)][int]$LineCount
    )

    $lines = @($Log -split "`r?`n")
    $start = [Math]::Max(0, $lines.Count - $LineCount)
    for ($i = $start; $i -lt $lines.Count; $i++) {
        $lines[$i]
    }
}

function ConvertTo-DateTimeOffsetOrNull {
    param($Value)

    if ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value)) { return $null }
    try {
        return [System.DateTimeOffset]::Parse([string]$Value)
    } catch {
        return $null
    }
}

function Format-RunTiming {
    param([Parameter(Mandatory)]$Run)

    $started = ConvertTo-DateTimeOffsetOrNull $Run.run_started_at
    if (-not $started) { $started = ConvertTo-DateTimeOffsetOrNull $Run.created_at }
    $updated = ConvertTo-DateTimeOffsetOrNull $Run.updated_at

    $parts = @()
    if ($started) { $parts += ('started={0:u}' -f $started.UtcDateTime) }
    if ($updated) { $parts += ('updated={0:u}' -f $updated.UtcDateTime) }
    if ($started -and $updated) {
        $parts += ('duration={0:n0}s' -f ($updated - $started).TotalSeconds)
    }

    if ($parts.Count -eq 0) { return '' }
    $parts -join ' '
}

function Resolve-ActionQueryState {
    param(
        [string]$BaseUrl,
        [string]$Repository,
        [string]$Token,
        [string]$Remote,
        [string]$Branch,
        [string]$HeadSha,
        [switch]$UseGitCredential,
        [switch]$UseTea,
        [string]$TeaLogin,
        [string]$TeaWorkingDirectory
    )

    if (-not $BaseUrl -and $env:GITEA_URL) { $BaseUrl = $env:GITEA_URL }
    if (-not $Repository -and $env:GITEA_REPO) { $Repository = $env:GITEA_REPO }
    if ($UseTea -or
        -not [string]::IsNullOrWhiteSpace($TeaLogin) -or
        -not [string]::IsNullOrWhiteSpace($TeaWorkingDirectory)) {
        throw 'The tea backend was removed from Actions diagnostics. Use -Token/GITEA_TOKEN or -UseGitCredential.'
    }

    $needsRemoteInfo = -not $Repository -or -not $BaseUrl -or ($UseGitCredential -and -not $Token -and -not $env:GITEA_TOKEN)
    $remoteInfo = $null
    if ($needsRemoteInfo) {
        $remoteInfo = Get-GiteaRemoteInfo -Name $Remote
    }

    if (-not $BaseUrl -and $remoteInfo) { $BaseUrl = $remoteInfo.BaseUrl }
    if (-not $Repository -and $remoteInfo) { $Repository = $remoteInfo.Repository }
    if (-not $Repository) { throw "Repository missing: pass -Repository 'owner/repo' or set GITEA_REPO." }
    if (-not $Branch -and -not $HeadSha) { $Branch = Get-GiteaCurrentBranchName }

    $context = $null
    if (-not $Token -and $UseGitCredential -and -not $env:GITEA_TOKEN) {
        $context = Get-GiteaGitCredentialContext `
            -BaseUrl $BaseUrl `
            -Repository $Repository `
            -Remote $Remote
    } else {
        $context = Get-GiteaContext -BaseUrl $BaseUrl -Repository $Repository -Token $Token
    }

    [pscustomobject]@{
        BaseUrl      = $BaseUrl
        Repository   = $Repository
        Branch       = $Branch
        HeadSha      = $HeadSha
        Context      = $context
    }
}

function Get-ActionRunsForState {
    param(
        [Parameter(Mandatory)]$State,
        [string]$Status,
        [int]$Limit
    )

    @(Get-GiteaActionRuns -Context $State.Context -Branch $State.Branch -HeadSha $State.HeadSha -Status $Status -Limit $Limit)
}

function Get-ActionJobsForRun {
    param(
        [Parameter(Mandatory)]$State,
        [Parameter(Mandatory)]$Run
    )

    @(Get-GiteaActionRunJobs -Context $State.Context -RunId ([long]$Run.id))
}

function Get-ActionJobLogForState {
    param(
        [Parameter(Mandatory)]$State,
        [Parameter(Mandatory)]$Job
    )

    Get-GiteaActionJobLog -Context $State.Context -JobId ([long]$Job.id)
}

$state = Resolve-ActionQueryState `
    -BaseUrl $BaseUrl `
    -Repository $Repository `
    -Token $Token `
    -Remote $Remote `
    -Branch $Branch `
    -HeadSha $HeadSha `
    -UseGitCredential:$UseGitCredential `
    -UseTea:$UseTea `
    -TeaLogin $TeaLogin `
    -TeaWorkingDirectory $TeaWorkingDirectory

$runs = @(Get-ActionRunsForState -State $state -Status $Status -Limit $Limit)

if ($runs.Count -eq 0) {
    Write-Host "No Actions runs matched status='$Status' branch='$($state.Branch)' head_sha='$($state.HeadSha)'." -ForegroundColor Yellow
    exit 0
}

foreach ($run in $runs) {
    $shortSha = [string]$run.head_sha
    if ($shortSha.Length -gt 12) { $shortSha = $shortSha.Substring(0, 12) }

    Write-Host ("run #{0} id={1} {2}/{3} branch={4} sha={5} title={6}" -f `
        $run.run_number, $run.id, $run.status, $run.conclusion, $run.head_branch, $shortSha, $run.display_title) -ForegroundColor Cyan
    if ($run.html_url) {
        Write-Host ("  {0}" -f $run.html_url)
    } elseif ($run.url) {
        Write-Host ("  {0}" -f $run.url)
    }
    $timing = Format-RunTiming -Run $run
    if (-not [string]::IsNullOrWhiteSpace($timing)) {
        Write-Host ("  {0}" -f $timing)
    }

    $jobs = @(Get-ActionJobsForRun -State $state -Run $run)
    $problemJobs = @($jobs | Where-Object { $_.conclusion -and $_.conclusion -ne 'success' -or $_.status -eq 'failure' })
    if ($problemJobs.Count -eq 0) { $problemJobs = $jobs }

    foreach ($job in $problemJobs) {
        Write-Host ("  job id={0} {1}/{2} name={3} runner={4}" -f $job.id, $job.status, $job.conclusion, $job.name, $job.runner_name) -ForegroundColor Magenta

        foreach ($step in @($job.steps | Where-Object { $_.conclusion -and $_.conclusion -ne 'success' -or $_.status -eq 'failure' })) {
            Write-Host ("    step {0}: {1} ({2}/{3})" -f $step.number, $step.name, $step.status, $step.conclusion) -ForegroundColor Yellow
        }

        if ($IncludeLogs) {
            Write-Host ("    --- last {0} log lines ---" -f $LogTail)
            $log = Get-ActionJobLogForState -State $state -Job $job
            Write-LogTail -Log $log -LineCount $LogTail
            Write-Host '    --- end log tail ---'
        }
    }
}
