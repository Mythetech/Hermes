# Copyright (c) Mythetech. Licensed under the MIT License.
# Runs SmokeTestApp once per deliberate fault and checks the shared verdict rules catch each one.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $AppPath,
    [Parameter(Mandatory)] [string] $VerdictScript,
    [string] $OutputDir = 'smoke-output'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. $VerdictScript

$cases = @(
    @{ Fault = 'none';   Passed = $true;  Expect = 'HERMES_SMOKE_RESULT: PASSED';                   Timeout = 60 }
    @{ Fault = 'check';  Passed = $false; Expect = 'HERMES_SMOKE_CHECK_FAIL: smoke-sample/failing'; Timeout = 60 }
    @{ Fault = 'render'; Passed = $false; Expect = 'HERMES_SMOKE_ERROR: blazor:';                   Timeout = 20 }
    @{ Fault = 'gate';   Passed = $false; Expect = 'timed out waiting for smoke-sample-gate';       Timeout = 15 }
    @{ Fault = 'log';    Passed = $false; Expect = 'HERMES_SMOKE_ERROR: log:';                      Timeout = 60 }
    @{ Fault = 'dialog'; Passed = $false; Expect = 'HERMES_SMOKE_ERROR: dialog:';                   Timeout = 60 }
)

$failures = [System.Collections.Generic.List[string]]::new()
$root = (New-Item -ItemType Directory -Force -Path $OutputDir).FullName
$app = (Resolve-Path -LiteralPath $AppPath).Path

foreach ($case in $cases) {
    $dir = (New-Item -ItemType Directory -Force -Path (Join-Path $root $case.Fault)).FullName
    $stdout = Join-Path $dir 'app-stdout.log'
    $stderr = Join-Path $dir 'app-stderr.log'
    $result = Join-Path $dir 'result.json'

    $env:HERMES_SMOKE_TEST = '1'
    $env:HERMES_SMOKE_TEST_TIMEOUT = "$($case.Timeout)"
    $env:HERMES_SMOKE_TEST_RESULT = $result
    $env:SMOKE_SAMPLE_FAULT = $case.Fault

    Write-Output "=== fault: $($case.Fault)"
    $process = Start-Process -FilePath $app -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    # Reading Handle now keeps ExitCode available after the process ends on Windows.
    $null = $process.Handle
    $timedOut = -not $process.WaitForExit(($case.Timeout + 30) * 1000)
    if ($timedOut) {
        $process.Kill($true)
        $null = $process.WaitForExit(10000)
    }

    $log = @(Get-Content -LiteralPath $stdout -ErrorAction SilentlyContinue)
    $json = if (Test-Path -LiteralPath $result) { [string](Get-Content -LiteralPath $result -Raw) } else { '' }
    $exitCode = if ($timedOut) { $null } else { $process.ExitCode }
    $verdict = Get-SmokeVerdict -LogLines $log -ResultJson $json -Mode verdict -ExitCode $exitCode -TimedOut $timedOut

    $log | ForEach-Object { Write-Output "  $_" }
    Write-Output "  verdict: passed=$($verdict.Passed) reason=$($verdict.Reason)"

    if ($verdict.Passed -ne $case.Passed) {
        $failures.Add("$($case.Fault): expected passed=$($case.Passed) but the verdict was passed=$($verdict.Passed) ($($verdict.Reason))")
    }
    elseif (-not (($log -join "`n").Contains($case.Expect))) {
        $failures.Add("$($case.Fault): the log never contained '$($case.Expect)'")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Output "::error::$_" }
    exit 1
}

Write-Output "All $($cases.Count) smoke fault cases behaved as expected"
