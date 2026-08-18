# GiteaWeb.psm1 - web-session helpers for Gitea pages not exposed by token APIs.

function Get-GiteaWebSessionPath {
    param([string]$Path)

    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        return $Path
    }
    if (-not [string]::IsNullOrWhiteSpace($env:GITEA_WEB_SESSION_PATH)) {
        return $env:GITEA_WEB_SESSION_PATH
    }

    $configRoot = $env:XDG_CONFIG_HOME
    if ([string]::IsNullOrWhiteSpace($configRoot)) {
        $configRoot = Join-Path $HOME '.config'
    }
    Join-Path (Join-Path $configRoot 'gitea-ops') 'gitea-web-session.json'
}

function New-GiteaWebSession {
    $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
    $session
}

function Save-GiteaWebSession {
    param(
        [Parameter(Mandatory)]$Session,
        [Parameter(Mandatory)][string]$BaseUrl,
        [string]$Path
    )

    $sessionPath = Get-GiteaWebSessionPath -Path $Path
    $sessionDir = Split-Path -Parent $sessionPath
    if (-not (Test-Path -LiteralPath $sessionDir -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $sessionDir -Force
    }

    $uri = [System.Uri]$BaseUrl
    $cookies = @()
    foreach ($cookie in $Session.Cookies.GetCookies($uri)) {
        $cookies += [pscustomobject]@{
            Name    = $cookie.Name
            Value   = $cookie.Value
            Domain  = $cookie.Domain
            Path    = $cookie.Path
            Secure  = $cookie.Secure
            Expires = if ($cookie.Expires -and $cookie.Expires -ne [datetime]::MinValue) { $cookie.Expires.ToUniversalTime().ToString('o') } else { $null }
        }
    }

    [pscustomobject]@{
        BaseUrl   = $BaseUrl.TrimEnd('/')
        CreatedAt = [DateTimeOffset]::UtcNow.ToString('o')
        Cookies   = $cookies
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $sessionPath -Encoding UTF8

    if ($IsLinux -or $IsMacOS) {
        chmod 600 -- "$sessionPath" 2>$null
    }

    $sessionPath
}

function Import-GiteaWebSession {
    param(
        [string]$Path,
        [string]$BaseUrl
    )

    $sessionPath = Get-GiteaWebSessionPath -Path $Path
    if (-not (Test-Path -LiteralPath $sessionPath -PathType Leaf)) {
        throw "Gitea web session was not found: $sessionPath"
    }

    $data = Get-Content -LiteralPath $sessionPath -Raw | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
        $BaseUrl = $data.BaseUrl
    }
    if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
        throw 'BaseUrl missing in session file. Pass -BaseUrl.'
    }

    $session = New-GiteaWebSession
    foreach ($entry in @($data.Cookies)) {
        if ([string]::IsNullOrWhiteSpace([string]$entry.Name)) { continue }
        $cookie = [System.Net.Cookie]::new([string]$entry.Name, [string]$entry.Value, [string]$entry.Path, [string]$entry.Domain)
        $cookie.Secure = [bool]$entry.Secure
        if ($entry.Expires) {
            # ConvertFrom-Json may hand back a DateTime or the raw ISO-8601 string.
            # Casting to string uses the invariant culture, so parse it that way
            # too; the current culture would reject invariant dates on non-US hosts.
            $cookie.Expires = if ($entry.Expires -is [datetime]) {
                $entry.Expires.ToUniversalTime()
            }
            else {
                [DateTime]::Parse(
                    [string]$entry.Expires,
                    [Globalization.CultureInfo]::InvariantCulture,
                    [Globalization.DateTimeStyles]::RoundtripKind).ToUniversalTime()
            }
        }
        $session.Cookies.Add($cookie)
    }
    $session
}

