function Write-LibreSpotProfilePointer {
    param([string]$Path, [string]$ProfileId)
    if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) { New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null }
    $document = [ordered]@{
        SchemaVersion = 1
        ProfileId     = $ProfileId
        UpdatedAt     = (Get-Date).ToUniversalTime().ToString('o')
    }
    Write-LibreSpotJsonAtomically -Path $Path -Document $document -Depth 4
}
