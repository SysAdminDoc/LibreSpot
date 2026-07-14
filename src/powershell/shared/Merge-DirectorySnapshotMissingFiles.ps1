function Merge-DirectorySnapshotMissingFiles {
    param(
        [Parameter(Mandatory)][string]$SourcePath,
        [Parameter(Mandatory)][string]$DestinationPath,
        [hashtable]$State
    )

    if (-not $State) {
        $State = @{ RestoredFileCount = 0; SkippedExistingFiles = 0; SkippedReparsePoints = 0 }
    }
    if (-not (Test-Path -LiteralPath $SourcePath -PathType Container)) {
        return [pscustomobject]$State
    }

    $source = Get-Item -LiteralPath $SourcePath -Force -ErrorAction Stop
    if (($source.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Refusing to restore from reparse-point root: $SourcePath"
    }

    if (Test-Path -LiteralPath $DestinationPath) {
        $destinationRoot = Get-Item -LiteralPath $DestinationPath -Force -ErrorAction Stop
        if (($destinationRoot.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            $State.SkippedReparsePoints = [int]$State.SkippedReparsePoints + 1
            return [pscustomobject]$State
        }
    } else {
        New-Item -Path $DestinationPath -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }

    foreach ($item in @(Get-ChildItem -LiteralPath $SourcePath -Force -ErrorAction Stop)) {
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            $State.SkippedReparsePoints = [int]$State.SkippedReparsePoints + 1
            continue
        }

        $destination = Join-Path $DestinationPath $item.Name
        if ($item.PSIsContainer) {
            $null = Merge-DirectorySnapshotMissingFiles -SourcePath $item.FullName -DestinationPath $destination -State $State
            continue
        }

        if (Test-Path -LiteralPath $destination) {
            $State.SkippedExistingFiles = [int]$State.SkippedExistingFiles + 1
            continue
        }
        Copy-Item -LiteralPath $item.FullName -Destination $destination -ErrorAction Stop
        $State.RestoredFileCount = [int]$State.RestoredFileCount + 1
    }

    return [pscustomobject]$State
}
