<#
    .SYNOPSIS
        Stages candidate pin advances for every community asset without
        applying any of them.

    .DESCRIPTION
        The drift services detect that a pin is behind; nothing proposes what
        moving it would mean. Blackout shipped for seven weeks after upstream
        deleted it because no one looked. This walks every pinned extension,
        theme and custom app, reports the commits between the pin and upstream
        head, whether the asset still exists at head and what it hashes to,
        runs the archived, stale and evidence policies against the candidate,
        and writes a review file under work/.

        It changes no pin, no manifest and no source file. The output is for a
        human to read before deciding.

    .PARAMETER ResponseCache
        Test seam. A JSON map of request URL to a recorded response
        ({ status, body, base64 }). When supplied, every request is served
        from it and the network is never touched; a URL that is not in the map
        is treated the same as an unreachable host, which is how the offline
        path is exercised.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$ManifestPath,
    [string]$OutputPath,
    [string]$ResponseCache
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepoRoot)) { $RepoRoot = Split-Path -Path $PSScriptRoot -Parent }
if ([string]::IsNullOrWhiteSpace($ManifestPath)) { $ManifestPath = Join-Path $RepoRoot 'schemas/community-assets.json' }
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $RepoRoot 'work/catalog-refresh-proposal.json' }

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Community asset manifest not found: $ManifestPath"
}

$script:RecordedResponses = $null
if (-not [string]::IsNullOrWhiteSpace($ResponseCache)) {
    if (-not (Test-Path -LiteralPath $ResponseCache -PathType Leaf)) {
        throw "Response cache not found: $ResponseCache"
    }
    $script:RecordedResponses = Get-Content -Raw -LiteralPath $ResponseCache | ConvertFrom-Json
}

$script:UnreachableUrls = @()

function Get-UpstreamText {
    param([Parameter(Mandatory)][string]$Url)

    if ($null -ne $script:RecordedResponses) {
        $entry = $script:RecordedResponses.PSObject.Properties[$Url]
        if ($null -eq $entry) {
            $script:UnreachableUrls += $Url
            return [pscustomobject]@{ Status = 0; Text = $null; Bytes = $null }
        }
        $recorded = $entry.Value
        $bytes = $null
        if ($recorded.PSObject.Properties['base64'] -and -not [string]::IsNullOrWhiteSpace([string]$recorded.base64)) {
            $bytes = [Convert]::FromBase64String([string]$recorded.base64)
        } elseif ($recorded.PSObject.Properties['body']) {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes([string]$recorded.body)
        }
        $text = if ($null -ne $bytes) { [System.Text.Encoding]::UTF8.GetString($bytes) } else { $null }
        return [pscustomobject]@{ Status = [int]$recorded.status; Text = $text; Bytes = $bytes }
    }

    $headers = @{ 'User-Agent' = 'LibreSpot-CatalogRefreshProposal' }
    try {
        $response = Invoke-WebRequest -Uri $Url -Headers $headers -UseBasicParsing -TimeoutSec 30 -ErrorAction Stop
    } catch {
        $status = 0
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $status = [int]$_.Exception.Response.StatusCode
        }
        if ($status -eq 0) { $script:UnreachableUrls += $Url }
        return [pscustomobject]@{ Status = $status; Text = $null; Bytes = $null }
    }

    $bytes = $response.Content
    if ($bytes -is [string]) { $bytes = [System.Text.Encoding]::UTF8.GetBytes($bytes) }
    $text = $null
    try { $text = [System.Text.Encoding]::UTF8.GetString($bytes) } catch { $text = $null }
    return [pscustomobject]@{ Status = [int]$response.StatusCode; Text = $text; Bytes = $bytes }
}

function Get-UpstreamJson {
    param([Parameter(Mandatory)][string]$Url)
    $result = Get-UpstreamText -Url $Url
    if ($result.Status -ne 200 -or [string]::IsNullOrWhiteSpace($result.Text)) { return $null }
    try { return $result.Text | ConvertFrom-Json } catch { return $null }
}

