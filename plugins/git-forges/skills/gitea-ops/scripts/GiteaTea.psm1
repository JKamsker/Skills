# GiteaTea.psm1 - worktree-safe wrappers around the tea CLI.

function Test-GiteaTeaAvailable {
    [bool](Get-Command tea -ErrorAction SilentlyContinue)
}

function Get-GiteaRemoteInfo {
    param([Parameter(Mandatory)][string]$Name)

    $url = (& git remote get-url $Name 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($url)) {
        throw "Could not read git remote '$Name'. Pass -BaseUrl and -Repository explicitly."
    }

    if ($url -notmatch '^https?://') {
        throw "Remote '$Name' is not an HTTPS Gitea URL: $url. Pass -BaseUrl and -Repository explicitly."
    }

    $uri = [System.Uri]$url
    $segments = @($uri.AbsolutePath.Trim('/') -split '/' | Where-Object { $_ })
    if ($segments.Count -lt 2) {
        throw "Remote '$Name' does not look like a repository URL: $url"
    }

    $repoName = $segments[$segments.Count - 1] -replace '\.git$', ''
    $owner = $segments[$segments.Count - 2]
    $basePath = ''
    if ($segments.Count -gt 2) {
        $basePath = '/' + (($segments[0..($segments.Count - 3)]) -join '/')
    }

    [pscustomobject]@{
        BaseUrl    = ('{0}://{1}{2}' -f $uri.Scheme, $uri.Authority, $basePath).TrimEnd('/')
        Repository = ('{0}/{1}' -f $owner, $repoName)
        Protocol   = $uri.Scheme
        Host       = $uri.Host
        Path       = $uri.AbsolutePath.TrimStart('/')
    }
}

function Get-GiteaCurrentBranchName {
    $name = (& git branch --show-current 2>$null)
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($name)) {
        return $name.Trim()
    }
}

function Get-GiteaMainWorktreePath {
    $lines = @(& git worktree list --porcelain 2>$null)
    if ($LASTEXITCODE -ne 0 -or $lines.Count -eq 0) { return }

    $paths = @()
    foreach ($line in $lines) {
        if ($line -like 'worktree *') {
            $paths += $line.Substring('worktree '.Length)
        }
    }

    foreach ($path in $paths) {
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        if (Test-Path -LiteralPath (Join-Path $path '.git') -PathType Container) {
            return $path
        }
    }

    if ($paths.Count -gt 0) { return $paths[0] }
}

function Get-GiteaTeaWorkDirectory {
    param([string]$RequestedDirectory)

    if (-not [string]::IsNullOrWhiteSpace($RequestedDirectory)) {
        return $RequestedDirectory
    }

    $mainWorktree = Get-GiteaMainWorktreePath
    if (-not [string]::IsNullOrWhiteSpace($mainWorktree)) {
        return $mainWorktree
    }

    $top = (& git rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($top)) {
        return $top.Trim()
    }

    (Get-Location).Path
}

function Get-GiteaTeaConfigPath {
    param([string]$ConfigPath)

    if (-not [string]::IsNullOrWhiteSpace($ConfigPath)) {
        return $ConfigPath
    }

    $configRoot = $env:XDG_CONFIG_HOME
    if ([string]::IsNullOrWhiteSpace($configRoot)) {
        $configRoot = Join-Path $HOME '.config'
    }
    Join-Path (Join-Path $configRoot 'tea') 'config.yml'
}

function ConvertFrom-GiteaTeaScalar {
    param([string]$Value)

    if ($null -eq $Value) { return '' }
    $text = $Value.Trim()
    if ($text.Length -ge 2) {
        $first = $text[0]
        $last = $text[$text.Length - 1]
        if (($first -eq '"' -and $last -eq '"') -or ($first -eq "'" -and $last -eq "'")) {
            return $text.Substring(1, $text.Length - 2)
        }
    }
    $text
}

