# GiteaApi.psm1 - Gitea REST client + planning-doc parsers.
#
# Two halves; the first is useful on its own (see scripts/gitea/README.md):
#   1. A thin Gitea REST API wrapper (context, auth, paging, error reporting)
#      plus typed helpers: Get-Gitea{Repository,Labels,Milestones,Issues},
#      New-Gitea{Label,Milestone,Issue}, Set-GiteaLabel, Add-GiteaIssueDependency.
#      Import this module directly to read/create tracker objects ad hoc.
#   2. Parsers for the planning docs in docs/gitea (labels.md, milestones/*.md,
#      epics/*.md) so the markdown files stay the single source of truth for the
#      Sync-*.ps1 scripts.
#
# Compatible with Windows PowerShell 5.1 and PowerShell 7+. Non-ASCII characters
# (the em-dash used in the planning docs) are built from char codes so this file
# is safe to load regardless of how the host decodes it.
#
# Invoke-Gitea -AllPages follows Gitea Link pagination itself so issue sync works
# in headless shells without relying on the tea CLI or PowerShell 7-only flags.

$script:EmDash = [string][char]0x2014   # the "---" character used in doc headings

# --------------------------------------------------------------------------
# Context & REST wrapper
# --------------------------------------------------------------------------

function Get-GiteaContext {
    <#
    .SYNOPSIS
        Resolve connection settings from parameters or environment variables.
    .DESCRIPTION
        Reads -BaseUrl/-Repository/-Token, falling back to GITEA_URL,
        GITEA_REPO ("owner/repo") and GITEA_TOKEN. Returns a context object
        passed to every other function in this module.
    #>
    [CmdletBinding()]
    param(
        [string]$BaseUrl,
        [string]$Repository,
        [string]$Token
    )
    if (-not $BaseUrl)    { $BaseUrl    = $env:GITEA_URL }
    if (-not $Repository) { $Repository = $env:GITEA_REPO }
    if (-not $Token)      { $Token      = $env:GITEA_TOKEN }
    if (-not $BaseUrl)    { throw 'Gitea base URL missing: pass -BaseUrl or set GITEA_URL (e.g. https://git.example.com).' }
    if (-not $Repository) { throw "Repository missing: pass -Repository 'owner/repo' or set GITEA_REPO." }
    if (-not $Token)      { throw 'API token missing: pass -Token or set GITEA_TOKEN (needs repository access; write operations need their matching write scope).' }

    $parts = $Repository.Trim('/').Split('/')
    if ($parts.Count -ne 2) { throw "Repository must be 'owner/repo', got '$Repository'." }

    [pscustomobject]@{
        BaseUrl = $BaseUrl.TrimEnd('/')
        ApiBase = $BaseUrl.TrimEnd('/') + '/api/v1'
        Owner   = $parts[0]
        Repo    = $parts[1]
        Headers = @{ Authorization = "token $Token" }
    }
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

function Get-GiteaGitCredentialContext {
    <#
    .SYNOPSIS
        Build a Gitea API context from the configured Git credential helper.
    .DESCRIPTION
        Resolves the server and repository from an HTTPS remote, asks Git's
        credential helper for that remote's credential, and returns a normal
        Gitea API context using HTTP Basic authentication. Credential values are
        held only in memory and are never written to output.
    #>
    [CmdletBinding()]
    param(
        [string]$BaseUrl,
        [string]$Repository,
        [string]$Remote = 'origin'
    )

    if (-not $BaseUrl) { $BaseUrl = $env:GITEA_URL }
    if (-not $Repository) { $Repository = $env:GITEA_REPO }

    $remoteInfo = Get-GiteaRemoteInfo -Name $Remote
    if (-not $BaseUrl) { $BaseUrl = $remoteInfo.BaseUrl }
    if (-not $Repository) { $Repository = $remoteInfo.Repository }

    $parts = $Repository.Trim('/').Split('/')
    if ($parts.Count -ne 2) { throw "Repository must be 'owner/repo', got '$Repository'." }

    $credentialInput = "protocol=$($remoteInfo.Protocol)`nhost=$($remoteInfo.Host)`npath=$($remoteInfo.Path)`n`n"
    $filled = @($credentialInput | git credential fill)
    if ($LASTEXITCODE -ne 0) { throw 'git credential fill failed.' }

    $username = ''
    $password = ''
    foreach ($line in $filled) {
        if ($line -like 'username=*') {
            $username = $line.Substring('username='.Length)
        }
        elseif ($line -like 'password=*') {
            $password = $line.Substring('password='.Length)
        }
    }

    if ([string]::IsNullOrWhiteSpace($username) -or [string]::IsNullOrWhiteSpace($password)) {
        throw 'git credential helper did not return a username and password/token.'
    }

    $basic = [Convert]::ToBase64String(
        [System.Text.Encoding]::UTF8.GetBytes(('{0}:{1}' -f $username, $password)))

    [pscustomobject]@{
        BaseUrl = $BaseUrl.TrimEnd('/')
        ApiBase = $BaseUrl.TrimEnd('/') + '/api/v1'
        Owner   = $parts[0]
        Repo    = $parts[1]
        Headers = @{ Authorization = "Basic $basic" }
    }
}

function Get-GiteaCurrentBranchName {
    $name = (& git branch --show-current 2>$null)
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($name)) {
        return $name.Trim()
    }
}

