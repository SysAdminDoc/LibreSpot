[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),

    [string]$GeneratedDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ManifestValue {
    param(
        [AllowNull()]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [AllowNull()]
        [object]$Default = $null
    )

    if ($null -eq $Object) {
        return $Default
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) {
        return $Default
    }

    return $property.Value
}

function Require-ManifestValue {
    param(
        [AllowNull()]
        [object]$Object,

        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [string]$Context
    )

    $value = Get-ManifestValue -Object $Object -Name $Name
    if ([string]::IsNullOrWhiteSpace([string]$value)) {
        throw "$Context is missing '$Name'."
    }

    return $value
}

function Read-JsonFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Manifest not found: $Path"
    }

    # The schemas are BOM-less UTF-8. Windows PowerShell 5.1 reads those in the
    # ANSI codepage, which turns every em-dash in a notes field into mojibake
    # and publishes a corrupted catalog. Decode explicitly on both hosts.
    $raw = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    return $raw | ConvertFrom-Json
}

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $encoding = New-Object -TypeName System.Text.UTF8Encoding -ArgumentList $false
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function ConvertTo-Html {
    param([AllowNull()][object]$Value)

    if ($null -eq $Value) {
        return ''
    }

    return [System.Net.WebUtility]::HtmlEncode([string]$Value)
}

