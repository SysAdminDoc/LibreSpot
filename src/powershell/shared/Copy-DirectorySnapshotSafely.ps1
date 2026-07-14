function Copy-DirectorySnapshotSafely {
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DestinationPath,
        [long]$MaxBytes = 268435456,
        [hashtable]$State
    )

    if (-not $State) {
        $State = @{ FileCount = 0; Bytes = [long]0; SkippedReparsePoints = 0 }
    }
    if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
        return [pscustomobject]$State
    }

    $source = Get-Item -LiteralPath $SourcePath -Force -ErrorAction Stop
    if (($source.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to snapshot reparse-point root: $SourcePath"
    }

    New-Item -Path $DestinationPath -ItemType Directory -Force -ErrorAction Stop | Out-Null
    foreach ($item in @(Get-ChildItem -LiteralPath $SourcePath -Force -ErrorAction Stop)) {
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            $State.SkippedReparsePoints = [int]$State.SkippedReparsePoints + 1
            continue
        }

        $destination = Join-Path $DestinationPath $item.Name
        if ($item.PSIsContainer) {
            $null = Copy-DirectorySnapshotSafely -SourcePath $item.FullName -DestinationPath $destination -MaxBytes $MaxBytes -State $State
            continue
        }

        $nextBytes = [long]$State.Bytes + [long]$item.Length
        if ($nextBytes -gt $MaxBytes) {
            throw "Spicetify state exceeds the $MaxBytes-byte preservation limit. No repair changes were made."
        }
        Copy-Item -LiteralPath $item.FullName -Destination $destination -Force -ErrorAction Stop
        $State.FileCount = [int]$State.FileCount + 1
        $State.Bytes = $nextBytes
    }

    return [pscustomobject]$State
}
