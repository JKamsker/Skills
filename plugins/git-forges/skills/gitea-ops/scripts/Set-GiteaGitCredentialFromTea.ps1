<#
.SYNOPSIS
    Approve a Git HTTPS credential from a configured tea login.
.DESCRIPTION
    Reads the selected tea login from the tea config, then stores its token in
    Git's configured credential helper for the repository remote. Token values
    are never printed.
.EXAMPLE
    .\scripts\gitea\Set-GiteaGitCredentialFromTea.ps1 -TeaLogin my-login
#>
[CmdletBinding()]
param(
    [string]$TeaLogin,
    [string]$Remote = 'origin',
    [string]$Username,
    [string]$TeaConfigPath
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'GiteaTea.psm1') -Force

$remoteInfo = Get-GiteaRemoteInfo -Name $Remote
$login = Get-GiteaTeaLogin -Name $TeaLogin -BaseUrl $remoteInfo.BaseUrl -ConfigPath $TeaConfigPath

if ([string]::IsNullOrWhiteSpace($login.Token)) {
    throw "tea login '$($login.Name)' does not contain a token."
}

if ([string]::IsNullOrWhiteSpace($Username)) {
    $Username = $login.User
}
if ([string]::IsNullOrWhiteSpace($Username)) {
    $Username = $login.Name
}
if ([string]::IsNullOrWhiteSpace($Username)) {
    throw "Could not determine a Git username for tea login '$($login.Name)'. Pass -Username."
}

Approve-GiteaGitCredential -RemoteInfo $remoteInfo -Username $Username -Token $login.Token
Write-Host ("Approved Git credential for {0} as user '{1}' from tea login '{2}'." -f $remoteInfo.Host, $Username, $login.Name)