function ConvertTo-GiteaPageItems {
    param($Value)

    if ($null -eq $Value) { return }

    if ($Value -is [System.Array]) {
        foreach ($item in $Value) {
            if ($item -is [System.Array]) {
                foreach ($nested in $item) { $nested }
            } else {
                $item
            }
        }
        return
    }

    $Value
}

function Get-GiteaResponseHeader {
    param(
        [Parameter(Mandatory)]$Response,
        [Parameter(Mandatory)][string]$Name
    )

    if (-not $Response.Headers) { return }

    foreach ($key in $Response.Headers.Keys) {
        if (-not [string]::Equals([string]$key, $Name, [System.StringComparison]::OrdinalIgnoreCase)) {
            continue
        }

        $value = $Response.Headers[$key]
        if ($value -is [System.Collections.IEnumerable] -and $value -isnot [string]) {
            foreach ($entry in $value) { [string]$entry }
        } else {
            [string]$value
        }
        return
    }
}

function Resolve-GiteaLinkUri {
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$Uri
    )

    if ($Uri -match '^https?://') { return $Uri }

    $base = New-Object System.Uri ($Context.BaseUrl + '/')
    $resolved = New-Object System.Uri $base, $Uri
    $resolved.AbsoluteUri
}

function Get-GiteaNextLink {
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)]$Response
    )

    foreach ($link in @(Get-GiteaResponseHeader -Response $Response -Name 'Link')) {
        foreach ($match in [regex]::Matches([string]$link, '<([^>]+)>;\s*[^,]*rel="?next"?')) {
            return (Resolve-GiteaLinkUri -Context $Context -Uri $match.Groups[1].Value)
        }
    }
}