function Get-Sha256Hex {
    param([byte[]]$Bytes)
    if ($null -eq $Bytes) { return $null }
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash($Bytes)) -replace '-', '').ToLowerInvariant()
    } finally {
        $sha.Dispose()
    }
}

# Every asset kind is fetched from a different shape of URL, and the proposal
# is only useful if it hashes exactly the bytes the installer would fetch.
function Get-HeadAssetUrl {
    param([pscustomobject]$Candidate, [string]$HeadCommit)
    switch ($Candidate.Section) {
        'extensions' { return "https://raw.githubusercontent.com/$($Candidate.Owner)/$($Candidate.Repository)/$HeadCommit/$($Candidate.AssetPath)" }
        'themes'     { return "https://github.com/$($Candidate.Owner)/$($Candidate.Repository)/archive/$HeadCommit.zip" }
        default      { return $null }
    }
}

$manifest = Get-Content -Raw -LiteralPath $ManifestPath | ConvertFrom-Json

$candidates = @()
foreach ($section in @('extensions', 'themes', 'customApps')) {
    foreach ($asset in @($manifest.$section)) {
        if ($null -eq $asset) { continue }
        $id = [string]$asset.filename
        if ([string]::IsNullOrWhiteSpace($id)) { $id = [string]$asset.themeId }
        if ([string]::IsNullOrWhiteSpace($id)) { $id = [string]$asset.appId }
        $candidates += [pscustomobject]@{
            Section      = $section
            Id           = $id
            Owner        = [string]$asset.owner
            Repository   = [string]$asset.repo
            Branch       = if ([string]::IsNullOrWhiteSpace([string]$asset.branch)) { 'main' } else { [string]$asset.branch }
            PinnedCommit = [string]$asset.commitSha
            AssetPath    = [string]$asset.assetPath
            PinnedHash   = if ($asset.PSObject.Properties['sha256']) { [string]$asset.sha256 } else { [string]$asset.archiveSha256 }
            Evidence     = @($asset.catalogReview.evidenceUrls)
            SupportState = [string]$asset.supportState
            Asset        = $asset
        }
    }
}

if ($candidates.Count -eq 0) {
    throw "No community assets found in $ManifestPath; the proposal would be empty."
}

