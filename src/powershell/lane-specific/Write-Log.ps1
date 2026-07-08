function Write-Log { param([string]$Message,[string]$Level='INFO'); Update-UI -Message $Message -Level $Level -IsHeader ($Level -eq 'STEP' -or $Level -eq 'HEADER') }
