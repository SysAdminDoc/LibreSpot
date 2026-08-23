function Get-QuarantineGuidance {
    param([string]$What = 'A verified file')
    return "$What is missing right after LibreSpot verified it. A security product (for example Microsoft Defender) may have quarantined it. Open Windows Security > Virus & threat protection > Protection history. Only restore or allow the file after confirming that it came from the official source and its SHA256 matches LibreSpot's pinned value or the same release's checksums.txt entry. If either check fails or cannot be completed, leave the file blocked and submit it to your security vendor for analysis. LibreSpot will not disable antivirus protection, add exclusions, or restore quarantined files for you."
}