function Invoke-Gitea {
    <#
    .SYNOPSIS
        Call the Gitea API. Handles auth, JSON, UTF-8 bodies, paging and errors.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $Context,
        [ValidateSet('GET','POST','PATCH','PUT','DELETE')] [string]$Method = 'GET',
        [Parameter(Mandatory)] [string]$Path,   # path under /api/v1, e.g. /repos/o/r/labels
        $Body,
        [switch]$AllPages
    )
    $baseUri = $Context.ApiBase + $Path

    $invoke = {
        param($uri)
        $req = @{
            Method      = $Method
            Uri         = $uri
            Headers     = $Context.Headers
            ErrorAction = 'Stop'
        }
        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 10
            # Send explicit UTF-8 bytes: PS 5.1 otherwise encodes string bodies
            # as ISO-8859-1 and mangles the em-dashes in issue titles/bodies.
            $req.Body        = [System.Text.Encoding]::UTF8.GetBytes($json)
            $req.ContentType = 'application/json; charset=utf-8'
        }
        $oldProgressPreference = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'
        try {
            Invoke-RestMethod @req
        } finally {
            $ProgressPreference = $oldProgressPreference
        }
    }

    $invokePage = {
        param($uri)
        $req = @{
            Method          = $Method
            Uri             = $uri
            Headers         = $Context.Headers
            ErrorAction     = 'Stop'
            UseBasicParsing = $true
        }
        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 10
            $req.Body        = [System.Text.Encoding]::UTF8.GetBytes($json)
            $req.ContentType = 'application/json; charset=utf-8'
        }
        $oldProgressPreference = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'
        try {
            Invoke-WebRequest @req
        } finally {
            $ProgressPreference = $oldProgressPreference
        }
    }

    try {
        if (-not $AllPages) {
            return & $invoke $baseUri
        }
        $sep = '?'
        if ($baseUri.Contains('?')) { $sep = '&' }
        $limit = 50
        $page = 1
        $all = @()
        $nextUri = $null
        while ($true) {
            if ($nextUri) {
                $uri = $nextUri
            } else {
                $uri = "{0}{1}limit={2}&page={3}" -f $baseUri, $sep, $limit, $page
            }

            $response = & $invokePage $uri
            $content = [string]$response.Content
            $parsed = $null
            if (-not [string]::IsNullOrWhiteSpace($content)) {
                $parsed = $content | ConvertFrom-Json
            }

            $batch = @(ConvertTo-GiteaPageItems -Value $parsed)
            $all += $batch
            $nextUri = Get-GiteaNextLink -Context $Context -Response $response
            if ($nextUri) {
                $page++
                continue
            }
            if ($batch.Count -lt $limit) { break }
            $page++
        }
        return $all
    }
    catch {
        $detail = ''
        if ($_.ErrorDetails -and $_.ErrorDetails.Message) { $detail = ' :: ' + $_.ErrorDetails.Message }
        throw ("Gitea API {0} {1} failed: {2}{3}" -f $Method, $Path, $_.Exception.Message, $detail)
    }
}

function Get-GiteaStatusCode {
    # Best-effort HTTP status extraction from a caught exception record
    # (works for both PS 5.1 WebException and PS 7 HttpResponseException).
    param($ErrorRecord)
    try {
        if ($ErrorRecord.Exception.Response -and $ErrorRecord.Exception.Response.StatusCode) {
            return [int]$ErrorRecord.Exception.Response.StatusCode
        }
    } catch { }
    # Invoke-Gitea rethrows as a plain exception; fall back to sniffing the message.
    if ($ErrorRecord.Exception.Message -match '\b(40[0-9]|409|422|50[0-9])\b') { return [int]$Matches[1] }
    return 0
}

function Get-GiteaRepository {
    param([Parameter(Mandatory)]$Context)
    Invoke-Gitea -Context $Context -Path ("/repos/{0}/{1}" -f $Context.Owner, $Context.Repo)
}

function Get-GiteaLabels {
    param([Parameter(Mandatory)]$Context)
    Invoke-Gitea -Context $Context -Path ("/repos/{0}/{1}/labels" -f $Context.Owner, $Context.Repo) -AllPages
}

function New-GiteaLabel {
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$Color,
        [string]$Description = ''
    )
    Invoke-Gitea -Context $Context -Method POST -Path ("/repos/{0}/{1}/labels" -f $Context.Owner, $Context.Repo) -Body @{
        name        = $Name
        color       = $Color
        description = $Description
    }
}

function Set-GiteaLabel {
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][long]$Id,
        [Parameter(Mandatory)][string]$Color,
        [string]$Description = ''
    )
    Invoke-Gitea -Context $Context -Method PATCH -Path ("/repos/{0}/{1}/labels/{2}" -f $Context.Owner, $Context.Repo, $Id) -Body @{
        color       = $Color
        description = $Description
    }
}

function Get-GiteaMilestones {
    param([Parameter(Mandatory)]$Context)
    Invoke-Gitea -Context $Context -Path ("/repos/{0}/{1}/milestones?state=all" -f $Context.Owner, $Context.Repo) -AllPages
}

function New-GiteaMilestone {
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$Title,
        [string]$Description = ''
    )
    Invoke-Gitea -Context $Context -Method POST -Path ("/repos/{0}/{1}/milestones" -f $Context.Owner, $Context.Repo) -Body @{
        title       = $Title
        description = $Description
    }
}