function Get-GiteaTeaLogins {
    param([string]$ConfigPath)

    $path = Get-GiteaTeaConfigPath -ConfigPath $ConfigPath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "tea config was not found: $path"
    }

    $logins = @()
    $current = $null
    $inLogins = $false
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\s*logins:\s*$') {
            $inLogins = $true
            continue
        }
        if (-not $inLogins) { continue }
        if ($line -match '^\S') { break }

        if ($line -match '^\s*-\s+name:\s*(.*)$') {
            if ($current) { $logins += [pscustomobject]$current }
            $current = [ordered]@{ Name = ConvertFrom-GiteaTeaScalar $Matches[1] }
            continue
        }

        if ($null -eq $current) { continue }
        if ($line -match '^\s+([A-Za-z_][A-Za-z0-9_-]*):\s*(.*)$') {
            $key = $Matches[1]
            $value = ConvertFrom-GiteaTeaScalar $Matches[2]
            switch ($key) {
                'url' { $current.Url = $value; break }
                'token' { $current.Token = $value; break }
                'default' { $current.Default = ($value -eq 'true'); break }
                'ssh_host' { $current.SshHost = $value; break }
                'user' { $current.User = $value; break }
            }
        }
    }
    if ($current) { $logins += [pscustomobject]$current }
    $logins
}

function Get-GiteaTeaLogin {
    param(
        [string]$Name,
        [string]$BaseUrl,
        [string]$ConfigPath
    )

    $logins = @(Get-GiteaTeaLogins -ConfigPath $ConfigPath)
    if ($logins.Count -eq 0) { throw 'No tea logins are configured.' }

    if (-not [string]::IsNullOrWhiteSpace($Name)) {
        $match = @($logins | Where-Object { $_.Name -eq $Name })
        if ($match.Count -eq 0) { throw "tea login '$Name' was not found." }
        return $match[0]
    }

    if (-not [string]::IsNullOrWhiteSpace($BaseUrl)) {
        $base = $BaseUrl.TrimEnd('/')
        $match = @($logins | Where-Object { $_.Url -and $_.Url.TrimEnd('/') -eq $base })
        if ($match.Count -gt 0) { return $match[0] }
    }

    $default = @($logins | Where-Object { $_.Default })
    if ($default.Count -gt 0) { return $default[0] }
    $logins[0]
}

function Approve-GiteaGitCredential {
    param(
        [Parameter(Mandatory)]$RemoteInfo,
        [Parameter(Mandatory)][string]$Username,
        [Parameter(Mandatory)][string]$Token
    )

    $records = @(
        "protocol=$($RemoteInfo.Protocol)`nhost=$($RemoteInfo.Host)`nusername=$Username`npassword=$Token`n`n",
        "protocol=$($RemoteInfo.Protocol)`nhost=$($RemoteInfo.Host)`npath=$($RemoteInfo.Path)`nusername=$Username`npassword=$Token`n`n"
    )

    foreach ($record in $records) {
        $null = $record | git credential approve
        if ($LASTEXITCODE -ne 0) {
            throw 'git credential approve failed.'
        }
    }
}

function Resolve-GiteaTeaContext {
    param(
        [string]$Repository,
        [string]$Remote = 'origin',
        [string]$Ref,
        [string]$Login,
        [string]$WorkingDirectory,
        [switch]$RequireRef
    )

    if (-not $Repository -and $env:GITEA_REPO) {
        $Repository = $env:GITEA_REPO
    }
    if (-not $Repository) {
        $Repository = (Get-GiteaRemoteInfo -Name $Remote).Repository
    }

    $parts = $Repository.Trim('/').Split('/')
    if ($parts.Count -ne 2) {
        throw "Repository must be 'owner/repo', got '$Repository'."
    }
    $Repository = ('{0}/{1}' -f $parts[0], $parts[1])

    if (-not $Ref) {
        $Ref = Get-GiteaCurrentBranchName
    }
    if ($RequireRef -and -not $Ref) {
        throw 'Ref missing: pass -Ref or run from a branch checkout.'
    }

    $resolvedDirectory = Get-GiteaTeaWorkDirectory -RequestedDirectory $WorkingDirectory
    if (-not (Test-Path -LiteralPath $resolvedDirectory -PathType Container)) {
        throw "tea working directory does not exist: $resolvedDirectory"
    }

    [pscustomobject]@{
        Repository       = $Repository
        Ref              = $Ref
        Login            = $Login
        WorkingDirectory = $resolvedDirectory
    }
}

