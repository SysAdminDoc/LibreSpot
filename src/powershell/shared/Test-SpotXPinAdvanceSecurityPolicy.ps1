function Test-SpotXPinAdvanceSecurityPolicy {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$ScriptPath,
        [Parameter(Mandatory)][string]$CurrentCommit,
        [Parameter(Mandatory)][string]$CandidateCommit,
        [Parameter(Mandatory)][string]$PolicyCommit,
        [Parameter(Mandatory)][string]$RequiredOptOut,
        [Parameter(Mandatory)][bool]$DeclaredDefenderMutations,
        [AllowEmptyString()][string]$DeclaredDefenderOptOut = '',
        [AllowEmptyString()][string]$InvocationArguments = '',
        [switch]$PostDefenderPolicy
    )

    if (-not (Test-Path -LiteralPath $ScriptPath -PathType Leaf)) {
        throw "SpotX candidate entrypoint not found: $ScriptPath"
    }
    if ($CurrentCommit -notmatch '^[0-9a-f]{8,40}$' -or $CandidateCommit -notmatch '^[0-9a-f]{8,40}$') {
        throw 'SpotX pin-advance policy requires hexadecimal commit identifiers.'
    }
    if ($PolicyCommit -cne 'afb4c3fc') {
        throw 'SpotX Defender policy boundary must be upstream commit afb4c3fc.'
    }
    if ($RequiredOptOut -cne '-defender_exclusions_off') {
        throw 'SpotX Defender policy must require the exact upstream -defender_exclusions_off switch.'
    }

    $info = Get-Item -LiteralPath $ScriptPath
    if ($info.Length -le 0 -or $info.Length -gt 1048576) {
        throw "SpotX candidate entrypoint has an invalid size: $($info.Length) bytes."
    }

    $content = [System.IO.File]::ReadAllText($ScriptPath, [System.Text.Encoding]::UTF8)
    $indicators = @()
    foreach ($indicator in @(
        @{ Name = 'Add-MpPreference'; Pattern = '(?i)\bAdd-MpPreference\b' },
        @{ Name = 'Set-MpPreference'; Pattern = '(?i)\bSet-MpPreference\b' },
        @{ Name = 'ExclusionPath'; Pattern = '(?i)-ExclusionPath\b' },
        @{ Name = 'ExclusionProcess'; Pattern = '(?i)-ExclusionProcess\b' }
    )) {
        if ([regex]::IsMatch($content, [string]$indicator.Pattern)) { $indicators += [string]$indicator.Name }
    }

    $containsMutations = $indicators.Count -gt 0
    $declaresUpstreamOptOut = [regex]::IsMatch($content, '(?i)\bdefender_exclusions_off\b')
    $passesOptOut = [regex]::IsMatch($InvocationArguments, '(?i)(?:^|\s)-defender_exclusions_off(?:\s|$)')

    if ($CandidateCommit -ne $CurrentCommit -and -not $PostDefenderPolicy) {
        throw "SpotX pin advance $CandidateCommit must explicitly declare the post-$PolicyCommit Defender policy."
    }
    if ($containsMutations -ne $DeclaredDefenderMutations) {
        throw "SpotX Defender-mutation metadata does not match the candidate entrypoint (detected: $containsMutations; declared: $DeclaredDefenderMutations)."
    }

    $requiresOptOut = $PostDefenderPolicy -or $containsMutations
    if ($requiresOptOut) {
        if (-not $declaresUpstreamOptOut -or $DeclaredDefenderOptOut -cne $RequiredOptOut -or -not $passesOptOut) {
            throw 'SpotX pin advance requires the declared and passed -defender_exclusions_off switch before Defender exclusions can run.'
        }
    } elseif (-not [string]::IsNullOrWhiteSpace($DeclaredDefenderOptOut)) {
        throw 'A safe pre-Defender SpotX candidate must not receive a Defender opt-out argument.'
    }

    return [pscustomobject][ordered]@{
        status                     = 'ok'
        currentCommit              = $CurrentCommit
        candidateCommit            = $CandidateCommit
        policyCommit               = $PolicyCommit
        postDefenderPolicy         = [bool]$PostDefenderPolicy
        requiredOptOut             = $RequiredOptOut
        invocationArguments        = $InvocationArguments
        invocationPassesOptOut     = $passesOptOut
        containsDefenderMutations  = $containsMutations
        defenderMutationIndicators = @($indicators)
        declaresUpstreamOptOut     = $declaresUpstreamOptOut
        adapterOptOut              = $DeclaredDefenderOptOut
    }
}
