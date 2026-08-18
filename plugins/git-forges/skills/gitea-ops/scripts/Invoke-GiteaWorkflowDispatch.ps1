<#
.SYNOPSIS
    Dispatch a Gitea Actions workflow through the Gitea REST API.
.DESCRIPTION
    Triggers workflow_dispatch for a workflow file. Authentication is resolved
    from -Token or GITEA_TOKEN.
.EXAMPLE
    .\scripts\gitea\Invoke-GiteaWorkflowDispatch.ps1 -WorkflowId build.yml
.EXAMPLE
    .\scripts\gitea\Invoke-GiteaWorkflowDispatch.ps1 -Ref codex/my-branch -WorkflowInput smoke=true
#>
[CmdletBinding()]
param(
    [string]$BaseUrl,
    [string]$Repository,
    [string]$Token,
    [string]$Remote = 'origin',
    [string]$WorkflowId = 'build.yml',
    [string]$Ref,
    [Alias('Input')]
    [string[]]$WorkflowInput = @(),
    [string]$TeaLogin,
    [string]$TeaWorkingDirectory
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'GiteaApi.psm1') -Force

function ConvertTo-WorkflowInputMap {
    param([string[]]$Entries)

    $map = @{}
    foreach ($entry in @($Entries)) {
        if ([string]::IsNullOrWhiteSpace($entry)) { continue }
        $separator = $entry.IndexOf('=')
        if ($separator -lt 1) {
            throw "Workflow input must be key=value, got '$entry'."
        }

        $key = $entry.Substring(0, $separator)
        $value = $entry.Substring($separator + 1)
        $map[$key] = $value
    }
    $map
}

if (-not [string]::IsNullOrWhiteSpace($TeaLogin) -or
    -not [string]::IsNullOrWhiteSpace($TeaWorkingDirectory)) {
    throw 'The tea backend was removed from workflow dispatch. Use -Token/GITEA_TOKEN.'
}

if ([string]::IsNullOrWhiteSpace($BaseUrl) -or [string]::IsNullOrWhiteSpace($Repository)) {
    $remoteInfo = Get-GiteaRemoteInfo -Name $Remote
    if ([string]::IsNullOrWhiteSpace($BaseUrl)) { $BaseUrl = $remoteInfo.BaseUrl }
    if ([string]::IsNullOrWhiteSpace($Repository)) { $Repository = $remoteInfo.Repository }
}

if ([string]::IsNullOrWhiteSpace($Ref)) {
    $Ref = Get-GiteaCurrentBranchName
}
if ([string]::IsNullOrWhiteSpace($Ref)) {
    throw 'Ref missing: pass -Ref or run from a branch checkout.'
}

$context = Get-GiteaContext -BaseUrl $BaseUrl -Repository $Repository -Token $Token
Invoke-GiteaWorkflowDispatch `
    -Context $context `
    -WorkflowId $WorkflowId `
    -Ref $Ref `
    -Inputs (ConvertTo-WorkflowInputMap -Entries $WorkflowInput)

Write-Host ("Dispatched workflow '{0}' on ref '{1}' for {2}." -f $WorkflowId, $Ref, $Repository)
