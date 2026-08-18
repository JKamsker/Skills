<#
.SYNOPSIS
    Edit a Gitea pull request's title, body, base, or state.
.DESCRIPTION
    Updates an existing pull request without hand-building JSON. Point -BodyPath
    at a markdown file to replace the description, or pass -Body/-Title inline.
    Only the fields you pass are sent, so updating the body leaves the title
    untouched. When -Number is omitted, the open PR for the current branch (or
    -Head) is resolved automatically. Authentication is resolved from -Token or
    GITEA_TOKEN.
.EXAMPLE
    .\scripts\gitea\Update-GiteaPullRequest.ps1 -Number 143 -BodyPath .\pr-body.md
.EXAMPLE
    .\scripts\gitea\Update-GiteaPullRequest.ps1 -Number 143 -Title 'New title' -State closed
.EXAMPLE
    # Resolve the PR from the current branch, refresh its description from a file:
    .\scripts\gitea\Update-GiteaPullRequest.ps1 -BodyPath .\pr-body.md
#>
[CmdletBinding()]
param(
    [string]$BaseUrl,
    [string]$Repository,
    [string]$Token,
    [string]$Remote = 'origin',
    [int]$Number,
    [string]$Head,
    [string]$Title,
    [string]$Body,
    [string]$BodyPath,
    [string]$Base,
    [ValidateSet('open', 'closed')][string]$State,
    [switch]$UseTea,
    [string]$TeaLogin,
    [string]$TeaWorkingDirectory,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'GiteaApi.psm1') -Force

function Write-PullRequestResult {
    param([Parameter(Mandatory)]$PullRequest)

    if ($Json) {
        $PullRequest | ConvertTo-Json -Depth 20
        return
    }

    $number = [string]$PullRequest.number
    $url = [string]$PullRequest.html_url
    if ([string]::IsNullOrWhiteSpace($url)) { $url = [string]$PullRequest.url }
    Write-Host ("Updated PR #{0}: {1}" -f $number, $url)
}

if ($PSBoundParameters.ContainsKey('Body') -and $PSBoundParameters.ContainsKey('BodyPath')) {
    throw 'Pass either -Body or -BodyPath, not both.'
}
if (-not [string]::IsNullOrWhiteSpace($BodyPath)) {
    $Body = Get-Content -LiteralPath $BodyPath -Raw -Encoding UTF8
}
$hasBody = $PSBoundParameters.ContainsKey('Body') -or -not [string]::IsNullOrWhiteSpace($BodyPath)

if (-not $hasBody -and
    -not $PSBoundParameters.ContainsKey('Title') -and
    -not $PSBoundParameters.ContainsKey('Base') -and
    -not $PSBoundParameters.ContainsKey('State')) {
    throw 'Nothing to update: pass at least one of -Title, -Body, -BodyPath, -Base, -State.'
}

if ($UseTea -or
    -not [string]::IsNullOrWhiteSpace($TeaLogin) -or
    -not [string]::IsNullOrWhiteSpace($TeaWorkingDirectory)) {
    throw 'The tea backend was removed from PR updates. Use -Token/GITEA_TOKEN.'
}

if ([string]::IsNullOrWhiteSpace($BaseUrl) -or [string]::IsNullOrWhiteSpace($Repository)) {
    $remoteInfo = Get-GiteaRemoteInfo -Name $Remote
    if ([string]::IsNullOrWhiteSpace($BaseUrl)) { $BaseUrl = $remoteInfo.BaseUrl }
    if ([string]::IsNullOrWhiteSpace($Repository)) { $Repository = $remoteInfo.Repository }
}

$context = Get-GiteaContext -BaseUrl $BaseUrl -Repository $Repository -Token $Token

if (-not $Number) {
    if ([string]::IsNullOrWhiteSpace($Head)) { $Head = Get-GiteaCurrentBranchName }
    if ([string]::IsNullOrWhiteSpace($Head)) {
        throw 'PR number missing: pass -Number or run from a branch checkout.'
    }
    $match = Get-GiteaPullRequestByHead -Context $context -Head $Head
    if (-not $match) { throw "No open PR found for head '$Head'. Pass -Number." }
    $Number = [int]$match.number
}

$update = @{
    Context = $context
    Number  = $Number
}
if ($PSBoundParameters.ContainsKey('Title')) { $update.Title = $Title }
if ($hasBody) { $update.Body = $Body }
if ($PSBoundParameters.ContainsKey('Base')) { $update.Base = $Base }
if ($PSBoundParameters.ContainsKey('State')) { $update.State = $State }

$pull = Update-GiteaPullRequest @update
Write-PullRequestResult -PullRequest $pull
