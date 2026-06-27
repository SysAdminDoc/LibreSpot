function Get-QuarantineGuidance {
    param([string]$What = 'A verified file')
    return "$What is missing right after LibreSpot verified it. A security product (for example Microsoft Defender) may have quarantined it. Open Windows Security > Virus & threat protection > Protection history; if the file is listed, restore it and re-run LibreSpot. LibreSpot will not disable your antivirus, add exclusions, or restore quarantined files for you."
}