function Get-GiteaIssues {
    # All issues (not PRs), every state. Used to make re-runs idempotent.
    param([Parameter(Mandatory)]$Context)
    Invoke-Gitea -Context $Context -Path ("/repos/{0}/{1}/issues?state=all&type=issues" -f $Context.Owner, $Context.Repo) -AllPages
}

function Get-GiteaPullRequests {
    <#
    .SYNOPSIS
        List repository pull requests.
    #>
    param(
        [Parameter(Mandatory)]$Context,
        [ValidateSet('open', 'closed', 'all')][string]$State = 'open',
        [int]$Limit = 50
    )

    $query = Join-GiteaQueryString @{
        state = $State
        limit = $Limit
    }
    ConvertTo-GiteaPageItems (
        Invoke-Gitea -Context $Context -Path ("/repos/{0}/{1}/pulls{2}" -f $Context.Owner, $Context.Repo, $query))
}

function New-GiteaPullRequest {
    <#
    .SYNOPSIS
        Create a repository pull request.
    #>
    param(
        [Parameter(Mandatory)]$Context,
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

    Invoke-Gitea -Context $Context -Method POST -Path ("/repos/{0}/{1}/pulls" -f $Context.Owner, $Context.Repo) -Body $payload
}

function Get-GiteaPullRequest {
    <#
    .SYNOPSIS
        Get a single pull request by number.
    #>
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][int]$Number
    )

    Invoke-Gitea -Context $Context -Path ("/repos/{0}/{1}/pulls/{2}" -f $Context.Owner, $Context.Repo, $Number)
}

function Update-GiteaPullRequest {
    <#
    .SYNOPSIS
        Edit a pull request's title, body, target base, or state.
    .DESCRIPTION
        Only the fields you pass are sent, so updating the body alone leaves the
        title untouched. Pass -Body or -BodyPath (a markdown file) to set the
        description without hand-building JSON.
    #>
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][int]$Number,
        [string]$Title,
        [string]$Body,
        [string]$BodyPath,
        [string]$Base,
        [ValidateSet('open', 'closed')][string]$State
    )

    if ($PSBoundParameters.ContainsKey('Body') -and $PSBoundParameters.ContainsKey('BodyPath')) {
        throw 'Pass either -Body or -BodyPath, not both.'
    }
    if ($PSBoundParameters.ContainsKey('BodyPath')) {
        $Body = Get-Content -LiteralPath $BodyPath -Raw -Encoding UTF8
    }

    $payload = @{}
    if ($PSBoundParameters.ContainsKey('Title')) { $payload.title = $Title }
    if ($PSBoundParameters.ContainsKey('Body') -or $PSBoundParameters.ContainsKey('BodyPath')) { $payload.body = $Body }
    if ($PSBoundParameters.ContainsKey('Base')) { $payload.base = $Base }
    if ($PSBoundParameters.ContainsKey('State')) { $payload.state = $State }
    if ($payload.Count -eq 0) {
        throw 'Nothing to update: pass at least one of -Title, -Body, -BodyPath, -Base, -State.'
    }

    Invoke-Gitea -Context $Context -Method PATCH -Path ("/repos/{0}/{1}/pulls/{2}" -f $Context.Owner, $Context.Repo, $Number) -Body $payload
}

function Get-GiteaPullRequestByHead {
    <#
    .SYNOPSIS
        Find a pull request by its head branch name.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Head,
        [ValidateSet('open', 'closed', 'all')][string]$State = 'open'
    )

    foreach ($pull in @(Get-GiteaPullRequests -Context $Context -State $State)) {
        $headRef = [string]$pull.head.ref
        $headLabel = [string]$pull.head.label
        if ($headRef -eq $Head -or $headLabel -eq $Head -or $headLabel.EndsWith(":$Head")) {
            return $pull
        }
    }
}

