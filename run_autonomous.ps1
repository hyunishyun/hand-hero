# run_autonomous.ps1
# Runs Claude Code headless in a loop for up to $Hours hours.
# - Each iteration starts a FRESH session that reads CLAUDE_AUTONOMOUS_PLAN.md + AUTO/PROGRESS.md
#   (state lives in files + git, not in the conversation).
# - On usage/session limit: waits until the reset time (if parseable) or polls every $LimitPollMinutes.
# - Stops when AUTO/PROGRESS.md contains "STATUS: ALL_DONE" or the deadline passes.
# - Keeps Windows awake while running.
#
# Usage (PowerShell, in the Unity project root):
#   Set-ExecutionPolicy -Scope Process Bypass
#   .\run_autonomous.ps1
#   .\run_autonomous.ps1 -Hours 12 -UseSkipPermissions   # if allow-list in .claude/settings.json is not enough

param(
    [double]$Hours = 12,
    [string]$ProjectPath = "C:\Users\AISTUDIO\Desktop\Hyun's Playground\MetaAwards\A_4",
    [int]$LimitPollMinutes = 15,
    [int]$MaxTurnsPerSession = 250,
    [switch]$UseSkipPermissions,
    # Ultracode: each session runs with --effort ultracode (multi-agent workflows, much higher usage).
    [switch]$Ultracode
)

$ErrorActionPreference = "Continue"
Set-Location $ProjectPath
# Decode claude's UTF-8 output correctly (Korean text in session logs).
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

$autoDir  = Join-Path $ProjectPath "AUTO"
$logDir   = Join-Path $autoDir "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$runnerLog = Join-Path $logDir "runner.log"
$progress  = Join-Path $autoDir "PROGRESS.md"
$deadline  = (Get-Date).AddHours($Hours)

function Log($msg) {
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $msg
    Write-Host $line
    # Never let a locked log file (indexer, antivirus, editor) break the loop.
    try { [System.IO.File]::AppendAllText($runnerLog, $line + "`r`n") }
    catch { Write-Host "  (runner.log busy, line not written)" }
}

# --- Keep the PC awake (no sleep / no display-off) while this script runs ---
Add-Type -Namespace Win32 -Name Power -MemberDefinition @"
[System.Runtime.InteropServices.DllImport("kernel32.dll")]
public static extern uint SetThreadExecutionState(uint esFlags);
"@
# ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED = 0x80000003.
# Decimal [uint32] literals: in PS 5.1 hex 0x80000000 is a negative Int32 and fails the uint conversion.
$ES_CONTINUOUS = [uint32]2147483648
$ES_AWAKE      = [uint32]2147483651
[Win32.Power]::SetThreadExecutionState($ES_AWAKE) | Out-Null

# Keep this prompt ASCII: Windows PowerShell 5.1 reads BOM-less UTF-8 scripts as ANSI,
# which turned the original Korean prompt into mojibake on the claude command line.
$prompt = @"
You are running in UNATTENDED mode (Phase A). Nobody will answer questions.
Read CLAUDE_AUTONOMOUS_PLAN.md in the project root from start to finish and follow section 2 (the unattended session protocol) exactly.
Check AUTO/PROGRESS.md, AUTO/DECISIONS.md, AUTO/BLOCKERS.md and git log, then continue with the next task.
Finish at most 2 tasks in this session, update and commit PROGRESS.md, then exit.
Write logs and reports in Korean as the plan says; code comments in English.
"@

$limitPattern   = '(usage limit|limit reached|hit your [a-z ]*limit|limit will reset|resets? at|resets? \d|rate.?limit|429|overloaded)'
$consecutiveFast = 0
$iteration       = 0

Log ("=== Autonomous run start. Deadline: {0}{1} ===" -f $deadline, $(if ($Ultracode) { " (ultracode)" } else { "" }))

