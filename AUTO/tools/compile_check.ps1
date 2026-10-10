# compile_check.ps1
# Batch-mode Unity compile check / EditMode tests / executeMethod for the autonomous run.
#
# Usage (from the project root):
#   powershell -ExecutionPolicy Bypass -File AUTO\tools\compile_check.ps1
#   powershell -ExecutionPolicy Bypass -File AUTO\tools\compile_check.ps1 -Tests
#   powershell -ExecutionPolicy Bypass -File AUTO\tools\compile_check.ps1 -ExecuteMethod HandHero.EditorTools.HandHeroSceneBuilder.BuildAll
#   powershell -ExecutionPolicy Bypass -File AUTO\tools\compile_check.ps1 -BuildTarget Android -TimeoutMinutes 120 -ExecuteMethod HandHero.EditorTools.BuildScript.BuildQuestApk
#   (BuildQuestApkRelease / BuildQuestApkDev regenerate the scenes first, which resets inspector
#    edits; BuildQuestApkReleaseKeepScenes / BuildQuestApkDevKeepScenes build the saved scenes as they are)
#
# Exit codes:
#   0 = OK
#   1 = compile errors (error CS...) or failed tests / method
#   2 = Unity exited non-zero without a recognizable reason (see log)
#   3 = the exact editor version from ProjectVersion.txt is not installed (NEVER use another version:
#       opening the project with a different version upgrades it)
#   4 = another Unity instance has the project open / is running
#
# ASCII only: Windows PowerShell 5.1 reads BOM-less scripts as ANSI.

param(
    [switch]$Tests,
    [string]$ExecuteMethod = "",
    [string]$BuildTarget = "",
    # Defaults to the project this script lives in (AUTO\tools\..\..), so a clone anywhere works.
    [string]$ProjectPath = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent),
    [int]$TimeoutMinutes = 60
)

$ErrorActionPreference = "Stop"

$versionFile = Join-Path $ProjectPath "ProjectSettings\ProjectVersion.txt"
$versionLine = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(\S+)' | Select-Object -First 1
$version = $versionLine.Matches[0].Groups[1].Value
$unityExe = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"

if (-not (Test-Path $unityExe)) {
    Write-Output "RESULT: EDITOR_MISSING - Unity $version not found at $unityExe. Do NOT use another editor version."
    exit 3
}

# Only editor processes count; Unity Hub ships its own "unity.exe serve" helper.
$running = Get-Process -Name "Unity" -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path -like "*\Editor\Unity.exe" }
if ($running) {
    Write-Output "RESULT: UNITY_RUNNING - a Unity process is already running (PID $($running.Id -join ',')). Close it first."
    exit 4
}

$logDir = Join-Path $ProjectPath "AUTO\logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

if ($Tests) {
    $logFile = Join-Path $logDir "tests.log"
    $resultsFile = Join-Path $logDir "tests.xml"
    if (Test-Path $resultsFile) { Remove-Item $resultsFile }
    $unityArgs = @("-batchmode", "-nographics", "-projectPath", "`"$ProjectPath`"",
                   "-runTests", "-testPlatform", "EditMode",
                   "-testResults", "`"$resultsFile`"", "-logFile", "`"$logFile`"")
} elseif ($ExecuteMethod) {
    $logFile = Join-Path $logDir "execute.log"
    $unityArgs = @("-batchmode", "-nographics", "-quit", "-projectPath", "`"$ProjectPath`"",
                   "-executeMethod", $ExecuteMethod, "-logFile", "`"$logFile`"")
} else {
    $logFile = Join-Path $logDir "compile.log"
    $unityArgs = @("-batchmode", "-nographics", "-quit", "-projectPath", "`"$ProjectPath`"",
                   "-logFile", "`"$logFile`"")
}
# Start the editor on this platform (e.g. Android for the APK build) instead of switching in-process.
if ($BuildTarget) { $unityArgs += @("-buildTarget", $BuildTarget) }

Write-Output "Running Unity $version ($(if ($Tests) {'EditMode tests'} elseif ($ExecuteMethod) {"executeMethod $ExecuteMethod"} else {'compile check'}))..."
$proc = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -PassThru -NoNewWindow
# PS 5.1 quirk: ExitCode stays empty unless the process handle is opened before waiting.
$null = $proc.Handle
if (-not $proc.WaitForExit($TimeoutMinutes * 60 * 1000)) {
    $proc.Kill()
    Write-Output "RESULT: TIMEOUT after $TimeoutMinutes min. See $logFile"
    exit 2
}
$exitCode = $proc.ExitCode
$log = if (Test-Path $logFile) { Get-Content $logFile -Raw } else { "" }

if ($log -match "another Unity instance|It looks like another Unity instance is running") {
    Write-Output "RESULT: UNITY_RUNNING - project is locked by another Unity instance. See $logFile"
    exit 4
}

$csErrors = Select-String -Path $logFile -Pattern "error CS\d+" -ErrorAction SilentlyContinue |
    ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique
if ($csErrors) {
    Write-Output "RESULT: COMPILE_ERRORS ($($csErrors.Count) unique)"
    $csErrors | Select-Object -First 40 | ForEach-Object { Write-Output "  $_" }
    exit 1
}

# BuildScript NOTE lines (deep review DR-4): saved scene edits that an APK build
# resets, and the result of BuildScript.CheckSceneEdits.
$notes = Select-String -Path $logFile -Pattern '\[BuildScript\] NOTE:' -ErrorAction SilentlyContinue |
    ForEach-Object { $_.Line.Trim() } | Select-Object -Unique
if ($notes) { $notes | ForEach-Object { Write-Output "  $_" } }

if ($Tests) {
    if (-not (Test-Path $resultsFile)) {
        Write-Output "RESULT: TESTS_NO_RESULTS (Unity exit $exitCode). See $logFile"
        exit 2
    }
    [xml]$xml = Get-Content $resultsFile
    $run = $xml.'test-run'
    Write-Output "Tests: total=$($run.total) passed=$($run.passed) failed=$($run.failed) skipped=$($run.skipped)"
    if ([int]$run.failed -gt 0) {
        $xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
            Write-Output "  FAILED: $($_.fullname)"
            Write-Output "    $($_.failure.message.'#cdata-section')"
        }
        Write-Output "RESULT: TESTS_FAILED"
        exit 1
    }
    Write-Output "RESULT: OK"
    exit 0
}

if ($exitCode -ne 0) {
    Write-Output "RESULT: UNITY_EXIT_$exitCode (no error CS found). Last log lines:"
    Get-Content $logFile -Tail 30 | ForEach-Object { Write-Output "  $_" }
    exit 2
}

Write-Output "RESULT: OK"
exit 0