function New-GiteaIssueComment {
    <#
    .SYNOPSIS
        Add a comment to a Gitea issue or pull request.
    .DESCRIPTION
        Gitea exposes pull-request conversation comments through the issue
        comments endpoint, so this helper works for both object types.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][ValidateRange(1, [int]::MaxValue)][int]$Number,
        [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Body
    )

    if ([string]::IsNullOrWhiteSpace($Body)) {
        throw 'Comment body must not be empty.'
    }

    Invoke-Gitea `
        -Context $Context `
        -Method POST `
        -Path ("/repos/{0}/{1}/issues/{2}/comments" -f $Context.Owner, $Context.Repo, $Number) `
        -Body @{ body = $Body }
}

function Invoke-GiteaWorkflowDispatch {
    <#
    .SYNOPSIS
        Trigger a Gitea Actions workflow_dispatch event.
    #>
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$WorkflowId,
        [Parameter(Mandatory)][string]$Ref,
        [hashtable]$Inputs = @{}
    )

    $payload = @{ ref = $Ref }
    if ($Inputs.Count -gt 0) {
        $payload.inputs = $Inputs
    }

    Invoke-Gitea `
        -Context $Context `
        -Method POST `
        -Path ("/repos/{0}/{1}/actions/workflows/{2}/dispatches" -f $Context.Owner, $Context.Repo, [System.Uri]::EscapeDataString($WorkflowId)) `
        -Body $payload
}

function New-GiteaIssue {
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$Title,
        [string]$Body = '',
        [long[]]$LabelIds = @(),
        [Nullable[long]]$MilestoneId = $null
    )
    $payload = @{ title = $Title; body = $Body }
    if ($LabelIds.Count -gt 0) { $payload.labels = $LabelIds }
    if ($null -ne $MilestoneId) { $payload.milestone = $MilestoneId }
    Invoke-Gitea -Context $Context -Method POST -Path ("/repos/{0}/{1}/issues" -f $Context.Owner, $Context.Repo) -Body $payload
}

function Add-GiteaIssueDependency {
    <#
    .SYNOPSIS
        Mark issue #$Index as blocked by issue #$BlockedByIndex.
    .OUTPUTS
        'created', 'exists' (already present) or throws.
    .NOTES
        Requires "issue dependencies" to be enabled on the repository
        (SETUP-IN-GITEA.md step 0).
    #>
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][long]$Index,
        [Parameter(Mandatory)][long]$BlockedByIndex
    )
    try {
        $null = Invoke-Gitea -Context $Context -Method POST -Path ("/repos/{0}/{1}/issues/{2}/dependencies" -f $Context.Owner, $Context.Repo, $Index) -Body @{
            index = $BlockedByIndex
            owner = $Context.Owner
            repo  = $Context.Repo
        }
        return 'created'
    }
    catch {
        $code = Get-GiteaStatusCode $_
        if ($code -eq 409) { return 'exists' }   # dependency already recorded
        throw
    }
}

function Join-GiteaQueryString {
    param([hashtable]$Query)

    $parts = @()
    foreach ($key in $Query.Keys) {
        $value = $Query[$key]
        if ($null -eq $value -or [string]::IsNullOrWhiteSpace([string]$value)) {
            continue
        }

        $parts += ('{0}={1}' -f
            [System.Uri]::EscapeDataString([string]$key),
            [System.Uri]::EscapeDataString([string]$value))
    }

    if ($parts.Count -eq 0) { return '' }
    '?' + ($parts -join '&')
}

function Get-GiteaActionRuns {
    <#
    .SYNOPSIS
        List repository Actions workflow runs.
    #>
    param(
        [Parameter(Mandatory)]$Context,
        [string]$Branch,
        [string]$Status,
        [string]$Event,
        [string]$HeadSha,
        [int]$Limit = 20
    )

    $query = Join-GiteaQueryString @{
        branch = $Branch
        status = $Status
        event = $Event
        head_sha = $HeadSha
        limit = $Limit
    }

    $response = Invoke-Gitea -Context $Context -Path ("/repos/{0}/{1}/actions/runs{2}" -f $Context.Owner, $Context.Repo, $query)
    if ($response.PSObject.Properties['workflow_runs']) { return @($response.workflow_runs) }
    @()
}

