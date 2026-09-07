function Get-LibreSpotPackageFingerprint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [int]$MaxFiles = 8192,

        [long]$MaxBytes = 1073741824,

        [switch]$AllowReparse
    )

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        throw "Package path not found: $resolvedPath"
    }

    $entries = New-Object System.Collections.Generic.List[string]
    $fileCount = 0
    [long]$totalBytes = 0
    $shaFactory = [System.Security.Cryptography.SHA256]::Create()
    try {
        $rootItem = Get-Item -LiteralPath $resolvedPath -Force -ErrorAction Stop
        if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            if ($AllowReparse) {
                $reparseDigest = [System.Text.Encoding]::UTF8.GetBytes(('R|root|{0}' -f [int]$rootItem.Attributes))
                return (($shaFactory.ComputeHash($reparseDigest) | ForEach-Object { $_.ToString('x2') }) -join '')
            }
            throw "Refusing to fingerprint a reparse point: $resolvedPath"
        }

        if (-not $rootItem.PSIsContainer) {
            if ([long]$rootItem.Length -gt $MaxBytes) {
                throw "Package exceeds the $MaxBytes-byte verification limit."
            }
            $bytes = [System.IO.File]::ReadAllBytes($resolvedPath)
            $digest = (($shaFactory.ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join '')
            return $digest
        }

        $entries.Add('D|.')
        $pending = New-Object System.Collections.Generic.Stack[object]
        $pending.Push([pscustomobject]@{ Item = $rootItem; Relative = '' })
        while ($pending.Count -gt 0) {
            $current = $pending.Pop()
            $children = @(Get-ChildItem -LiteralPath $current.Item.FullName -Force -ErrorAction Stop)
            foreach ($child in $children) {
                $relative = if ([string]::IsNullOrWhiteSpace($current.Relative)) {
                    [string]$child.Name
                } else {
                    ($current.Relative + '/' + [string]$child.Name)
                }
                if (($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                    if ($AllowReparse) {
                        $entries.Add(('R|{0}|{1}' -f $relative, [int]$child.Attributes))
                        continue
                    }
                    throw "Refusing to fingerprint a package containing a reparse point: $($child.FullName)"
                }
                if ($child.PSIsContainer) {
                    $entries.Add('D|' + $relative)
                    $pending.Push([pscustomobject]@{ Item = $child; Relative = $relative })
                    continue
                }

                $fileCount++
                if ($fileCount -gt $MaxFiles) {
                    throw "Package contains more than the $MaxFiles-file verification limit."
                }
                [long]$totalBytes += [long]$child.Length
                if ($totalBytes -gt $MaxBytes) {
                    throw "Package exceeds the $MaxBytes-byte verification limit."
                }

                $fileStream = $null
                try {
                    $fileStream = [System.IO.File]::Open($child.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
                    $fileDigest = (($shaFactory.ComputeHash($fileStream) | ForEach-Object { $_.ToString('x2') }) -join '')
                } finally {
                    if ($null -ne $fileStream) { $fileStream.Dispose() }
                }
                $entries.Add(('F|{0}|{1}|{2}' -f $relative, $child.Length, $fileDigest))
            }
        }

        $canonical = ($entries | Sort-Object) -join "`n"
        $canonicalBytes = [System.Text.Encoding]::UTF8.GetBytes($canonical)
        return (($shaFactory.ComputeHash($canonicalBytes) | ForEach-Object { $_.ToString('x2') }) -join '')
    } finally {
        $shaFactory.Dispose()
    }
}
