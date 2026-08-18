<#
.SYNOPSIS
    Read a Gitea Actions run/job through an authenticated web session.
.DESCRIPTION
    Uses the cookie jar created by Connect-GiteaWebSession.ps1 to call the same
    web JSON endpoint used by the Gitea Actions UI. This is useful on Gitea
    1.25 when token APIs expose task ids but not job ids/logs.
.EXAMPLE
    .\scripts\gitea\Get-GiteaActionWebLog.ps1 -RunIndex 35 -ExpandAllSteps
#>
[CmdletBinding()]
param(
    [string]$BaseUrl,
    [string]$Repository,
    [string]$Remote = 'origin',
    [Parameter(Mandatory)][int]$RunIndex,
    [int]$JobIndex = 0,
    [int]$LogTail = 160,
    [switch]$ExpandAllSteps,
    [string]$SessionPath,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'GiteaApi.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'GiteaWeb.psm1') -Force

if ([string]::IsNullOrWhiteSpace($BaseUrl) -or [string]::IsNullOrWhiteSpace($Repository)) {
    $remoteInfo = Get-GiteaRemoteInfo -Name $Remote
    if ([string]::IsNullOrWhiteSpace($BaseUrl)) { $BaseUrl = $remoteInfo.BaseUrl }
    if ([string]::IsNullOrWhiteSpace($Repository)) { $Repository = $remoteInfo.Repository }
}

$session = Import-GiteaWebSession -BaseUrl $BaseUrl -Path $SessionPath
$data = Get-GiteaActionWebJob `
    -BaseUrl $BaseUrl `
    -Repository $Repository `
    -Session $session `
    -RunIndex $RunIndex `
    -JobIndex $JobIndex `
    -ExpandAllSteps:$ExpandAllSteps

if ($Json) {
    $data | ConvertTo-Json -Depth 20
} else {
    Write-GiteaActionWebJobSummary -JobData $data -LogTail $LogTail
}