function ConvertTo-HtmlLink {
    param(
        [AllowNull()][string]$Url,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if ([string]::IsNullOrWhiteSpace($Url)) {
        return ConvertTo-Html -Value $Label
    }

    return '<a href="' + (ConvertTo-Html $Url) + '" rel="noopener noreferrer">' +
        (ConvertTo-Html $Label) + '</a>'
}

function New-CatalogItem {
    param(
        [Parameter(Mandatory = $true)][object]$Asset,
        [Parameter(Mandatory = $true)][string]$Kind,
        [Parameter(Mandatory = $true)][object]$PreviewManifest
    )

    $contextName = "$Kind catalog entry"
    $id = if ($Kind -eq 'theme') {
        Require-ManifestValue -Object $Asset -Name 'themeId' -Context $contextName
    } elseif ($Kind -eq 'custom-app') {
        Require-ManifestValue -Object $Asset -Name 'appId' -Context $contextName
    } else {
        Require-ManifestValue -Object $Asset -Name 'filename' -Context $contextName
    }

    $name = Require-ManifestValue -Object $Asset -Name 'displayName' -Context $contextName
    $owner = Require-ManifestValue -Object $Asset -Name 'owner' -Context $contextName
    $repo = Require-ManifestValue -Object $Asset -Name 'repo' -Context $contextName
    $commit = Require-ManifestValue -Object $Asset -Name 'commitSha' -Context $contextName
    $review = Get-ManifestValue -Object $Asset -Name 'catalogReview'
    if ($null -eq $review) {
        throw "$contextName '$id' is missing catalogReview metadata."
    }

    $sourceUrl = Get-ManifestValue -Object $Asset -Name 'sourceUrl'
    if ([string]::IsNullOrWhiteSpace([string]$sourceUrl)) {
        $themeFolder = Get-ManifestValue -Object $Asset -Name 'themeFolder' -Default '.'
        $sourceUrl = "https://github.com/$owner/$repo/tree/$commit/$themeFolder"
    }

    $digest = if ($Kind -eq 'theme') {
        Require-ManifestValue -Object $Asset -Name 'archiveSha256' -Context $contextName
    } else {
        Require-ManifestValue -Object $Asset -Name 'sha256' -Context $contextName
    }

    $preview = $null
    $schemes = @()
    if ($Kind -eq 'theme') {
        $previewMatches = @($PreviewManifest.themes | Where-Object { $_.id -eq $id })
        if ($previewMatches.Count -ne 1) {
            throw "Theme '$id' must have exactly one theme-preview-manifest entry."
        }

        $previewEntry = $previewMatches[0]
        $previewData = Get-ManifestValue -Object $previewEntry -Name 'preview'
        $preview = [ordered]@{
            url = Get-ManifestValue -Object $previewData -Name 'url'
            status = Require-ManifestValue -Object $previewData -Name 'status' -Context "$contextName preview"
            contentType = Get-ManifestValue -Object $previewData -Name 'contentType'
            fallbackLabel = Get-ManifestValue -Object $previewData -Name 'fallbackLabel'
        }
        $schemes = @(Get-ManifestValue -Object $Asset -Name 'schemes' -Default @())
    }

    $description = Get-ManifestValue -Object $Asset -Name 'description'
    if ([string]::IsNullOrWhiteSpace([string]$description)) {
        $description = "$name from $owner/$repo."
    }

    $networkBehavior = Get-ManifestValue -Object $Asset -Name 'networkBehavior' -Default 'not-declared'
    $reviewDecision = Require-ManifestValue -Object $review -Name 'decision' -Context "$contextName catalogReview"
    $evaluatedDate = Require-ManifestValue -Object $review -Name 'evaluatedDate' -Context "$contextName catalogReview"

    return [ordered]@{
        id = $id
        kind = $Kind
        name = $name
        description = $description
        schemes = $schemes
        provenance = [ordered]@{
            owner = $owner
            repository = "$owner/$repo"
            branch = Get-ManifestValue -Object $Asset -Name 'branch' -Default 'main'
            commit = $commit
            sourceUrl = $sourceUrl
            releaseNotesUrl = Get-ManifestValue -Object $Asset -Name 'releaseNotesUrl'
        }
        license = [ordered]@{
            spdx = Require-ManifestValue -Object $Asset -Name 'spdxLicense' -Context $contextName
            copyrightHolder = Require-ManifestValue -Object $Asset -Name 'copyrightHolder' -Context $contextName
            redistributionPosture = Require-ManifestValue -Object $Asset -Name 'redistributionPosture' -Context $contextName
        }
        verification = [ordered]@{
            status = 'verified'
            badge = 'Pinned SHA256 verified'
            algorithm = 'SHA256'
            digest = $digest
            lastVerifiedDate = Require-ManifestValue -Object $Asset -Name 'lastVerifiedDate' -Context $contextName
        }
        review = [ordered]@{
            decision = $reviewDecision
            evaluatedDate = $evaluatedDate
            lastPush = Require-ManifestValue -Object $review -Name 'lastPush' -Context "$contextName catalogReview"
            archived = [bool](Get-ManifestValue -Object $review -Name 'archived' -Default $false)
            reason = Require-ManifestValue -Object $review -Name 'reason' -Context "$contextName catalogReview"
            evidenceUrls = @(Get-ManifestValue -Object $review -Name 'evidenceUrls' -Default @())
        }
        network = [ordered]@{
            behavior = $networkBehavior
            detail = Get-ManifestValue -Object $Asset -Name 'networkDetail'
        }
        supportState = Get-ManifestValue -Object $Asset -Name 'supportState' -Default 'active'
        fallbackBehavior = Get-ManifestValue -Object $Asset -Name 'fallbackBehavior'
        notes = Get-ManifestValue -Object $Asset -Name 'notes'
        preview = $preview
    }
}

function New-CatalogHtml {
    param(
        [Parameter(Mandatory = $true)][object[]]$Items,
        [Parameter(Mandatory = $true)][string]$GeneratedOn
    )

    $cards = New-Object System.Text.StringBuilder
    foreach ($item in $Items) {
        $kind = ConvertTo-Html $item.kind
        $name = ConvertTo-Html $item.name
        $description = ConvertTo-Html $item.description
        $repository = ConvertTo-Html $item.provenance.repository
        $commit = ConvertTo-Html $item.provenance.commit
        $license = ConvertTo-Html $item.license.spdx
        $holder = ConvertTo-Html $item.license.copyrightHolder
        $posture = ConvertTo-Html $item.license.redistributionPosture
        $network = ConvertTo-Html $item.network.behavior
        $networkDetail = ConvertTo-Html $item.network.detail
        $decision = ConvertTo-Html $item.review.decision
        $evaluated = ConvertTo-Html $item.review.evaluatedDate
        $lastPush = ConvertTo-Html $item.review.lastPush
        $reviewReason = ConvertTo-Html $item.review.reason
        $sourceLink = ConvertTo-HtmlLink -Url $item.provenance.sourceUrl -Label $item.provenance.repository
        $releaseLink = ConvertTo-HtmlLink -Url $item.provenance.releaseNotesUrl -Label 'Release notes'
        $evidenceLinks = foreach ($evidenceUrl in @($item.review.evidenceUrls)) {
            '<li>' + (ConvertTo-HtmlLink -Url $evidenceUrl -Label $evidenceUrl) + '</li>'
        }
        $evidenceMarkup = ($evidenceLinks -join '')
        $networkMarkup = if ([string]::IsNullOrWhiteSpace([string]$networkDetail)) {
            $network
        } else {
            "$network. $networkDetail"
        }
        $schemeMarkup = if (@($item.schemes).Count -gt 0) {
            '<div class="subtle"><strong>Schemes:</strong> ' + (ConvertTo-Html ((@($item.schemes) -join ', '))) + '</div>'
        } else {
            ''
        }
        $previewMarkup = ''
        if ($null -ne $item.preview) {
            $previewLabel = ConvertTo-Html $item.preview.fallbackLabel
            if ([string]::IsNullOrWhiteSpace([string]$item.preview.url)) {
                $previewMarkup = '<div class="preview unavailable">' + $previewLabel + '</div>'
            } else {
                $previewMarkup = '<div class="preview"><a href="' + (ConvertTo-Html $item.preview.url) +
                    '" rel="noopener noreferrer">Open preview</a></div>'
            }
        }

        [void]$cards.AppendLine(@"
<article class="card" data-kind="$kind" data-search="$name $repository $description">
  <div class="card-top"><span class="kind">$kind</span><span class="badge">$($item.verification.badge)</span></div>
  <h2>$name</h2>
  <p>$description</p>
  $previewMarkup
  $schemeMarkup
  <dl class="facts">
    <div><dt>Provenance</dt><dd>$sourceLink<br><code>$commit</code></dd></div>
    <div><dt>License</dt><dd>$license<br><span class="subtle">$holder</span></dd></div>
    <div><dt>Network behavior</dt><dd>$networkMarkup</dd></div>
    <div><dt>Reviewed</dt><dd>$evaluated<br><span class="subtle">Last push: $lastPush</span></dd></div>
    <div><dt>Catalog decision</dt><dd>$decision</dd></div>
  </dl>
  <p class="review"><strong>Review:</strong> $reviewReason</p>
  <p class="subtle">Redistribution: $posture</p>
  <details><summary>Evidence and release links</summary><ul>$evidenceMarkup<li>$releaseLink</li></ul></details>
</article>
"@)
    }

    $generated = ConvertTo-Html $GeneratedOn
    $itemCount = $Items.Count
    $extensionCount = @($Items | Where-Object kind -eq 'extension').Count
    $themeCount = @($Items | Where-Object kind -eq 'theme').Count
    $appCount = @($Items | Where-Object kind -eq 'custom-app').Count
    $css = @'
:root { color-scheme: dark; --bg: #11111b; --surface: #1e1e2e; --surface-2: #313244; --text: #cdd6f4; --muted: #a6adc8; --blue: #89b4fa; --teal: #94e2d5; --green: #a6e3a1; --border: #45475a; }
* { box-sizing: border-box; }
body { margin: 0; background: radial-gradient(circle at top right, #1e1e2e 0, var(--bg) 42rem); color: var(--text); font: 16px/1.55 system-ui, -apple-system, "Segoe UI", sans-serif; }
a { color: var(--blue); }
header, main, footer { width: min(1180px, calc(100% - 32px)); margin: 0 auto; }
header { padding: 72px 0 34px; }
h1 { margin: 0 0 12px; font-size: clamp(2rem, 5vw, 4rem); letter-spacing: -0.04em; }
h2 { margin: 10px 0 8px; color: #f5e0e0; font-size: 1.45rem; }
p { margin: 0 0 14px; }
.lede { max-width: 760px; color: var(--muted); font-size: 1.1rem; }
.meta { display: flex; flex-wrap: wrap; gap: 10px; margin-top: 22px; color: var(--muted); }
.pill, .kind, .badge { display: inline-flex; align-items: center; border: 1px solid var(--border); border-radius: 999px; padding: 4px 10px; font-size: .78rem; letter-spacing: .03em; }
.kind { color: var(--teal); text-transform: uppercase; }
.badge { border-color: #3f6f57; background: #1c3b2c; color: var(--green); }
.controls { display: flex; flex-wrap: wrap; gap: 12px; margin: 22px 0; }
input, select { border: 1px solid var(--border); border-radius: 8px; background: var(--surface); color: var(--text); padding: 10px 12px; font: inherit; }
input { flex: 1 1 300px; }
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 350px), 1fr)); gap: 18px; }
.card { display: flex; flex-direction: column; min-height: 100%; border: 1px solid var(--border); border-radius: 14px; background: linear-gradient(145deg, rgba(49,50,68,.92), rgba(30,30,46,.96)); padding: 22px; box-shadow: 0 14px 36px rgba(0,0,0,.18); }
.card:hover { border-color: var(--blue); transform: translateY(-2px); transition: transform .18s ease, border-color .18s ease; }
.card-top { display: flex; justify-content: space-between; gap: 10px; align-items: center; }
.facts { display: grid; gap: 10px; margin: 18px 0; }
.facts div { border-top: 1px solid rgba(69,71,90,.7); padding-top: 8px; }
dt { color: var(--muted); font-size: .8rem; text-transform: uppercase; letter-spacing: .04em; }
dd { margin: 2px 0 0; }
code { color: var(--teal); overflow-wrap: anywhere; }
.subtle { color: var(--muted); font-size: .9rem; }
.review { border-left: 3px solid var(--blue); padding-left: 10px; }
.preview { border: 1px solid var(--border); border-radius: 8px; background: var(--surface-2); padding: 8px 10px; margin: 8px 0 12px; }
.preview.unavailable { color: var(--muted); }
details { margin-top: auto; padding-top: 10px; }
summary { cursor: pointer; color: var(--blue); }
ul { padding-left: 20px; overflow-wrap: anywhere; }
footer { margin-top: 52px; padding: 24px 0 48px; border-top: 1px solid var(--border); color: var(--muted); }
@media (prefers-reduced-motion: reduce) { .card:hover { transform: none; transition: none; } }
'@
    $script = @'
const search = document.getElementById("search");
const kind = document.getElementById("kind");
const cards = Array.from(document.querySelectorAll(".card"));
const count = document.getElementById("count");
function applyFilter() {
  const query = search.value.toLowerCase().trim();
  const selected = kind.value;
  let visible = 0;
  cards.forEach(function (card) {
    const matchesText = !query || card.dataset.search.toLowerCase().indexOf(query) >= 0;
    const matchesKind = selected === "all" || card.dataset.kind === selected;
    const show = matchesText && matchesKind;
    card.hidden = !show;
    if (show) visible += 1;
  });
  count.textContent = visible + " shown";
}
search.addEventListener("input", applyFilter);
kind.addEventListener("change", applyFilter);
applyFilter();
'@

    return @"
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <meta name="description" content="Reviewed, hash-pinned LibreSpot community extensions, themes, and custom apps.">
  <title>LibreSpot Community Catalog</title>
  <style>$css</style>
</head>
<body>
  <header>
    <div class="pill">LibreSpot community catalog</div>
    <h1>Reviewed assets for Spotify customization.</h1>
    <p class="lede">Every entry comes from the repository's pinned catalog. Provenance, license posture, network behavior, SHA256 verification, and review evidence stay visible so you can decide before installing.</p>
    <div class="meta">
      <span class="pill">$itemCount assets</span>
      <span class="pill">$extensionCount extensions</span>
      <span class="pill">$themeCount themes</span>
      <span class="pill">$appCount custom app</span>
      <span class="pill">Generated $generated</span>
    </div>
  </header>
  <main>
    <div class="controls">
      <input id="search" type="search" placeholder="Search assets, owners, or descriptions" aria-label="Search catalog">
      <select id="kind" aria-label="Filter by asset type">
        <option value="all">All asset types</option>
        <option value="extension">Extensions</option>
        <option value="theme">Themes</option>
        <option value="custom-app">Custom apps</option>
      </select>
      <span id="count" class="pill">$itemCount shown</span>
    </div>
    <section class="grid" aria-label="Catalog assets">
      $cards
    </section>
  </main>
  <footer>
    <p>Generated locally from <a href="https://github.com/SysAdminDoc/LibreSpot/blob/main/schemas/community-assets.json">community-assets.json</a> and <a href="https://github.com/SysAdminDoc/LibreSpot/blob/main/schemas/theme-preview-manifest.json">theme-preview-manifest.json</a>. This site does not redistribute upstream files. Downloads come from the pinned source URLs shown on each card.</p>
    <p><a href="catalog.json">Machine-readable catalog</a> · <a href="https://github.com/SysAdminDoc/LibreSpot">LibreSpot source repository</a></p>
  </footer>
  <script>$script</script>
</body>
</html>
"@
}

$repoRootPath = (Resolve-Path -LiteralPath $RepoRoot).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$communityPath = Join-Path $repoRootPath 'schemas\community-assets.json'
$previewPath = Join-Path $repoRootPath 'schemas\theme-preview-manifest.json'
$communityManifest = Read-JsonFile -Path $communityPath
$previewManifest = Read-JsonFile -Path $previewPath

$items = @()
foreach ($extension in @($communityManifest.extensions)) {
    $items += New-CatalogItem -Asset $extension -Kind 'extension' -PreviewManifest $previewManifest
}
foreach ($theme in @($communityManifest.themes)) {
    $items += New-CatalogItem -Asset $theme -Kind 'theme' -PreviewManifest $previewManifest
}
foreach ($customApp in @($communityManifest.customApps)) {
    $items += New-CatalogItem -Asset $customApp -Kind 'custom-app' -PreviewManifest $previewManifest
}

if ($items.Count -eq 0) {
    throw 'The community catalog contains no assets.'
}

New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$counts = [ordered]@{
    extensions = @($items | Where-Object kind -eq 'extension').Count
    themes = @($items | Where-Object kind -eq 'theme').Count
    customApps = @($items | Where-Object kind -eq 'custom-app').Count
}
$catalog = [ordered]@{
    catalogVersion = 1
    generatedDate = $GeneratedDate
    generator = 'tools/Build-CommunityCatalog.ps1'
    sourceSchemas = @(
        'schemas/community-assets.json'
        'schemas/theme-preview-manifest.json'
    )
    counts = $counts
    items = $items
}

$catalogJson = $catalog | ConvertTo-Json -Depth 16
Write-Utf8File -Path (Join-Path $outputPath 'catalog.json') -Content $catalogJson
Write-Utf8File -Path (Join-Path $outputPath 'index.html') -Content (New-CatalogHtml -Items $items -GeneratedOn $GeneratedDate)
Write-Utf8File -Path (Join-Path $outputPath '404.html') -Content (New-CatalogHtml -Items $items -GeneratedOn $GeneratedDate)
Write-Utf8File -Path (Join-Path $outputPath 'README.md') -Content @"
# LibreSpot community catalog

This static site is generated locally from schemas/community-assets.json and
schemas/theme-preview-manifest.json by tools/Build-CommunityCatalog.ps1.
The gh-pages branch is the published output. No upstream asset files are
redistributed here. Each card links to its pinned source and review evidence.

Generated: $GeneratedDate
"@

Write-Output "Community catalog generated: $outputPath ($($items.Count) assets)"
