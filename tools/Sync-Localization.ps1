[CmdletBinding()]
param(
    [switch]$Validate,
    [switch]$PrefillMissing,
    [switch]$ScanRawStrings
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $repoRoot 'src/LibreSpot.Desktop/Properties/Strings.resx'
$propertiesDir = Split-Path -Parent $sourcePath
$identicalAllowlistPath = Join-Path $repoRoot 'schemas/localization-identical-allowlist.json'
$cultures = @('en', 'ru', 'zh-Hans', 'pt-BR', 'es')
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

if (-not (Test-Path -LiteralPath $sourcePath)) {
    throw "Source resource file not found at $sourcePath"
}

function Get-ResxEntries {
    param([string]$Path)

    [xml]$doc = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8)
    $entries = [ordered]@{}
    foreach ($data in $doc.root.data) {
        $entries[[string]$data.name] = [pscustomobject]@{
            Value = [string]$data.value
            Comment = [string]$data.comment
        }
    }
    return $entries
}

function Save-PrefilledResx {
    param(
        [string]$Culture,
        [string]$TargetPath
    )

    $content = [System.IO.File]::ReadAllText($sourcePath, [System.Text.Encoding]::UTF8)
    [System.IO.File]::WriteAllText($TargetPath, $content, $utf8NoBom)
    Write-Host "Prefilled missing $Culture resource file from source strings." -ForegroundColor Yellow
}

function Add-MissingResxEntries {
    param(
        [string]$Culture,
        [string]$TargetPath,
        [System.Collections.IDictionary]$SourceEntries,
        [System.Collections.IDictionary]$TargetEntries
    )

    $missing = @($SourceEntries.Keys | Where-Object { -not $TargetEntries.Contains($_) })
    $content = [System.IO.File]::ReadAllText($TargetPath, [System.Text.Encoding]::UTF8)
    $firstNewline = [regex]::Match($content, '\r?\n').Value
    $newline = if ([string]::IsNullOrEmpty($firstNewline)) { [Environment]::NewLine } else { $firstNewline }
    $normalizedContent = [regex]::Replace($content, '\r?\n', $newline)

    if ($missing.Count -eq 0) {
        if ($normalizedContent -cne $content) {
            [System.IO.File]::WriteAllText($TargetPath, $normalizedContent, $utf8NoBom)
            Write-Host "Normalized $Culture resource line endings." -ForegroundColor Yellow
        }
        return
    }

    $lines = foreach ($key in $missing) {
        $entry = $SourceEntries[$key]
        $escapedKey = [System.Security.SecurityElement]::Escape([string]$key)
        $escapedValue = [System.Security.SecurityElement]::Escape([string]$entry.Value)
        $escapedComment = [System.Security.SecurityElement]::Escape([string]$entry.Comment)
        "  <data name=`"$escapedKey`" xml:space=`"preserve`"><value>$escapedValue</value><comment>$escapedComment</comment></data>"
    }

    $insertion = ($lines -join $newline) + $newline
    $content = $normalizedContent.Replace('</root>', $insertion + '</root>')
    [System.IO.File]::WriteAllText($TargetPath, $content, $utf8NoBom)
    Write-Host "Prefilled $($missing.Count) missing $Culture resource key(s) from source strings." -ForegroundColor Yellow
}

function Get-FormatPlaceholderIndices {
    # Returns the set of distinct numbered .NET composite-format placeholder
    # indices ({0}, {1,-8}, {2:X}, ...) in a resource value, ignoring escaped
    # braces ({{ and }}). A translation whose placeholder set differs from the
    # source throws FormatException at runtime, so this is a hard gate.
    param([string]$Value)

    $indices = [System.Collections.Generic.HashSet[int]]::new()
    if ([string]::IsNullOrEmpty($Value)) { return , $indices }

    $stripped = $Value -replace '\{\{', '' -replace '\}\}', ''
    foreach ($match in [regex]::Matches($stripped, '\{(\d+)(?:[,:][^}]*)?\}')) {
        $null = $indices.Add([int]$match.Groups[1].Value)
    }
    # Comma operator: return the HashSet as a single object rather than letting
    # PowerShell enumerate it into the pipeline (which would collapse a single
    # index to a scalar int and break .Contains()).
    return , $indices
}