function ConvertFrom-GiteaSecureString {
    param([Parameter(Mandatory)][securestring]$SecureString)

    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureString)
    try {
        [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    } finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Get-GiteaCsrfToken {
    param([Parameter(Mandatory)][string]$Html)

    $configMatch = [regex]::Match($Html, "csrfToken:\s*'([^']+)'")
    if ($configMatch.Success) {
        return $configMatch.Groups[1].Value
    }

    $bodyMatch = [regex]::Match($Html, '"x-csrf-token":\s*"([^"]+)"')
    if ($bodyMatch.Success) {
        return $bodyMatch.Groups[1].Value
    }

    $match = [regex]::Match($Html, 'name="_csrf"\s+value="([^"]+)"|value="([^"]+)"\s+name="_csrf"')
    if (-not $match.Success) {
        throw 'Could not find _csrf token in Gitea login page.'
    }
    if ($match.Groups[1].Success) { return $match.Groups[1].Value }
    $match.Groups[2].Value
}

function Invoke-GiteaWebRequest {
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)]$Session,
        [ValidateSet('GET', 'POST')][string]$Method = 'GET',
        [hashtable]$Form
    )

    $uri = $BaseUrl.TrimEnd('/') + '/' + $Path.TrimStart('/')
    $request = @{
        Uri             = $uri
        Method          = $Method
        WebSession      = $Session
        UseBasicParsing = $true
        ErrorAction     = 'Stop'
    }
    if ($Form) {
        $request.Body = $Form
        $request.ContentType = 'application/x-www-form-urlencoded'
    }

    $oldProgressPreference = $ProgressPreference
    $ProgressPreference = 'SilentlyContinue'
    try {
        Invoke-WebRequest @request
    } finally {
        $ProgressPreference = $oldProgressPreference
    }
}

