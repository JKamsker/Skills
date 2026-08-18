<#
.SYNOPSIS
Identify which software forge serves a host: Forgejo, Gitea, GitHub, or GitLab.

.DESCRIPTION
PowerShell twin of detect-forge.sh. Works from a URL, a bare host, an ssh/scp
remote URL, or the current repo's git remote. Handles sub-path installs
(https://host/gitea/...) and REQUIRE_SIGNIN_VIEW instances (pass -Token).

Detection logic (validated against Forgejo 14/15/16, Gitea 1.25/1.27):
  1. GET {base}/api/v1/version
     200 + "+gitea-" suffix -> Forgejo; 200 + plain 1.x -> Gitea;
     401/403 -> locked, step 2; 404 -> walk the path up one segment.
  2. GET {base}/api/forgejo/v1/version — the route only exists on Forgejo:
     200/401/403 -> Forgejo, 404 -> Gitea. GitLab answers /api/v4/version.
  3. "Powered by Forgejo|Gitea" in the HTML footer confirms locked instances.

.PARAMETER InputUrl
URL, host, or host/subpath. Defaults to `git remote get-url origin`.

.PARAMETER Token
API token for locked instances. Falls back to $env:FORGE_TOKEN,
$env:GITEA_TOKEN, $env:FORGEJO_TOKEN.

.EXAMPLE
./Detect-Forge.ps1                               # detect the current repo's forge
./Detect-Forge.ps1 code.example.com/gitea -Json
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)] [string]$InputUrl,
    [string]$Token,
    [string]$Remote = 'origin',
    [int]$TimeoutSec = 10,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'

if (-not $Token) {
    $Token = $env:FORGE_TOKEN ?? $env:GITEA_TOKEN ?? $env:FORGEJO_TOKEN
}
if (-not $InputUrl) {
    $InputUrl = git remote get-url $Remote 2>$null
    if (-not $InputUrl) { Write-Error "no input URL and no git remote '$Remote' found"; exit 2 }
}

function ConvertTo-BaseUrl([string]$url) {
    $hostPart = ''; $path = ''
    if ($url -match '^ssh://(?:[^@/]+@)?([^/:]+)(?::\d+)?(/.*)?$') {
        $hostPart = $Matches[1]; $path = $Matches[2] ?? ''
    }
    elseif ($url -match '^https?://([^/]+)(/.*)?$') {
        $hostPart = $Matches[1]; $path = $Matches[2] ?? ''
    }
    elseif ($url -match '^[^@/]+@([^:]+):(.*)$') {
        $hostPart = $Matches[1]; $path = '/' + $Matches[2]
    }
    else {
        $parts = $url -split '/', 2
        $hostPart = $parts[0]; $path = if ($parts.Count -gt 1) { '/' + $parts[1] } else { '' }
    }
    $path = $path -replace '\.git$', '' -replace '/$', ''
    "https://$hostPart$path"
}

function Invoke-Probe([string]$url) {
    $headers = @{}
    if ($Token) { $headers['Authorization'] = "token $Token" }
    try {
        $resp = Invoke-WebRequest -Uri $url -Headers $headers -TimeoutSec $TimeoutSec `
            -MaximumRedirection 5 -SkipHttpErrorCheck -UseBasicParsing
        [pscustomobject]@{ Code = [int]$resp.StatusCode; Body = "$($resp.Content)"; Headers = $resp.Headers }
    } catch {
        [pscustomobject]@{ Code = 0; Body = ''; Headers = @{} }
    }
}

function Get-VersionFromBody([string]$body) {
    if ($body -match '"version"\s*:\s*"([^"]*)"') { $Matches[1] } else { '' }
}

function Get-HtmlBrand([string]$base) {
    $page = (Invoke-Probe "$base/").Body
    if ($page -match 'Powered by Forgejo' -or $page -match 'content="Forgejo') { return 'forgejo' }
    if ($page -match 'Powered by Gitea' -or $page -match 'content="Gitea') { return 'gitea' }
    ''
}

function Write-Result([string]$forge, [string]$confidence, [string]$version, [string]$apiBase, [string]$method) {
    if ($Json) {
        [ordered]@{ forge = $forge; confidence = $confidence; version = $version
                    api_base = $apiBase; method = $method } | ConvertTo-Json -Compress
    } else {
        "{0} ({1})  version={2}  api_base={3}  via={4}" -f $forge, $confidence,
            ($version ? $version : '?'), $apiBase, $method
    }
    exit ($forge -eq 'unknown' ? 1 : 0)
}

$full = ConvertTo-BaseUrl $InputUrl
$candidates = [System.Collections.Generic.List[string]]::new()
$cur = $full
while ($true) {
    $candidates.Add($cur)
    $rest = $cur.Substring(8)   # strip https://
    if ($rest -notmatch '/') { break }
    $cur = 'https://' + ($rest -replace '/[^/]*$', '')
}

foreach ($base in $candidates) {
    $r = Invoke-Probe "$base/api/v1/version"
    switch ($r.Code) {
        200 {
            $ver = Get-VersionFromBody $r.Body
            if (-not $ver) { continue }   # 200 but not version JSON (SPA fallback)
            if ($ver -match '\+gitea-') {
                Write-Result forgejo confirmed $ver $base 'api/v1/version compat suffix'
            }
            $fr = Invoke-Probe "$base/api/forgejo/v1/version"
            if ($fr.Code -eq 200) { Write-Result forgejo confirmed $ver $base 'api/forgejo/v1/version' }
            Write-Result gitea confirmed $ver $base 'api/v1/version plain version'
        }
        { $_ -in 401, 403 } {
            $fr = Invoke-Probe "$base/api/forgejo/v1/version"
            if ($fr.Code -eq 200) {
                Write-Result forgejo confirmed (Get-VersionFromBody $fr.Body) $base 'api/forgejo/v1/version (authed)'
            }
            $guess = ($fr.Code -in 401, 403) ? 'forgejo' : 'gitea'
            # Blanket 401/403 can also be GitLab or a proxy; /api/v4 disambiguates.
            $gl = Invoke-Probe "$base/api/v4/version"
            if ($gl.Code -in 200, 401) {
                Write-Result gitlab confirmed (Get-VersionFromBody $gl.Body) $base "api/v4/version status $($gl.Code)"
            }
            $brand = Get-HtmlBrand $base
            if ($brand) { Write-Result $brand confirmed '' $base 'locked api + html branding' }
            Write-Result $guess likely '' $base "locked api, forgejo-route status $($fr.Code)"
        }
    }
}

$root = $candidates[-1]
$gl = Invoke-Probe "$root/api/v4/version"
if ($gl.Code -in 200, 401) { Write-Result gitlab confirmed '' $root "api/v4/version status $($gl.Code)" }
$head = Invoke-Probe "$root/"
if ($head.Headers.Keys -match 'x-github-request-id') {
    Write-Result github confirmed '' $root 'x-github-request-id header'
}
$brand = Get-HtmlBrand $root
if ($brand) { Write-Result $brand likely '' $root 'html branding only' }
Write-Result unknown none '' $root 'no signature matched'
