function Enter-LibreSpotMutationLease {
    [CmdletBinding()]
    param(
        [string]$Label = 'LibreSpot mutation',
        [ValidateRange(1, 3600)][int]$TimeoutSeconds = 30,
        [ValidateRange(10, 5000)][int]$RetryMilliseconds = 100
    )

    $targetValues = @(
        [string]$global:SPOTIFY_EXE_PATH
        [string]$global:SPICETIFY_DIR
        [string]$global:SPICETIFY_CONFIG_DIR
    )
    if ($targetValues.Count -ne 3 -or @($targetValues | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -ne 0) {
        throw 'LibreSpot could not resolve the canonical Spotify and Spicetify mutation targets.'
    }

    $canonicalTargets = @($targetValues | ForEach-Object {
        $expanded = [Environment]::ExpandEnvironmentVariables($_.Trim())
        try {
            [System.IO.Path]::GetFullPath($expanded).TrimEnd([char[]]@('\', '/')).ToUpperInvariant()
        } catch {
            throw "LibreSpot could not canonicalize a mutation target: $expanded"
        }
    } | Sort-Object -Unique)
    $userIdentity = $null
    try { $userIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value } catch {}
    if ([string]::IsNullOrWhiteSpace($userIdentity)) {
        $userIdentity = if ($env:USERDOMAIN -and $env:USERDOMAIN -ne $env:COMPUTERNAME) {
            "$env:USERDOMAIN\$env:USERNAME"
        } else { $env:USERNAME }
    }
    $targetIdentity = "LibreSpotMutationLease/v1|user=$userIdentity|targets=$($canonicalTargets -join '|')"

    $hash = [System.Security.Cryptography.SHA256]::Create()
    try {
        $digest = $hash.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($targetIdentity))
    } finally {
        $hash.Dispose()
    }
    $fileName = ([System.BitConverter]::ToString($digest)).Replace('-', '').ToLowerInvariant() + '.lock'
    $leaseRoot = Join-Path $env:LOCALAPPDATA 'LibreSpot\mutation-leases'
    if (-not (Test-Path -LiteralPath $leaseRoot -PathType Container)) {
        New-Item -Path $leaseRoot -ItemType Directory -Force -ErrorAction Stop | Out-Null
    }
    $leasePath = Join-Path $leaseRoot $fileName

    if ($null -eq $global:LibreSpotMutationLeases) {
        $global:LibreSpotMutationLeases = @{}
    }
    $existing = $global:LibreSpotMutationLeases[$leasePath]
    if ($null -ne $existing) {
        $existing.Depth = [int]$existing.Depth + 1
        return [pscustomobject]@{ Key = $leasePath; Reentrant = $true; Label = $Label }
    }

    $readOwner = {
        param([string]$Path)
        try {
            if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
            $raw = [System.IO.File]::ReadAllText($Path)
            if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
            return ($raw | ConvertFrom-Json -ErrorAction Stop)
        } catch {
            return $null
        }
    }
    $hasLiveDescendant = {
        param([int]$RootPid)
        try {
            $processes = @(Get-CimInstance -ClassName Win32_Process -ErrorAction Stop | Select-Object ProcessId, ParentProcessId)
        } catch {
            return $false
        }
        $pending = New-Object 'System.Collections.Generic.Queue[int]'
        $seen = New-Object 'System.Collections.Generic.HashSet[int]'
        $pending.Enqueue($RootPid)
        $null = $seen.Add($RootPid)
        while ($pending.Count -gt 0) {
            $parent = $pending.Dequeue()
            foreach ($candidate in $processes) {
                if ([int]$candidate.ParentProcessId -eq $parent -and $seen.Add([int]$candidate.ProcessId)) {
                    return $true
                }
            }
        }
        return $false
    }

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $stream = $null
    while ($null -eq $stream) {
        $owner = & $readOwner $leasePath
        $ownerPid = 0
        [DateTime]$ownerStart = [DateTime]::MinValue
        $ownerTimestampValid = $false
        try {
            $ownerStart = [DateTime]::Parse([string]$owner.startedAtUtc, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind)
            $ownerTimestampValid = $true
        } catch {}
        if ($owner -and [int]::TryParse([string]$owner.pid, [ref]$ownerPid) -and $ownerTimestampValid) {
            $ownerMatches = $false
            $ownerAlive = $false
            try {
                $ownerProcess = Get-Process -Id $ownerPid -ErrorAction Stop
                $ownerMatches = [Math]::Abs(($ownerProcess.StartTime.ToUniversalTime() - $ownerStart.ToUniversalTime()).TotalSeconds) -le 5
                $ownerAlive = $ownerMatches
            } catch {
                $ownerMatches = $true
            }
            if ($ownerMatches -and -not $ownerAlive -and (& $hasLiveDescendant $ownerPid)) {
                if ([DateTime]::UtcNow -ge $deadline) {
                    throw "LIBRESPOT_MUTATION_BUSY: Another LibreSpot operation left installer descendants running for '$Label'. The operation was deferred without changing Spotify or Spicetify."
                }
                Start-Sleep -Milliseconds $RetryMilliseconds
                continue
            }
        }

        try {
            $stream = [System.IO.File]::Open(
                $leasePath,
                [System.IO.FileMode]::OpenOrCreate,
                [System.IO.FileAccess]::ReadWrite,
                [System.IO.FileShare]::None)
        } catch [System.IO.IOException] {
            if ([DateTime]::UtcNow -ge $deadline) {
                throw "LIBRESPOT_MUTATION_BUSY: Another LibreSpot operation is using the canonical Spotify and Spicetify installation. '$Label' was deferred without changing Spotify or Spicetify."
            }
            Start-Sleep -Milliseconds $RetryMilliseconds
        } catch {
            throw "LibreSpot could not open its per-user mutation lease: $($_.Exception.Message)"
        }

        # The owner can terminate after the pre-open check and before the OS
        # releases its handle. Re-read the marker through our exclusive handle
        # so an installer child still writing for that owner cannot be bypassed.
        if ($stream) {
            $postOpenOwner = $null
            try {
                $stream.Position = 0
                $reader = [System.IO.StreamReader]::new($stream, [System.Text.Encoding]::UTF8, $true, 1024, $true)
                try {
                    $postOpenRaw = $reader.ReadToEnd()
                } finally {
                    $reader.Dispose()
                }
                if (-not [string]::IsNullOrWhiteSpace($postOpenRaw)) {
                    $postOpenOwner = $postOpenRaw | ConvertFrom-Json -ErrorAction Stop
                }
            } catch {
                $postOpenOwner = $null
            }
            $postOpenPid = 0
            [DateTime]$postOpenStart = [DateTime]::MinValue
            $postOpenTimestampValid = $false
            try {
                $postOpenStart = [DateTime]::Parse([string]$postOpenOwner.startedAtUtc, [System.Globalization.CultureInfo]::InvariantCulture, [System.Globalization.DateTimeStyles]::RoundtripKind)
                $postOpenTimestampValid = $true
            } catch {}
            if ($postOpenOwner -and [int]::TryParse([string]$postOpenOwner.pid, [ref]$postOpenPid) -and $postOpenTimestampValid) {
                $postOpenMatches = $false
                $postOpenAlive = $false
                try {
                    $postOpenProcess = Get-Process -Id $postOpenPid -ErrorAction Stop
                    $postOpenMatches = [Math]::Abs(($postOpenProcess.StartTime.ToUniversalTime() - $postOpenStart.ToUniversalTime()).TotalSeconds) -le 5
                    $postOpenAlive = $postOpenMatches
                } catch {
                    $postOpenMatches = $true
                }
                if ($postOpenMatches -and -not $postOpenAlive -and (& $hasLiveDescendant $postOpenPid)) {
                    $stream.Dispose()
                    $stream = $null
                    if ([DateTime]::UtcNow -ge $deadline) {
                        throw "LIBRESPOT_MUTATION_BUSY: Another LibreSpot operation left installer descendants running for '$Label'. The operation was deferred without changing Spotify or Spicetify."
                    }
                    Start-Sleep -Milliseconds $RetryMilliseconds
                }
            }
        }
    }

    try {
        $ownerRecord = [ordered]@{
            schemaVersion = 1
            pid = [int]$PID
            startedAtUtc = (Get-Process -Id $PID -ErrorAction Stop).StartTime.ToUniversalTime().ToString('o')
            targetIdentity = $targetIdentity
            label = [string]$Label
        }
        $ownerBytes = [System.Text.Encoding]::UTF8.GetBytes(($ownerRecord | ConvertTo-Json -Compress))
        $stream.SetLength(0)
        $stream.Position = 0
        $stream.Write($ownerBytes, 0, $ownerBytes.Length)
        $stream.Flush()
    } catch {
        try { $stream.Dispose() } catch {}
        throw "LibreSpot could not publish its mutation lease owner record: $($_.Exception.Message)"
    }

    $global:LibreSpotMutationLeases[$leasePath] = [pscustomobject]@{ Stream = $stream; Depth = 1 }
    return [pscustomobject]@{ Key = $leasePath; Reentrant = $false; Label = $Label }
}