function Get-GiteaActionRunJobs {
    <#
    .SYNOPSIS
        List jobs for a repository Actions workflow run.
    #>
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][long]$RunId,
        [string]$Status,
        [int]$Limit = 50
    )

    $query = Join-GiteaQueryString @{
        status = $Status
        limit = $Limit
    }

    $response = Invoke-Gitea -Context $Context -Path ("/repos/{0}/{1}/actions/runs/{2}/jobs{3}" -f $Context.Owner, $Context.Repo, $RunId, $query)
    if ($response.PSObject.Properties['jobs']) { return @($response.jobs) }
    @()
}

function Get-GiteaActionJobLog {
    <#
    .SYNOPSIS
        Download a repository Actions job log.
    #>
    param(
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][long]$JobId
    )

    [string](Invoke-Gitea -Context $Context -Path ("/repos/{0}/{1}/actions/jobs/{2}/logs" -f $Context.Owner, $Context.Repo, $JobId))
}

# --------------------------------------------------------------------------
# Planning-doc parsers
# --------------------------------------------------------------------------

function Get-PlanDocsRoot {
    # docs/gitea, resolved relative to this module (scripts/gitea/ -> repo root).
    $repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    Join-Path (Join-Path $repoRoot 'docs') 'gitea'
}

function ConvertFrom-PlanLabelsDoc {
    <#
    .SYNOPSIS
        Parse docs/gitea/labels.md table rows into label objects.
    .OUTPUTS
        [pscustomobject] Name, Color, Description per label.
    #>
    param([Parameter(Mandatory)][string]$Path)
    $rowRe = '^\|\s*`([^`]+)`\s*\|\s*`(#[0-9a-fA-F]{6})`\s*\|\s*(.*?)\s*\|\s*$'
    Get-Content -LiteralPath $Path -Encoding UTF8 | ForEach-Object {
        if ($_ -match $rowRe) {
            [pscustomobject]@{
                Name        = $Matches[1]
                Color       = $Matches[2]
                Description = $Matches[3]
            }
        }
    }
}

function ConvertFrom-PlanMilestoneDoc {
    <#
    .SYNOPSIS
        Parse a milestones/M*.md file into a milestone + its issues.
    .DESCRIPTION
        H1 becomes the milestone title; the preamble (everything before the
        first ---) becomes the milestone description. Every "## Mx-n - Title
        `label` ... - SIZE" heading becomes one issue: body is the verbatim
        text under the heading, labels come from the backticked tokens, and
        "depends on:" lines are extracted as local-id dependencies.
    #>
    param([Parameter(Mandatory)][string]$Path)
    $lines = @(Get-Content -LiteralPath $Path -Encoding UTF8)
    if ($lines.Count -eq 0) { throw "Empty file: $Path" }
    if ($lines[0] -notmatch '^#\s+(.+)$') { throw "No H1 title in $Path" }
    $title = $Matches[1].Trim()

    # Preamble = lines after H1 up to the first '---' separator.
    $sepIdx = -1
    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') { $sepIdx = $i; break }
    }
    if ($sepIdx -lt 0) { $sepIdx = $lines.Count }
    $description = ''
    if ($sepIdx -gt 1) { $description = (($lines[1..($sepIdx - 1)] -join "`n")).Trim() }

    $headRe = '^##\s+(M\d+-\d+)\s+' + $script:EmDash + '\s+(.+)$'
    $sizeRe = '\s+' + $script:EmDash + '\s*(XS|S|M|L|XL)\s*$'
    $depRe  = '^\s*\*\*depends on:\*\*\s*(.+)$'

    # Collect issue heading line numbers.
    $headIdx = @()
    for ($i = $sepIdx; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match $headRe) { $headIdx += $i }
    }

    $issues = @()
    for ($h = 0; $h -lt $headIdx.Count; $h++) {
        $start = $headIdx[$h]
        $end = $lines.Count - 1
        if ($h + 1 -lt $headIdx.Count) { $end = $headIdx[$h + 1] - 1 }

        $null = $lines[$start] -match $headRe
        $localId = $Matches[1]
        $rest = $Matches[2]

        $size = ''
        if ($rest -match $sizeRe) {
            $size = $Matches[1]
            $rest = $rest -replace $sizeRe, ''
        }
        $labels = @()
        foreach ($m in [regex]::Matches($rest, '`([a-z0-9/._-]+)`')) { $labels += $m.Groups[1].Value }
        $issueTitle = (($rest -replace '`[^`]*`', '') -replace '\s{2,}', ' ').Trim()

        $bodyLines = @()
        if ($start + 1 -le $end) { $bodyLines = $lines[($start + 1)..$end] }
        # Trim trailing blank lines, keep everything else verbatim.
        while ($bodyLines.Count -gt 0 -and $bodyLines[-1].Trim() -eq '') {
            if ($bodyLines.Count -eq 1) { $bodyLines = @(); break }
            $bodyLines = $bodyLines[0..($bodyLines.Count - 2)]
        }

        $dependsOn = @()
        foreach ($line in $bodyLines) {
            if ($line -match $depRe) {
                foreach ($m in [regex]::Matches($Matches[1], '\bM\d+-\d+\b|\bM\d+\b')) { $dependsOn += $m.Value }
            }
        }

        $issues += [pscustomobject]@{
            LocalId   = $localId
            Title     = $issueTitle
            Labels    = $labels
            Size      = $size
            Body      = ($bodyLines -join "`n")
            DependsOn = $dependsOn
        }
    }

    [pscustomobject]@{
        Title       = $title
        Description = $description
        Issues      = $issues
        Path        = $Path
    }
}

