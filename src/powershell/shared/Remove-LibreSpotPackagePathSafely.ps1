function Remove-LibreSpotPackagePathSafely {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) { return $false }

    function Remove-PackagePathNode {
        param([Parameter(Mandatory = $true)][string]$NodePath)

        $item = Get-Item -LiteralPath $NodePath -Force -ErrorAction Stop
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            # A reparse point is unlinked as one node. Never enumerate it, so a
            # junction cannot redirect cleanup outside the transaction root.
            if ($item.PSIsContainer) {
                [System.IO.Directory]::Delete($item.FullName)
            } else {
                [System.IO.File]::Delete($item.FullName)
            }
            return
        }

        if ($item.PSIsContainer) {
            foreach ($child in @(Get-ChildItem -LiteralPath $item.FullName -Force -ErrorAction Stop)) {
                Remove-PackagePathNode -NodePath $child.FullName
            }
            [System.IO.Directory]::Delete($item.FullName)
        } else {
            [System.IO.File]::Delete($item.FullName)
        }
    }

    Remove-PackagePathNode -NodePath $Path
    return $true
}
