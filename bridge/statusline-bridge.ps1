# statusline-bridge.ps1
#
# Registered as Claude Code's `statusLine` command (see SettingsWindow's
# "Register statusLine" button, or wire it in manually — see README.md).
# Claude Code pipes one JSON object per invocation on stdin; this script
# extracts rate_limits (if present) and writes it to the shared snapshot
# file the tray app watches. It also prints one line back so the terminal's
# own status line stays useful.
#
# Runs entirely locally — no network calls, no API tokens spent.

$ErrorActionPreference = 'SilentlyContinue'

$raw = [Console]::In.ReadToEnd()
$input_json = $raw | ConvertFrom-Json

$targetDir = Join-Path $env:APPDATA 'ClaudeQuotaTracker'
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
$targetPath = Join-Path $targetDir 'snapshot.json'
$tempPath = "$targetPath.tmp"

$existing = $null
if (Test-Path $targetPath) {
    $existing = Get-Content $targetPath -Raw | ConvertFrom-Json
}

$rateLimits = $input_json.rate_limits

function ConvertWindow($window) {
    if (-not $window) { return $null }
    return @{
        usedPercentage = $window.used_percentage
        resetsAt       = [DateTimeOffset]::FromUnixTimeSeconds([int64]$window.resets_at).ToString('o')
    }
}

$fiveHour = ConvertWindow $rateLimits.five_hour
$sevenDay = ConvertWindow $rateLimits.seven_day

# rate_limits is only present after the session's first API response (see
# the plan doc, section 0), so keep the last known good values instead of
# overwriting them with nulls on every statusLine event where it's absent.
$snapshot = [ordered]@{
    updatedAt       = if ($rateLimits) { (Get-Date).ToUniversalTime().ToString('o') } else { $existing.updatedAt }
    sourceSessionId = $input_json.session_id
    fiveHour        = if ($fiveHour) { $fiveHour } else { $existing.fiveHour }
    sevenDay        = if ($sevenDay) { $sevenDay } else { $existing.sevenDay }
}

$snapshot | ConvertTo-Json -Depth 5 | Set-Content -Path $tempPath -Encoding utf8
Move-Item -Path $tempPath -Destination $targetPath -Force

if ($fiveHour) {
    Write-Output ("Quota: {0:N0}% (tray app active)" -f $fiveHour.usedPercentage)
} else {
    Write-Output "Quota: -- (tray app active)"
}