function ConvertFrom-PlanEpicDoc {
    <#
    .SYNOPSIS
        Parse an epics/E*.md file into an epic tracking-issue definition.
    .DESCRIPTION
        Issue title is "Epic: <name>" (H1 minus the "En -" prefix), labels come
        from the backticked label line, and the body is the file content minus
        the H1, the label line and the "this file is the body" blockquote -
        exactly what SETUP-IN-GITEA.md step 4 says to paste.
    #>
    param([Parameter(Mandatory)][string]$Path)
    $lines = @(Get-Content -LiteralPath $Path -Encoding UTF8)
    $h1Re = '^#\s+(E\d+)\s+' + $script:EmDash + '\s+(.+)$'
    if ($lines.Count -eq 0 -or $lines[0] -notmatch $h1Re) { throw "No '# En - Title' H1 in $Path" }
    $localId = $Matches[1]
    $name = $Matches[2].Trim()

    $labelLineRe = '^\s*(`[a-z0-9/._-]+`\s*)+$'
    $labels = @()
    $skip = @{ 0 = $true }
    for ($i = 1; $i -lt [Math]::Min(6, $lines.Count); $i++) {
        if ($lines[$i] -match $labelLineRe) {
            foreach ($m in [regex]::Matches($lines[$i], '`([a-z0-9/._-]+)`')) { $labels += $m.Groups[1].Value }
            $skip[$i] = $true
            break
        }
    }

    # Drop the first blockquote block that carries the "paste this" note.
    $inNote = $false
    $noteDone = $false
    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($noteDone) { break }
        $isQuote = $lines[$i] -match '^\s*>'
        if (-not $inNote -and $isQuote -and $lines[$i] -match 'file is the body') { $inNote = $true }
        if ($inNote) {
            if ($isQuote) { $skip[$i] = $true } else { $noteDone = $true }
        }
    }

    $bodyLines = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if (-not $skip.ContainsKey($i)) { $bodyLines += $lines[$i] }
    }
    while ($bodyLines.Count -gt 0 -and $bodyLines[0].Trim() -eq '') {
        if ($bodyLines.Count -eq 1) { $bodyLines = @(); break }
        $bodyLines = $bodyLines[1..($bodyLines.Count - 1)]
    }

    [pscustomobject]@{
        LocalId    = $localId
        Name       = $name
        IssueTitle = "Epic: $name"
        Labels     = $labels
        Body       = ($bodyLines -join "`n").Trim()
        Path       = $Path
    }
}