function Invoke-GiteaWebJsonPost {
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)]$Session,
        [Parameter(Mandatory)][string]$CsrfToken,
        [Parameter(Mandatory)]$Body
    )

    $uri = $BaseUrl.TrimEnd('/') + '/' + $Path.TrimStart('/')
    $json = $Body | ConvertTo-Json -Depth 12 -Compress
    $oldProgressPreference = $ProgressPreference
    $ProgressPreference = 'SilentlyContinue'
    try {
        $response = Invoke-WebRequest `
            -Uri $uri `
            -Method POST `
            -WebSession $Session `
            -UseBasicParsing `
            -Headers @{ 'x-csrf-token' = $CsrfToken; 'content-type' = 'application/json' } `
            -Body $json `
            -ErrorAction Stop
        if ([string]::IsNullOrWhiteSpace([string]$response.Content)) { return $null }
        $response.Content | ConvertFrom-Json
    } finally {
        $ProgressPreference = $oldProgressPreference
    }
}

function Get-GiteaActionWebJob {
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$Repository,
        [Parameter(Mandatory)]$Session,
        [Parameter(Mandatory)][int]$RunIndex,
        [int]$JobIndex = 0,
        [switch]$ExpandAllSteps
    )

    $runPage = Invoke-GiteaWebRequest -BaseUrl $BaseUrl -Path "/$Repository/actions/runs/$RunIndex" -Session $Session
    $csrf = Get-GiteaCsrfToken -Html ([string]$runPage.Content)
    $path = "/$Repository/actions/runs/$RunIndex/jobs/$JobIndex"

    $initial = Invoke-GiteaWebJsonPost `
        -BaseUrl $BaseUrl `
        -Path $path `
        -Session $Session `
        -CsrfToken $csrf `
        -Body @{ logCursors = @(@{ step = 0; cursor = $null; expanded = $true }) }
    if (-not $ExpandAllSteps) { return $initial }

    $steps = @($initial.state.currentJob.steps)
    if ($steps.Count -eq 0) { return $initial }

    $cursors = @()
    for ($i = 0; $i -lt $steps.Count; $i++) {
        $cursors += @{ step = $i; cursor = $null; expanded = $true }
    }

    Invoke-GiteaWebJsonPost `
        -BaseUrl $BaseUrl `
        -Path $path `
        -Session $Session `
        -CsrfToken $csrf `
        -Body @{ logCursors = $cursors }
}

function Write-GiteaActionWebJobSummary {
    param(
        [Parameter(Mandatory)]$JobData,
        [int]$LogTail = 160
    )

    $run = $JobData.state.run
    $job = $JobData.state.currentJob
    Write-Host ("run {0} status={1} title={2}" -f $run.link, $run.status, $run.title) -ForegroundColor Cyan
    Write-Host ("job {0}: {1}" -f $job.title, $job.detail) -ForegroundColor Magenta

    for ($i = 0; $i -lt @($job.steps).Count; $i++) {
        $step = $job.steps[$i]
        Write-Host ("  step {0}: {1} ({2}, {3})" -f $i, $step.summary, $step.status, $step.duration)
    }

    foreach ($stepLog in @($JobData.logs.stepsLog)) {
        $step = $job.steps[[int]$stepLog.step]
        Write-Host ("--- step {0}: {1} last {2} lines ---" -f $stepLog.step, $step.summary, $LogTail)
        $lines = @($stepLog.lines)
        $start = [Math]::Max(0, $lines.Count - $LogTail)
        for ($i = $start; $i -lt $lines.Count; $i++) {
            $lines[$i].message
        }
        Write-Host '--- end step log ---'
    }
}

function Test-GiteaWebSession {
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)]$Session
    )

    try {
        $response = Invoke-GiteaWebRequest -BaseUrl $BaseUrl -Path '/user/settings' -Session $Session
        $title = [regex]::Match([string]$response.Content, '<title>([^<]+)</title>').Groups[1].Value
        return ($title -notmatch 'Sign In')
    } catch {
        return $false
    }
}

function Connect-GiteaWebSession {
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [string]$Username,
        [securestring]$Password,
        [string]$Otp,
        [string]$SessionPath
    )

    if ([string]::IsNullOrWhiteSpace($Username)) {
        $Username = Read-Host 'Gitea username'
    }
    if ($null -eq $Password) {
        $Password = Read-Host 'Gitea password' -AsSecureString
    }

    $session = New-GiteaWebSession
    $loginPage = Invoke-GiteaWebRequest -BaseUrl $BaseUrl -Path '/user/login' -Session $session
    $csrf = Get-GiteaCsrfToken -Html ([string]$loginPage.Content)
    $plainPassword = ConvertFrom-GiteaSecureString -SecureString $Password
    try {
        $form = @{
            _csrf    = $csrf
            user_name = $Username
            password = $plainPassword
            remember = 'on'
        }
        $response = Invoke-GiteaWebRequest -BaseUrl $BaseUrl -Path '/user/login' -Session $session -Method POST -Form $form
    } finally {
        $plainPassword = $null
    }

    if ([string]$response.Content -match 'name="passcode"') {
        if ([string]::IsNullOrWhiteSpace($Otp)) {
            $Otp = Read-Host 'Gitea two-factor code'
        }
        $csrf = Get-GiteaCsrfToken -Html ([string]$response.Content)
        $response = Invoke-GiteaWebRequest -BaseUrl $BaseUrl -Path '/user/two_factor' -Session $session -Method POST -Form @{
            _csrf   = $csrf
            passcode = $Otp
        }
    }

    if (-not (Test-GiteaWebSession -BaseUrl $BaseUrl -Session $session)) {
        throw 'Gitea web login did not produce an authenticated session.'
    }

    Save-GiteaWebSession -Session $session -BaseUrl $BaseUrl -Path $SessionPath
}

Export-ModuleMember -Function @(
    'Get-GiteaWebSessionPath',
    'New-GiteaWebSession',
    'Save-GiteaWebSession',
    'Import-GiteaWebSession',
    'Get-GiteaCsrfToken',
    'Invoke-GiteaWebRequest',
    'Invoke-GiteaWebJsonPost',
    'Get-GiteaActionWebJob',
    'Write-GiteaActionWebJobSummary',
    'Test-GiteaWebSession',
    'Connect-GiteaWebSession'
)
