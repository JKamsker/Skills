<#
.SYNOPSIS
    Add a comment to a Gitea pull request.
.DESCRIPTION
    Posts a Markdown comment without hand-building JSON. Pass -BodyPath for a
    reusable Markdown file or -Body for short comments. When -Number is omitted,
    the open pull request for the current branch (or -Head) is resolved
    automatically. Authentication comes from -Token/GITEA_TOKEN, or from Git's
    credential helper when -UseGitCredential is passed.
.EXAMPLE
    .\scripts\gitea\Add-GiteaPullRequestComment.ps1 -Number 137 -BodyPath .\artifacts\pr-comment.md
.EXAMPLE
    .\scripts\gitea\Add-GiteaPullRequestComment.ps1 -UseGitCredential -Body 'Validation passed.'
#>
[CmdletBinding()]
param(
    [string]$BaseUrl,
    [string]$Repository,
    [string]$Token,
    [string]$Remote = 'origin',
    [ValidateRange(0, [int]::MaxValue)][int]$Number,
    [string]$Head,
    [string]$Body,
    [string]$BodyPath,
    [switch]$UseGitCredential,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'GiteaApi.psm1') -Force

$hasBody = $PSBoundParameters.ContainsKey('Body')
$hasBodyPath = $PSBoundParameters.ContainsKey('BodyPath')
if ($hasBody -eq $hasBodyPath) {
    throw 'Pass exactly one of -Body or -BodyPath.'
}
if ($hasBodyPath) {
    $Body = Get-Content -LiteralPath $BodyPath -Raw -Encoding UTF8
}
if ([string]::IsNullOrWhiteSpace($Body)) {
    throw 'Comment body must not be empty.'
}

$remoteInfo = $null
if ([string]::IsNullOrWhiteSpace($BaseUrl) -or
    [string]::IsNullOrWhiteSpace($Repository) -or
    ($UseGitCredential -and [string]::IsNullOrWhiteSpace($Token) -and [string]::IsNullOrWhiteSpace($env:GITEA_TOKEN))) {
    $remoteInfo = Get-GiteaRemoteInfo -Name $Remote
}
if ([string]::IsNullOrWhiteSpace($BaseUrl) -and $remoteInfo) { $BaseUrl = $remoteInfo.BaseUrl }
if ([string]::IsNullOrWhiteSpace($Repository) -and $remoteInfo) { $Repository = $remoteInfo.Repository }

if ($UseGitCredential -and [string]::IsNullOrWhiteSpace($Token) -and [string]::IsNullOrWhiteSpace($env:GITEA_TOKEN)) {
    $context = Get-GiteaGitCredentialContext -BaseUrl $BaseUrl -Repository $Repository -Remote $Remote
}
else {
    $context = Get-GiteaContext -BaseUrl $BaseUrl -Repository $Repository -Token $Token
}

if (-not $Number) {
    if ([string]::IsNullOrWhiteSpace($Head)) { $Head = Get-GiteaCurrentBranchName }
    if ([string]::IsNullOrWhiteSpace($Head)) {
        throw 'PR number missing: pass -Number or run from a branch checkout.'
    }

    $pull = Get-GiteaPullRequestByHead -Context $context -Head $Head
    if (-not $pull) { throw "No open PR found for head '$Head'. Pass -Number." }
    $Number = [int]$pull.number
}

$comment = New-GiteaIssueComment -Context $context -Number $Number -Body $Body
if ($Json) {
    $comment | ConvertTo-Json -Depth 20
    exit 0
}

$url = [string]$comment.html_url
if ([string]::IsNullOrWhiteSpace($url)) { $url = [string]$comment.url }
Write-Host ("Commented on PR #{0}: {1}" -f $Number, $url)