$results = @()
$flagged = @()
foreach ($candidate in $candidates) {
    $label = "$($candidate.Section)/$($candidate.Id)"
    $repoMeta = Get-UpstreamJson -Url "https://api.github.com/repos/$($candidate.Owner)/$($candidate.Repository)"
    $headCommit = $null
    $commits = @()
    $behind = $null
    $assetUrl = $null
    $assetExists = $null
    $headHash = $null

    if ($null -ne $repoMeta) {
        $branch = if ([string]::IsNullOrWhiteSpace([string]$repoMeta.default_branch)) { $candidate.Branch } else { [string]$repoMeta.default_branch }
        $headRef = Get-UpstreamJson -Url "https://api.github.com/repos/$($candidate.Owner)/$($candidate.Repository)/commits/$branch"
        if ($null -ne $headRef) { $headCommit = [string]$headRef.sha }
    }

    if ($headCommit) {
        $comparison = Get-UpstreamJson -Url "https://api.github.com/repos/$($candidate.Owner)/$($candidate.Repository)/compare/$($candidate.PinnedCommit)...$headCommit"
        if ($null -ne $comparison) {
            $behind = [int]$comparison.ahead_by
            foreach ($commit in @($comparison.commits)) {
                $subject = ([string]$commit.commit.message -split "`n")[0]
                $commits += [pscustomobject]@{ sha = [string]$commit.sha; subject = $subject }
            }
        }

        $assetUrl = Get-HeadAssetUrl -Candidate $candidate -HeadCommit $headCommit
        if ($assetUrl) {
            $assetResponse = Get-UpstreamText -Url $assetUrl
            $assetExists = ($assetResponse.Status -eq 200)
            if ($assetExists) { $headHash = Get-Sha256Hex -Bytes $assetResponse.Bytes }
        }
    }

    # The policies the catalog already applies, run against the candidate
    # rather than the pin: an archived or long-idle upstream, missing review
    # evidence, and an asset that is simply gone at head.
    $issues = @()
    if ($null -eq $repoMeta) {
        $issues += 'upstream repository metadata could not be read'
    } else {
        if ([bool]$repoMeta.archived) { $issues += 'upstream repository is archived' }
        $pushed = [datetime]::MinValue
        if ([datetime]::TryParse([string]$repoMeta.pushed_at, [ref]$pushed)) {
            if ($pushed -lt (Get-Date).ToUniversalTime().AddMonths(-12)) {
                $issues += "upstream has not been pushed since $($pushed.ToString('yyyy-MM-dd')), past the twelve-month maintenance threshold"
            }
        } else {
            $issues += 'upstream push date could not be read'
        }
    }
    if ($assetExists -eq $false) { $issues += "the pinned asset no longer exists at head ($assetUrl)" }
    if (@($candidate.Evidence).Count -eq 0) {
        $issues += 'the catalog entry carries no review evidence URLs'
    } else {
        foreach ($url in @($candidate.Evidence)) {
            if (-not ([string]$url).StartsWith('https://', [System.StringComparison]::Ordinal)) {
                $issues += "review evidence URL is not HTTPS: $url"
            }
        }
    }

    if ($issues.Count -gt 0) {
        foreach ($issue in $issues) { $flagged += "${label}: $issue" }
    }

    $results += [ordered]@{
        section          = $candidate.Section
        id               = $candidate.Id
        repository       = "$($candidate.Owner)/$($candidate.Repository)"
        pinnedCommit     = $candidate.PinnedCommit
        pinnedSha256     = $candidate.PinnedHash
        headCommit       = $headCommit
        commitsBehind    = $behind
        commits          = @($commits)
        headAssetUrl     = $assetUrl
        assetExistsAtHead = $assetExists
        headAssetSha256  = $headHash
        archived         = if ($null -eq $repoMeta) { $null } else { [bool]$repoMeta.archived }
        upstreamPushedAt = if ($null -eq $repoMeta) { $null } else { [string]$repoMeta.pushed_at }
        supportState     = $candidate.SupportState
        policyIssues     = @($issues)
    }
}

if ($script:UnreachableUrls.Count -gt 0) {
    Write-Host 'Catalog refresh proposal needs network access. These requests could not be made:' -ForegroundColor Red
    foreach ($url in ($script:UnreachableUrls | Select-Object -Unique)) { Write-Host "  $url" -ForegroundColor Red }
    throw 'The catalog refresh proposal ran without reaching upstream, so it has nothing to propose.'
}

$proposal = [ordered]@{
    '$comment'      = 'Candidate pin advances for review. Nothing here has been applied; every pin in schemas/community-assets.json is unchanged. Regenerate with Build-Scripts.ps1 -ProposeCatalogRefresh.'
    generatedAtUtc  = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    manifest        = [System.IO.Path]::GetFileName($ManifestPath)
    candidateCount  = $results.Count
    flagged         = @($flagged)
    candidates      = @($results)
}

$outputDirectory = Split-Path -Path $OutputPath -Parent
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path -LiteralPath $outputDirectory -PathType Container)) {
    New-Item -Path $outputDirectory -ItemType Directory -Force | Out-Null
}
$json = $proposal | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($OutputPath, $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "Catalog refresh proposal written to $OutputPath ($($results.Count) candidates)." -ForegroundColor Green
foreach ($candidate in $results) {
    $behindText = if ($null -eq $candidate.commitsBehind) { 'unknown' } else { "$($candidate.commitsBehind) commits" }
    Write-Host ("  {0,-24} {1} behind, asset at head: {2}" -f "$($candidate.section)/$($candidate.id)", $behindText, $candidate.assetExistsAtHead)
}
if ($flagged.Count -gt 0) {
    Write-Host 'Flagged for review:' -ForegroundColor Yellow
    foreach ($item in $flagged) { Write-Host "  $item" -ForegroundColor Yellow }
}
