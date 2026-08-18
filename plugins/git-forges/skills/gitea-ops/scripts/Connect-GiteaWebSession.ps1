<#
.SYNOPSIS
    Log in to Gitea's web UI and save a local web-session cookie jar.
.DESCRIPTION
    Performs the normal Gitea web login flow, including optional TOTP, and saves
    cookies outside the repository by default. Passwords and cookie values are
    never printed.
.EXAMPLE
    .\scripts\gitea\Connect-GiteaWebSession.ps1 -Username my-user
#>
[CmdletBinding()]
param(
    [string]$BaseUrl,
    [string]$Remote = 'origin',
    [string]$Username,
    [securestring]$Password,
    [string]$Otp,
    [string]$SessionPath
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'GiteaApi.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'GiteaWeb.psm1') -Force

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl = (Get-GiteaRemoteInfo -Name $Remote).BaseUrl
}

$saved = Connect-GiteaWebSession `
    -BaseUrl $BaseUrl `
    -Username $Username `
    -Password $Password `
    -Otp $Otp `
    -SessionPath $SessionPath

Write-Host ("Saved authenticated Gitea web session to {0}" -f $saved)
