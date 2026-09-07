function Read-ProcessOutputDelta {
    param(
        [string]$Path,
        [long]$Offset = 0,
        [string]$Remainder = '',
        [int]$MaxChunkBytes = 262144,
        [int]$MaxRemainderCharacters = 32768
    )

    $maxChunkBytes = [Math]::Max(64, [Math]::Min($MaxChunkBytes, 1048576))
    $maxRemainderCharacters = [Math]::Max(64, [Math]::Min($MaxRemainderCharacters, 262144))
    $maxOutputLines = 256
    $maxOutputCharacters = 262144
    $strictUtf8 = New-Object System.Text.UTF8Encoding($false, $true)
    $result = @{
        Offset = $Offset
        Remainder = if ($Remainder.Length -le $maxRemainderCharacters) { $Remainder } else { $Remainder.Substring($Remainder.Length - $maxRemainderCharacters) }
        Lines = @()
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $result }

    try {
        $stream = [System.IO.File]::Open(
            $Path,
            [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read,
            [System.IO.FileShare]::ReadWrite)
        try {
            if ($result.Offset -gt $stream.Length) {
                $result.Offset = 0
                $result.Remainder = ''
            }

            $null = $stream.Seek($result.Offset, [System.IO.SeekOrigin]::Begin)
            $buffer = New-Object byte[] $maxChunkBytes
            $read = $stream.Read($buffer, 0, $buffer.Length)
            if ($read -le 0) {
                $chunk = ''
            } else {
                $decodedBytes = $read
                $chunk = $null
                while ($decodedBytes -gt 0 -and $null -eq $chunk) {
                    try {
                        $chunk = $strictUtf8.GetString($buffer, 0, $decodedBytes)
                    } catch [System.Text.DecoderFallbackException] {
                        # A concurrent writer can leave a partial UTF-8 codepoint
                        # at the capture boundary. Leave those bytes for the next
                        # invocation rather than replacing them or losing offset.
                        $decodedBytes--
                    }
                }
                if ($null -eq $chunk) { $chunk = '' }
                $result.Offset = $result.Offset + $decodedBytes
                if ($result.Offset -eq $decodedBytes -and $decodedBytes -ge 3 -and
                    $buffer[0] -eq 0xEF -and $buffer[1] -eq 0xBB -and $buffer[2] -eq 0xBF) {
                    $chunk = $chunk.Substring(1)
                }
            }
        } finally {
            try { $stream.Dispose() } catch {}
        }

        if ([string]::IsNullOrEmpty($chunk)) { return $result }

        $lines = New-Object System.Collections.Generic.List[string]
        $pending = [string]$result.Remainder
        $fragmentMarker = '[output truncated: oversized line]'
        $batchMarker = '[output truncated: reader batch bounded]'
        $batchMarkerLength = $batchMarker.Length
        $lineCharacters = 0
        $batchTruncationPending = $false
        $batchMarkerAdded = $false
        $addOutputLine = {
            param([string]$Line)

            if ([string]::IsNullOrWhiteSpace($Line)) { return }
            $needMarker = $batchTruncationPending
            $markerSlots = if ($needMarker) { 1 } else { 0 }
            $dropped = $false
            while ($lines.Count + $markerSlots -ge $maxOutputLines -or
                   $lineCharacters + ($markerSlots * $batchMarkerLength) + $Line.Length -gt $maxOutputCharacters) {
                if ($lines.Count -le 0) { break }
                $discardIndex = if ($batchMarkerAdded -and [string]$lines[0] -eq $batchMarker) { 1 } else { 0 }
                if ($discardIndex -ge $lines.Count) { break }
                $discarded = [string]$lines[$discardIndex]
                $lines.RemoveAt($discardIndex)
                $lineCharacters -= $discarded.Length
                $dropped = $true
            }
            if ($dropped -and -not $batchMarkerAdded) {
                $batchTruncationPending = $true
                $needMarker = $true
                $markerSlots = 1
            }
            if ($needMarker) {
                while ($lines.Count + 2 -gt $maxOutputLines -or
                       $lineCharacters + $batchMarkerLength + $Line.Length -gt $maxOutputCharacters) {
                    if ($lines.Count -le 0) { break }
                    $discardIndex = if ($batchMarkerAdded -and [string]$lines[0] -eq $batchMarker) { 1 } else { 0 }
                    if ($discardIndex -ge $lines.Count) { break }
                    $discarded = [string]$lines[$discardIndex]
                    $lines.RemoveAt($discardIndex)
                    $lineCharacters -= $discarded.Length
                }
                if ($lines.Count + 2 -gt $maxOutputLines -or
                    $lineCharacters + $batchMarkerLength + $Line.Length -gt $maxOutputCharacters) { return }
                [void]$lines.Add($batchMarker)
                $lineCharacters += $batchMarkerLength
                $batchTruncationPending = $false
                $batchMarkerAdded = $true
            }
            [void]$lines.Add($Line)
            $lineCharacters += $Line.Length
        }
        $appendSegment = {
            param(
                [string]$Segment,
                [bool]$Complete
            )

            $combined = $pending + [string]$Segment
            while ($combined.Length -gt $maxRemainderCharacters) {
                $fragment = $combined.Substring(0, $maxRemainderCharacters)
                if (-not [string]::IsNullOrWhiteSpace($fragment)) {
                    $null = . $addOutputLine ($fragment + ' ' + $fragmentMarker)
                }
                $combined = $combined.Substring($maxRemainderCharacters)
            }

            if ($Complete) {
                if (-not [string]::IsNullOrWhiteSpace($combined)) {
                    $null = . $addOutputLine $combined
                }
                $pending = ''
            } else {
                $pending = $combined
            }
        }

        $text = [string]$chunk
        $parts = [regex]::Split($text, "\r\n|\n|\r")
        $hasTrailingNewline = $text.EndsWith("`n") -or $text.EndsWith("`r")
        for ($i = 0; $i -lt ($parts.Count - 1); $i++) {
            $null = . $appendSegment ([string]$parts[$i]) $true
        }
        if (-not $hasTrailingNewline -and $parts.Count -gt 0) {
            $null = . $appendSegment ([string]$parts[-1]) $false
        } else {
            $pending = ''
        }

        $result.Remainder = $pending
        $result.Lines = @($lines.ToArray())
    } catch {}
    return $result
}
