function Move-ConfigFileToQuarantine {
    [CmdletBinding(SupportsShouldProcess)]
    param([string]$Reason)
    $reasonSuffix = if ([string]::IsNullOrWhiteSpace($Reason)) { '' } else { " Reason: $Reason" }
    try {
        if (-not (Test-Path -LiteralPath $global:CONFIG_DIR)) {
            New-Item -Path $global:CONFIG_DIR -ItemType Directory -Force | Out-Null
        }
        if (Test-Path -LiteralPath $global:CONFIG_PATH) {
            $stamp = Get-Date -Format 'yyyyMMdd-HHmmssfff'
            $quarantinePath = $null
            for ($attempt = 0; $attempt -lt 10; $attempt++) {
                $suffix = if ($attempt -eq 0) { '' } else { "-$attempt" }
                $candidateName = "config.corrupt.$stamp$suffix.json"
                $candidatePath = Join-Path $global:CONFIG_DIR $candidateName
                if (-not (Test-Path -LiteralPath $candidatePath)) {
                    $quarantinePath = $candidatePath
                    break
                }
            }
            if (-not $quarantinePath) {
                $quarantinePath = Join-Path $global:CONFIG_DIR ("config.corrupt.{0}.json" -f [Guid]::NewGuid().ToString('N'))
            }

            if ($PSCmdlet.ShouldProcess($global:CONFIG_PATH, 'Quarantine corrupted config')) {
                Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Planned' -WouldChange $true -Reversible $true -RollbackHint 'Restore the quarantined file manually.'
                Move-Item -LiteralPath $global:CONFIG_PATH -Destination $quarantinePath -ErrorAction Stop
                Write-OperationJournalEntry -Phase 'config' -Target $global:CONFIG_PATH -SafetyDecision 'Allowed' -Result 'Quarantined' -WouldChange $true -Reversible $true -RollbackHint 'Restore the quarantined file manually.'
                $quarantineName = Split-Path -Path $quarantinePath -Leaf
                $script:ConfigLoadWarning = "LibreSpot reset the saved settings because the config file could not be read safely.$reasonSuffix The previous file was moved to $quarantineName."
            }
        } else {
            $script:ConfigLoadWarning = "LibreSpot reset the saved settings because the config file could not be read safely.$reasonSuffix"
        }
    } catch {
        $script:ConfigLoadWarning = 'LibreSpot reset the saved settings because the config file could not be read safely, but it could not move the original file aside automatically.'
    }
    try {
        if ($Reason) { Write-Log "Config reset: $Reason" -Level 'WARN' }
    } catch {}
}
