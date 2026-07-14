function Test-LibreSpotProfileActivationTransaction {
    param([Parameter(Mandatory)][object]$Transaction)

    $transactionId = [string]$Transaction.TransactionId
    $parsedId = [Guid]::Empty
    if ([int]$Transaction.SchemaVersion -ne 1 -or
        -not [Guid]::TryParseExact($transactionId, 'N', [ref]$parsedId) -or
        [string]::IsNullOrWhiteSpace([string]$Transaction.OldProfileId) -or
        [string]::IsNullOrWhiteSpace([string]$Transaction.NewProfileId) -or
        (ConvertTo-LibreSpotProfileId -Name ([string]$Transaction.OldProfileId)) -cne [string]$Transaction.OldProfileId -or
        (ConvertTo-LibreSpotProfileId -Name ([string]$Transaction.NewProfileId)) -cne [string]$Transaction.NewProfileId -or
        [string]$Transaction.OldConfigFingerprint -notmatch '^[0-9a-fA-F]{64}$' -or
        [string]$Transaction.NewConfigFingerprint -notmatch '^[0-9a-fA-F]{64}$' -or
        [string]$Transaction.OldConfigStageFile -cne "profile-activation.$transactionId.previous.staged.json" -or
        [string]$Transaction.NewConfigStageFile -cne "profile-activation.$transactionId.next.staged.json") {
        return $false
    }
    return $true
}