function Invoke-GiteaTeaApiText {
    param(
        [Parameter(Mandatory)][string]$ApiPath,
        [Parameter(Mandatory)][string]$RepoSlug,
        [string]$Login,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [ValidateSet('GET', 'POST', 'PATCH', 'PUT', 'DELETE')][string]$Method = 'GET',
        [string]$Body
    )

    if (-not (Test-GiteaTeaAvailable)) {
        throw 'tea CLI was not found on PATH.'
    }

    $args = @('api')
    if ($Method -ne 'GET') {
        $args += @('--method', $Method)
    }
    if (-not [string]::IsNullOrWhiteSpace($Body)) {
        $args += @('--data', $Body)
    }
    if (-not [string]::IsNullOrWhiteSpace($Login)) {
        $args += @('--login', $Login)
    }
    $args += @('--repo', $RepoSlug, $ApiPath)

    Push-Location -LiteralPath $WorkingDirectory
    try {
        $output = & tea @args 2>&1
        $exitCode = $LASTEXITCODE
    } finally {
        Pop-Location
    }

    $text = ($output | Out-String).Trim()
    if ($exitCode -ne 0) {
        if ([string]::IsNullOrWhiteSpace($text)) { $text = "exit code $exitCode" }
        throw "tea api failed for '$ApiPath': $text"
    }

    $text
}

function Invoke-GiteaTeaApiJson {
    param(
        [Parameter(Mandatory)][string]$ApiPath,
        [Parameter(Mandatory)][string]$RepoSlug,
        [string]$Login,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [ValidateSet('GET', 'POST', 'PATCH', 'PUT', 'DELETE')][string]$Method = 'GET',
        [string]$Body
    )

    $text = Invoke-GiteaTeaApiText `
        -ApiPath $ApiPath `
        -RepoSlug $RepoSlug `
        -Login $Login `
        -WorkingDirectory $WorkingDirectory `
        -Method $Method `
        -Body $Body

    if ([string]::IsNullOrWhiteSpace($text)) { return $null }
    $text | ConvertFrom-Json
}

function Get-GiteaTeaActionTasks {
    param(
        [Parameter(Mandatory)][string]$RepoSlug,
        [string]$Login,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [int]$Limit
    )

    $response = Invoke-GiteaTeaApiJson `
        -ApiPath "/repos/$RepoSlug/actions/tasks?limit=$Limit" `
        -RepoSlug $RepoSlug `
        -Login $Login `
        -WorkingDirectory $WorkingDirectory
    if ($response -and $response.PSObject.Properties['workflow_runs']) {
        return @($response.workflow_runs)
    }
    @()
}

function Get-GiteaTeaPullRequests {
    param(
        [Parameter(Mandatory)][string]$RepoSlug,
        [string]$Login,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [ValidateSet('open', 'closed', 'all')][string]$State = 'open',
        [int]$Limit = 50
    )

    @(Invoke-GiteaTeaApiJson `
        -ApiPath "/repos/$RepoSlug/pulls?state=$State&limit=$Limit" `
        -RepoSlug $RepoSlug `
        -Login $Login `
        -WorkingDirectory $WorkingDirectory)
}

function Get-GiteaTeaPullRequest {
    param(
        [Parameter(Mandatory)][string]$RepoSlug,
        [string]$Login,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][int]$Number
    )

    Invoke-GiteaTeaApiJson `
        -ApiPath "/repos/$RepoSlug/pulls/$Number" `
        -RepoSlug $RepoSlug `
        -Login $Login `
        -WorkingDirectory $WorkingDirectory
}

function Update-GiteaTeaPullRequest {
    param(
        [Parameter(Mandatory)][string]$RepoSlug,
        [string]$Login,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][int]$Number,
        [string]$Title,
        [string]$Body,
        [string]$Base,
        [ValidateSet('open', 'closed')][string]$State
    )

    $payload = @{}
    if ($PSBoundParameters.ContainsKey('Title')) { $payload.title = $Title }
    if ($PSBoundParameters.ContainsKey('Body')) { $payload.body = $Body }
    if ($PSBoundParameters.ContainsKey('Base')) { $payload.base = $Base }
    if ($PSBoundParameters.ContainsKey('State')) { $payload.state = $State }
    if ($payload.Count -eq 0) {
        throw 'Nothing to update: pass at least one of -Title, -Body, -Base, -State.'
    }

    $json = $payload | ConvertTo-Json -Depth 10 -Compress
    Invoke-GiteaTeaApiJson `
        -ApiPath "/repos/$RepoSlug/pulls/$Number" `
        -RepoSlug $RepoSlug `
        -Login $Login `
        -WorkingDirectory $WorkingDirectory `
        -Method PATCH `
        -Body $json
}

