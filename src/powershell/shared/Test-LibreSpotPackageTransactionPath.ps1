function Test-LibreSpotPackageTransactionPath {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string[]]$AllowedRoots,

        [Parameter(Mandatory = $true)]
        [string]$TransactionId,

        [ValidateSet('target', 'stage', 'backup', 'marker')]
        [string]$Role = 'target'
    )

    if ($TransactionId -notmatch '\A[0-9a-f]{32}\z') {
        throw "Package transaction id is invalid: $TransactionId"
    }
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "Package transaction $Role path is empty."
    }

    try {
        $fullPath = [System.IO.Path]::GetFullPath($Path)
    } catch {
        throw "Package transaction $Role path is invalid: $Path"
    }
    $fullPath = $fullPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    if ([string]::IsNullOrWhiteSpace($fullPath)) {
        throw "Package transaction $Role path is empty after normalization."
    }

    $roots = @()
    foreach ($rootPath in @($AllowedRoots)) {
        if ([string]::IsNullOrWhiteSpace([string]$rootPath)) { continue }
        $root = [System.IO.Path]::GetFullPath([string]$rootPath).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            throw "Package transaction root does not exist: $root"
        }
        $rootItem = Get-Item -LiteralPath $root -Force -ErrorAction Stop
        if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Package transaction root is a reparse point: $root"
        }
        $roots += $root
    }
    if ($roots.Count -eq 0) { throw 'Package transaction has no allowed roots.' }

    $parentPath = Split-Path -Path $fullPath -Parent
    $parent = [System.IO.Path]::GetFullPath($parentPath).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $matchingRoot = @($roots | Where-Object {
        $_.Equals($parent, [System.StringComparison]::OrdinalIgnoreCase)
    })
    if ($Role -eq 'marker') {
        if ($matchingRoot.Count -ne 1) {
            throw "Package transaction marker must be directly inside an allowed root: $fullPath"
        }
    } elseif ($matchingRoot.Count -ne 1) {
        throw "Package transaction $Role must be directly inside an allowed root: $fullPath"
    }

    if ($Role -in @('stage', 'backup')) {
        $prefix = '.librespot-package-' + $TransactionId + '-'
        $leaf = Split-Path -Path $fullPath -Leaf
        if (-not $leaf.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Package transaction $Role is not owned by transaction ${TransactionId}: $fullPath"
        }
    }

    $pathRoot = [System.IO.Path]::GetPathRoot($fullPath)
    $matchingVolume = @($roots | Where-Object {
        [System.IO.Path]::GetPathRoot($_).Equals($pathRoot, [System.StringComparison]::OrdinalIgnoreCase)
    })
    if ($matchingVolume.Count -eq 0) {
        throw "Package transaction $Role is on a different volume from its allowed root: $fullPath"
    }

    $walkPath = if (Test-Path -LiteralPath $fullPath) { $fullPath } else { $parent }
    while (-not [string]::IsNullOrWhiteSpace($walkPath)) {
        $walkItem = Get-Item -LiteralPath $walkPath -Force -ErrorAction Stop
        if (($walkItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Package transaction path crosses a reparse point: $walkPath"
        }
        $walkRoot = [System.IO.Path]::GetPathRoot($walkPath).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        $walkNormalized = $walkPath.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
        if ($roots | Where-Object { $_.Equals($walkNormalized, [System.StringComparison]::OrdinalIgnoreCase) }) { break }
        if ($walkNormalized -eq $walkRoot) { break }
        $next = Split-Path -Path $walkNormalized -Parent
        if ($next -eq $walkNormalized) { break }
        $walkPath = $next
    }

    return $fullPath
}
