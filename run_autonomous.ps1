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
    [switch]$UseSkipPermissions
)

$ErrorActionPreference = "Continue"
Set-Location $ProjectPath

$autoDir  = Join-Path $ProjectPath "AUTO"
$logDir   = Join-Path $autoDir "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$runnerLog = Join-Path $logDir "runner.log"
$progress  = Join-Path $autoDir "PROGRESS.md"
$deadline  = (Get-Date).AddHours($Hours)

function Log($msg) {
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $msg
    Write-Host $line
    Add-Content -Path $runnerLog -Value $line
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

$prompt = @"
너는 무인 모드(Phase A)로 실행 중이다. 아무도 질문에 답하지 않는다.
프로젝트 루트의 CLAUDE_AUTONOMOUS_PLAN.md를 처음부터 끝까지 읽고, 섹션 2 '무인 세션 프로토콜'을 그대로 따라라.
AUTO/PROGRESS.md, AUTO/DECISIONS.md, AUTO/BLOCKERS.md, git log를 확인한 뒤 다음 태스크를 진행하라.
이번 세션에서는 태스크를 최대 2개 끝내고, PROGRESS.md를 최신화·커밋한 뒤 종료하라.
"@

$limitPattern   = '(usage limit|limit reached|limit will reset|resets? at|rate.?limit|429|overloaded)'
$consecutiveFast = 0
$iteration       = 0

Log "=== Autonomous run start. Deadline: $deadline ==="

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
