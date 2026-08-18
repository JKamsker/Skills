<#
.SYNOPSIS
    Open or reuse a Gitea pull request.
.DESCRIPTION
    Creates a pull request for the selected branch, or returns the existing open
    PR for the same head/base pair. Authentication is resolved from -Token or
    GITEA_TOKEN.
.EXAMPLE
    .\scripts\gitea\New-GiteaPullRequest.ps1 -Base dev -Title 'Gitea tooling'
.EXAMPLE
    .\scripts\gitea\New-GiteaPullRequest.ps1 -Head codex/my-branch -Base dev -Title 'My change' -BodyPath .\pr-body.md
#>
[CmdletBinding()]
param(
    [string]$BaseUrl,
    [string]$Repository,
    [string]$Token,
    [string]$Remote = 'origin',
    [string]$Head,
    [string]$Base = 'dev',
    [string]$Title,
    [string]$Body = '',
    [string]$BodyPath,
    [switch]$Draft,
    [switch]$UseTea,
    [string]$TeaLogin,
    [string]$TeaWorkingDirectory,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'GiteaApi.psm1') -Force

function Select-GiteaMatchingPullRequest {
    param(
        [object[]]$PullRequests,
        [Parameter(Mandatory)][string]$Head,
        [Parameter(Mandatory)][string]$Base
    )

    foreach ($pull in @($PullRequests)) {
        $headRef = [string]$pull.head.ref
        $headLabel = [string]$pull.head.label
        $baseRef = [string]$pull.base.ref

        $headMatches = $headRef -eq $Head -or $headLabel -eq $Head -or $headLabel.EndsWith(":$Head")
        if ($headMatches -and $baseRef -eq $Base) {
            return $pull
        }
    }
}

function Write-PullRequestResult {
    param(
        [Parameter(Mandatory)]$PullRequest,
        [Parameter(Mandatory)][bool]$Created
    )

    if ($Json) {
        [pscustomobject]@{
            created = $Created
            pull_request = $PullRequest
        } | ConvertTo-Json -Depth 20
        return
    }

    $action = if ($Created) { 'Created' } else { 'Reused' }
    $number = [string]$PullRequest.number
    $url = [string]$PullRequest.html_url
    if ([string]::IsNullOrWhiteSpace($url)) { $url = [string]$PullRequest.url }
    Write-Host ("{0} PR #{1}: {2}" -f $action, $number, $url)
}

if (-not [string]::IsNullOrWhiteSpace($BodyPath)) {
    if (-not [string]::IsNullOrWhiteSpace($Body)) {
        throw 'Pass either -Body or -BodyPath, not both.'
    }
    $Body = Get-Content -LiteralPath $BodyPath -Raw -Encoding UTF8
}

if ($UseTea -or
    -not [string]::IsNullOrWhiteSpace($TeaLogin) -or
    -not [string]::IsNullOrWhiteSpace($TeaWorkingDirectory)) {
    throw 'The tea backend was removed from PR creation. Use -Token/GITEA_TOKEN.'
}

if ([string]::IsNullOrWhiteSpace($BaseUrl) -or [string]::IsNullOrWhiteSpace($Repository)) {
    $remoteInfo = Get-GiteaRemoteInfo -Name $Remote
    if ([string]::IsNullOrWhiteSpace($BaseUrl)) { $BaseUrl = $remoteInfo.BaseUrl }
    if ([string]::IsNullOrWhiteSpace($Repository)) { $Repository = $remoteInfo.Repository }
}
if ([string]::IsNullOrWhiteSpace($Head)) {
    $Head = Get-GiteaCurrentBranchName
}
if ([string]::IsNullOrWhiteSpace($Head)) {
    throw 'Head branch missing: pass -Head or run from a branch checkout.'
}
if ([string]::IsNullOrWhiteSpace($Title)) { $Title = $Head }

$context = Get-GiteaContext -BaseUrl $BaseUrl -Repository $Repository -Token $Token
$existing = Select-GiteaMatchingPullRequest `
    -PullRequests (Get-GiteaPullRequests -Context $context) `
    -Head $Head `
    -Base $Base

if ($existing) {
    Write-PullRequestResult -PullRequest $existing -Created $false
    exit 0
}

$pull = New-GiteaPullRequest `
    -Context $context `
    -Head $Head `
    -Base $Base `
    -Title $Title `
    -Body $Body `
    -Draft:$Draft
Write-PullRequestResult -PullRequest $pull -Created $true