function New-GiteaTeaPullRequest {
    param(
        [Parameter(Mandatory)][string]$RepoSlug,
        [string]$Login,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][string]$Head,
        [Parameter(Mandatory)][string]$Base,
        [Parameter(Mandatory)][string]$Title,
        [string]$Body = '',
        [switch]$Draft
    )

    $payload = @{
        head  = $Head
        base  = $Base
        title = $Title
        body  = $Body
    }
    if ($Draft) { $payload.draft = $true }

    $json = $payload | ConvertTo-Json -Depth 10 -Compress
    Invoke-GiteaTeaApiJson `
        -ApiPath "/repos/$RepoSlug/pulls" `
        -RepoSlug $RepoSlug `
        -Login $Login `
        -WorkingDirectory $WorkingDirectory `
        -Method POST `
        -Body $json
}

function Get-GiteaTeaActionRunJobs {
    param(
        [Parameter(Mandatory)][string]$RepoSlug,
        [string]$Login,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)]$Run
    )

    $runIds = @()
    if ($Run.PSObject.Properties['run_number'] -and $Run.run_number) { $runIds += [string]$Run.run_number }
    if ($Run.PSObject.Properties['id'] -and $Run.id) { $runIds += [string]$Run.id }

    foreach ($runId in @($runIds | Select-Object -Unique)) {
        try {
            $response = Invoke-GiteaTeaApiJson `
                -ApiPath "/repos/$RepoSlug/actions/runs/$runId/jobs" `
                -RepoSlug $RepoSlug `
                -Login $Login `
                -WorkingDirectory $WorkingDirectory
            if ($response -and $response.PSObject.Properties['jobs'] -and @($response.jobs).Count -gt 0) {
                return @($response.jobs)
            }
        } catch {
            Write-Verbose $_.Exception.Message
        }
    }
    @()
}

function Get-GiteaTeaActionJobs {
    param(
        [Parameter(Mandatory)][string]$RepoSlug,
        [string]$Login,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [int]$Limit = 50
    )

    $response = Invoke-GiteaTeaApiJson `
        -ApiPath "/repos/$RepoSlug/actions/jobs?limit=$Limit" `
        -RepoSlug $RepoSlug `
        -Login $Login `
        -WorkingDirectory $WorkingDirectory
    if ($response -and $response.PSObject.Properties['jobs']) {
        return @($response.jobs)
    }
    @()
}

function Get-GiteaTeaActionJobLog {
    param(
        [Parameter(Mandatory)][string]$RepoSlug,
        [string]$Login,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][long]$JobId
    )

    Invoke-GiteaTeaApiText `
        -ApiPath "/repos/$RepoSlug/actions/jobs/$JobId/logs" `
        -RepoSlug $RepoSlug `
        -Login $Login `
        -WorkingDirectory $WorkingDirectory
}

function Invoke-GiteaTeaWorkflowDispatch {
    param(
        [Parameter(Mandatory)][string]$RepoSlug,
        [string]$Login,
        [Parameter(Mandatory)][string]$WorkingDirectory,
        [Parameter(Mandatory)][string]$WorkflowId,
        [Parameter(Mandatory)][string]$Ref,
        [hashtable]$Inputs = @{}
    )

    $body = @{ ref = $Ref }
    if ($Inputs.Count -gt 0) {
        $body.inputs = $Inputs
    }

    $json = $body | ConvertTo-Json -Depth 10 -Compress
    $null = Invoke-GiteaTeaApiJson `
        -ApiPath "/repos/$RepoSlug/actions/workflows/$WorkflowId/dispatches" `
        -RepoSlug $RepoSlug `
        -Login $Login `
        -WorkingDirectory $WorkingDirectory `
        -Method POST `
        -Body $json
}

Export-ModuleMember -Function @(
    'Test-GiteaTeaAvailable',
    'Get-GiteaRemoteInfo',
    'Get-GiteaCurrentBranchName',
    'Get-GiteaTeaWorkDirectory',
    'Get-GiteaTeaConfigPath',
    'Get-GiteaTeaLogins',
    'Get-GiteaTeaLogin',
    'Approve-GiteaGitCredential',
    'Resolve-GiteaTeaContext',
    'Invoke-GiteaTeaApiText',
    'Invoke-GiteaTeaApiJson',
    'Get-GiteaTeaPullRequests',
    'Get-GiteaTeaPullRequest',
    'New-GiteaTeaPullRequest',
    'Update-GiteaTeaPullRequest',
    'Get-GiteaTeaActionTasks',
    'Get-GiteaTeaActionRunJobs',
    'Get-GiteaTeaActionJobs',
    'Get-GiteaTeaActionJobLog',
    'Invoke-GiteaTeaWorkflowDispatch'
)