while ((Get-Date) -lt $deadline) {

    if ((Test-Path $progress) -and (Select-String -Path $progress -Pattern "STATUS: ALL_DONE" -Quiet)) {
        Log "PROGRESS.md reports ALL_DONE. Stopping."
        break
    }

    $iteration++
    $sessionLog = Join-Path $logDir ("session_{0:D3}_{1}.log" -f $iteration, (Get-Date -Format "MMdd_HHmm"))
    Log "Session #$iteration starting -> $sessionLog"

    $claudeArgs = @("-p", $prompt, "--max-turns", "$MaxTurnsPerSession", "--output-format", "text")
    if ($UseSkipPermissions) { $claudeArgs += "--dangerously-skip-permissions" }
    if ($Ultracode) {
        # The "ultracode" keyword is ignored in -p prompts; the effort flag is the documented switch.
        # Workflow must be allowed (no one answers prompts), and -p must not cut a running workflow
        # after its default 10-minute background wait.
        $claudeArgs += @("--effort", "ultracode", "--allowedTools", "Workflow")
        $env:CLAUDE_CODE_PRINT_BG_WAIT_CEILING_MS = "0"
    }

    $start = Get-Date
    $output = & claude @claudeArgs 2>&1 | Tee-Object -FilePath $sessionLog
    $exit = $LASTEXITCODE
    $elapsed = ((Get-Date) - $start).TotalSeconds
    $text = ($output | Out-String)

    Log ("Session #{0} ended. exit={1}, {2:N0}s" -f $iteration, $exit, $elapsed)

    # --- Usage / session limit handling ---
    if ($text -match $limitPattern) {
        $waitUntil = $null
        # Some versions print an epoch after a pipe, e.g. "Claude AI usage limit reached|1760000000"
        if ($text -match 'limit reached\|(\d{10})') {
            $waitUntil = [DateTimeOffset]::FromUnixTimeSeconds([int64]$Matches[1]).LocalDateTime.AddMinutes(1)
        }
        # Newer versions print a clock time, e.g. "resets 3pm" / "reset at 3:30 PM (America/New_York)".
        # The time zone is ignored (this PC's local zone is assumed); a past time means tomorrow.
        # Capped at 5 h 15 min so a misread never parks the runner for a day.
        elseif ($text -match 'resets?(?:\s+at)?\s+(\d{1,2})(?::(\d{2}))?\s*([ap]m)') {
            $hour = [int]$Matches[1] % 12
            if ($Matches[3] -ieq 'pm') { $hour += 12 }
            $minute = 0
            if ($Matches[2]) { $minute = [int]$Matches[2] }
            $reset = (Get-Date).Date.AddHours($hour).AddMinutes($minute)
            if ($reset -le (Get-Date)) { $reset = $reset.AddDays(1) }
            $cap = (Get-Date).AddMinutes(315)
            if ($reset -gt $cap) { $reset = $cap }
            $waitUntil = $reset.AddMinutes(2)
        }
        if ($waitUntil -and $waitUntil -gt (Get-Date)) {
            Log "Limit hit. Sleeping until $waitUntil"
            while ((Get-Date) -lt $waitUntil -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 60 }
        } else {
            Log "Limit hit (reset time unknown). Polling again in $LimitPollMinutes min."
            Start-Sleep -Seconds ($LimitPollMinutes * 60)
        }
        $consecutiveFast = 0
        continue
    }

    # --- Crash / misconfiguration guard: many very short sessions in a row ---
    if ($elapsed -lt 60) { $consecutiveFast++ } else { $consecutiveFast = 0 }
    if ($consecutiveFast -ge 5) {
        Log "5 very short sessions in a row. Backing off 20 min. Check $sessionLog."
        Start-Sleep -Seconds 1200
        $consecutiveFast = 0
        continue
    }

    Start-Sleep -Seconds 20
}

[Win32.Power]::SetThreadExecutionState($ES_CONTINUOUS) | Out-Null
Log "=== Autonomous run finished. See AUTO/REPORT_FOR_HYUN.md ==="