function Test-RawXamlStrings {
    $pattern = '\b(?:Text|Content|Header|ToolTip|Description|Title|AutomationProperties\.Name|AutomationProperties\.HelpText)="(?<value>[^\{][^"]*)"'
    $desktopRoot = Join-Path $repoRoot 'src/LibreSpot.Desktop'
    $violations = @()
    foreach ($xamlPath in @(Get-ChildItem -LiteralPath $desktopRoot -Filter '*.xaml' -File -Recurse |
        Where-Object { $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' })) {
        $content = [System.IO.File]::ReadAllText($xamlPath.FullName, [System.Text.Encoding]::UTF8)
        $violations += [regex]::Matches($content, $pattern) |
            Where-Object {
                $value = $_.Groups['value'].Value
                -not [string]::IsNullOrWhiteSpace($value) -and
                -not $value.StartsWith('pack://', [System.StringComparison]::OrdinalIgnoreCase)
            } |
            ForEach-Object { "$($xamlPath.FullName): $($_.Value)" }
    }

    $healthPath = Join-Path $desktopRoot 'Services/EnvironmentSnapshotService.cs'
    $healthContent = [System.IO.File]::ReadAllText($healthPath, [System.Text.Encoding]::UTF8)
    foreach ($block in [regex]::Matches($healthContent, '(?ms)(?:Component|new StackHealthComponent)\(.+?\);')) {
        foreach ($literal in [regex]::Matches($block.Value, '(?<![A-Za-z])"(?<value>[^"\r\n]*[^\S\r\n]+[^"\r\n]*)"')) {
            if ($literal.Groups['value'].Value -match '[\p{L}\p{N}]') {
                $violations += "${healthPath}: raw health text `"$($literal.Groups['value'].Value)`""
            }
        }
    }

    if ($violations.Count -gt 0) {
        Write-Host "Raw user-facing XAML and health strings must use resource bindings:" -ForegroundColor Red
        foreach ($violation in $violations) {
            Write-Host "  $violation" -ForegroundColor Red
        }
        exit 1
    }
}

$sourceEntries = Get-ResxEntries -Path $sourcePath
$sourceKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($key in $sourceEntries.Keys) {
    $null = $sourceKeys.Add($key)
}

$failures = New-Object System.Collections.Generic.List[string]
$identicalAllowlist = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$observedReviewedIdentical = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
$protectedTerms = @()

if (-not (Test-Path -LiteralPath $identicalAllowlistPath -PathType Leaf)) {
    $failures.Add("Missing reviewed identical-value allowlist: $identicalAllowlistPath")
} else {
    $allowlistDocument = Get-Content -Raw -LiteralPath $identicalAllowlistPath | ConvertFrom-Json
    if ([int]$allowlistDocument.schemaVersion -ne 1) {
        $failures.Add("Unsupported localization identical-value allowlist schema version '$($allowlistDocument.schemaVersion)'")
    }
    if ([string]$allowlistDocument.reviewedOn -notmatch '^\d{4}-\d{2}-\d{2}$') {
        $failures.Add('Localization identical-value allowlist reviewedOn must use YYYY-MM-DD.')
    }
    $protectedTerms = @($allowlistDocument.protectedTerms | ForEach-Object { [string]$_ })
    if ($protectedTerms.Count -eq 0 -or @($protectedTerms | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
        $failures.Add('Localization identical-value allowlist must declare non-empty protectedTerms.')
    }
    if (@($protectedTerms | Select-Object -Unique).Count -ne $protectedTerms.Count) {
        $failures.Add('Localization protectedTerms must not contain duplicates.')
    }

    foreach ($entry in @($allowlistDocument.entries)) {
        $key = [string]$entry.key
        if ([string]::IsNullOrWhiteSpace($key) -or [string]::IsNullOrWhiteSpace([string]$entry.reason)) {
            $failures.Add('Every localization identical-value allowlist entry needs a key and review reason.')
            continue
        }
        if (-not $sourceKeys.Contains($key)) {
            $failures.Add("Localization identical-value allowlist has unknown key '$key'")
        }
        if (-not $identicalAllowlist.Add($key)) {
            $failures.Add("Localization identical-value allowlist repeats key '$key'")
        }
    }
}

foreach ($culture in $cultures) {
    $targetPath = Join-Path $propertiesDir "Strings.$culture.resx"
    if (-not (Test-Path -LiteralPath $targetPath)) {
        if ($PrefillMissing) {
            Save-PrefilledResx -Culture $culture -TargetPath $targetPath
        } else {
            $failures.Add("Missing resource file: Strings.$culture.resx")
            continue
        }
    }

    $targetEntries = Get-ResxEntries -Path $targetPath
    if ($PrefillMissing) {
        Add-MissingResxEntries -Culture $culture -TargetPath $targetPath -SourceEntries $sourceEntries -TargetEntries $targetEntries
        $targetEntries = Get-ResxEntries -Path $targetPath
    }
    $targetKeys = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($key in $targetEntries.Keys) {
        $null = $targetKeys.Add($key)
    }

    foreach ($key in $sourceKeys) {
        if (-not $targetKeys.Contains($key)) {
            $failures.Add("Strings.$culture.resx is missing key '$key'")
            continue
        }

        if ([string]::IsNullOrWhiteSpace($targetEntries[$key].Value)) {
            $failures.Add("Strings.$culture.resx key '$key' has an empty value")
            continue
        }

        # Truncation lint: machine-translation pipelines have dropped everything after
        # the first sentence, leaving a trailing space and fewer sentences than the
        # English source. Catch both symptoms.
        $sourceValue = $sourceEntries[$key].Value
        $targetValue = $targetEntries[$key].Value

        if ($targetValue -ne $targetValue.TrimEnd()) {
            $failures.Add("Strings.$culture.resx key '$key' ends with trailing whitespace (likely truncated translation)")
        }

        # Terminators: . ! ? plus full-width ideographic stops (U+3002, U+FF01, U+FF1F).
        # Regex \u escapes keep this file ASCII-only for Windows PowerShell 5.1.
        $terminatorPattern = '[.!?\u3002\uFF01\uFF1F]'
        $sourceTerminators = [regex]::Matches($sourceValue, $terminatorPattern).Count
        $targetTerminators = [regex]::Matches($targetValue, $terminatorPattern).Count
        if ($targetTerminators -lt $sourceTerminators) {
            $failures.Add("Strings.$culture.resx key '$key' looks truncated: $targetTerminators sentence terminator(s) vs $sourceTerminators in the source value")
        }

        # Placeholder integrity: the target must use the same set of {N} indices
        # as the source, or string.Format throws FormatException at runtime.
        $sourceIndices = Get-FormatPlaceholderIndices -Value $sourceValue
        $targetIndices = Get-FormatPlaceholderIndices -Value $targetValue
        $missingIndices = @($sourceIndices | Where-Object { -not $targetIndices.Contains($_) } | Sort-Object)
        $extraIndices = @($targetIndices | Where-Object { -not $sourceIndices.Contains($_) } | Sort-Object)
        if ($missingIndices.Count -gt 0 -or $extraIndices.Count -gt 0) {
            $detail = @()
            if ($missingIndices.Count -gt 0) { $detail += "missing {$($missingIndices -join '}, {')}" }
            if ($extraIndices.Count -gt 0) { $detail += "extra {$($extraIndices -join '}, {')}" }
            $failures.Add("Strings.$culture.resx key '$key' has a format-placeholder mismatch ($($detail -join '; ')) - this would crash string.Format at runtime")
        }

        foreach ($term in $protectedTerms) {
            if ($sourceValue.IndexOf($term, [System.StringComparison]::Ordinal) -ge 0 -and
                $targetValue.IndexOf($term, [System.StringComparison]::Ordinal) -lt 0) {
                $failures.Add("Strings.$culture.resx key '$key' changed protected product/token '$term'")
            }
        }

        if ($sourceEntries[$key].Comment -match '(?i)accelerator|access key') {
            $sourceAccelerators = [regex]::Matches($sourceValue, '(?<!_)_(?!_)').Count
            $targetAccelerators = [regex]::Matches($targetValue, '(?<!_)_(?!_)').Count
            if ($targetAccelerators -ne $sourceAccelerators) {
                $failures.Add("Strings.$culture.resx key '$key' changed a declared access-key marker")
            }
        }
    }

    foreach ($key in $targetKeys) {
        if (-not $sourceKeys.Contains($key)) {
            $failures.Add("Strings.$culture.resx has stale key '$key'")
        }
    }

    if ($culture -eq 'en') {
        $englishDrift = @($sourceKeys | Where-Object {
            $targetEntries.Contains($_) -and
            $targetEntries[$_].Value -cne $sourceEntries[$_].Value
        })
        foreach ($key in $englishDrift) {
            $failures.Add("Strings.en.resx key '$key' must match the source English value")
        }
        Write-Host "Localization en: source-matched=$($sourceKeys.Count); total=$($sourceKeys.Count)."
        continue
    }

    $identicalKeys = @($sourceKeys | Where-Object {
        $targetEntries.Contains($_) -and
        $targetEntries[$_].Value -ceq $sourceEntries[$_].Value
    })
    $reviewedIdentical = @($identicalKeys | Where-Object { $identicalAllowlist.Contains($_) })
    foreach ($key in $reviewedIdentical) {
        $null = $observedReviewedIdentical.Add($key)
    }
    $unreviewedIdentical = @($identicalKeys | Where-Object { -not $identicalAllowlist.Contains($_) } | Sort-Object)
    $translatedCount = $sourceKeys.Count - $identicalKeys.Count
    Write-Host "Localization ${culture}: translated=$translatedCount; reviewed-identical=$($reviewedIdentical.Count); unreviewed-identical=$($unreviewedIdentical.Count); total=$($sourceKeys.Count)."
    if ($unreviewedIdentical.Count -gt 0) {
        $sample = ($unreviewedIdentical | Select-Object -First 20) -join ', '
        $suffix = if ($unreviewedIdentical.Count -gt 20) { ', ...' } else { '' }
        $failures.Add("Strings.$culture.resx has $($unreviewedIdentical.Count) source-identical value(s) without review: $sample$suffix")
    }
}

foreach ($key in $identicalAllowlist) {
    if (-not $observedReviewedIdentical.Contains($key)) {
        $failures.Add("Localization identical-value allowlist key '$key' is stale because no non-English locale uses the source value")
    }
}

if ($ScanRawStrings) {
    Test-RawXamlStrings
}

if ($Validate -or $ScanRawStrings) {
    if ($failures.Count -gt 0) {
        Write-Host "Localization validation failed:" -ForegroundColor Red
        foreach ($failure in $failures) {
            Write-Host "  $failure" -ForegroundColor Red
        }
        exit 1
    }

    Write-Host "Localization resources are complete and translation-reviewed for $($cultures -join ', ')." -ForegroundColor Green
    if ($ScanRawStrings) {
        Write-Host "No raw user-facing XAML strings found." -ForegroundColor Green
    }
}