function Convert-PlanRelativeLink {
    <#
    .SYNOPSIS
        Rewrite relative .md links to absolute Gitea source links.
    .DESCRIPTION
        Issue bodies live at URLs where relative doc links would 404. This
        rewrites "](E2-foo.md)" / "](../DECISIONS.md#anchor)" to
        "<base>/<owner>/<repo>/src/branch/<branch>/docs/gitea/...".
    #>
    param(
        [Parameter(Mandatory)][string]$Markdown,
        [Parameter(Mandatory)]$Context,
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][string]$DocRelDir   # repo-relative dir of the source doc, e.g. docs/gitea/epics
    )
    $evaluator = {
        param($match)
        $target = $match.Groups[1].Value
        $anchor = $match.Groups[2].Value
        $stack = New-Object System.Collections.Generic.List[string]
        foreach ($seg in ($DocRelDir -split '/')) { if ($seg) { $stack.Add($seg) } }
        foreach ($seg in ($target -split '/')) {
            if ($seg -eq '..') { if ($stack.Count -gt 0) { $stack.RemoveAt($stack.Count - 1) } }
            elseif ($seg -and $seg -ne '.') { $stack.Add($seg) }
        }
        $repoRel = $stack -join '/'
        '](' + ('{0}/{1}/{2}/src/branch/{3}/{4}{5}' -f $Context.BaseUrl, $Context.Owner, $Context.Repo, $Branch, $repoRel, $anchor) + ')'
    }
    $pattern = '\]\(((?!https?://|#)[^)\s]+?\.md)(#[^)]*)?\)'
    [regex]::Replace($Markdown, $pattern, $evaluator)
}

function New-PlanIssueFooter {
    <#
    .SYNOPSIS
        Build the traceability footer appended to every synced issue body.
    .DESCRIPTION
        The "Planning id" marker is what makes re-runs idempotent: existing
        issues are matched by this marker, never recreated.
    #>
    param(
        [Parameter(Mandatory)][string]$LocalId,
        [Parameter(Mandatory)][string]$SourceRelPath,
        [string]$Size = ''
    )
    $suffix = ''
    if ($Size) { $suffix = ", size: $Size" }
    "`n`n---`n_Planning id: ``$LocalId`` (source: ``$SourceRelPath``$suffix). Managed by scripts/gitea; edit the doc, not this line._"
}

function Get-PlanIdMap {
    <#
    .SYNOPSIS
        Map planning ids (M1-2, E4, ...) to existing Gitea issues via the
        "Planning id" footer marker. Falls back to nothing if absent.
    #>
    param([Parameter(Mandatory)]$Context)
    $map = @{}
    foreach ($issue in @(Get-GiteaIssues -Context $Context)) {
        $body = ''
        if ($issue.PSObject.Properties['body'] -and $issue.body) { $body = [string]$issue.body }
        if ($body -match 'Planning id: `([ME]\d+(?:-\d+)?)`') {
            $map[$Matches[1]] = $issue
        }
    }
    $map
}

Export-ModuleMember -Function @(
    'Get-GiteaContext', 'Get-GiteaGitCredentialContext', 'Get-GiteaRemoteInfo', 'Get-GiteaCurrentBranchName',
    'Invoke-Gitea', 'Get-GiteaStatusCode', 'Get-GiteaRepository',
    'Get-GiteaLabels', 'New-GiteaLabel', 'Set-GiteaLabel',
    'Get-GiteaMilestones', 'New-GiteaMilestone',
    'Get-GiteaPullRequests', 'Get-GiteaPullRequest', 'Get-GiteaPullRequestByHead',
    'New-GiteaPullRequest', 'Update-GiteaPullRequest',
    'Get-GiteaIssues', 'New-GiteaIssue', 'New-GiteaIssueComment', 'Add-GiteaIssueDependency',
    'Get-GiteaActionRuns', 'Get-GiteaActionRunJobs', 'Get-GiteaActionJobLog',
    'Invoke-GiteaWorkflowDispatch',
    'Get-PlanDocsRoot', 'ConvertFrom-PlanLabelsDoc', 'ConvertFrom-PlanMilestoneDoc',
    'ConvertFrom-PlanEpicDoc', 'Convert-PlanRelativeLink', 'New-PlanIssueFooter', 'Get-PlanIdMap'
)
