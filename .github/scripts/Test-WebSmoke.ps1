# Copyright (c) Mythetech. Licensed under the MIT License.
# Boots each web sample in smoke mode and checks the shared verdict rules, including a page fault that must fail.
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $VerdictScript,
    [string] $SamplesRoot = 'samples',
    [string] $OutputDir = 'web-smoke-output',
    [int] $Timeout = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. $VerdictScript

$cases = @(
    @{ Name = 'web';       Sample = 'WebHelloWorld';    Fault = '';     Passed = $true;  Expect = 'HERMES_SMOKE_CHECK_PASS: web-hello/bridge-echo' }
    @{ Name = 'web-fault'; Sample = 'WebHelloWorld';    Fault = 'page'; Passed = $false; Expect = 'HERMES_SMOKE_ERROR: page: Error: web sample fault' }
    @{ Name = 'react';     Sample = 'ReactHelloWorld';  Fault = '';     Passed = $true;  Expect = 'HERMES_SMOKE_RESULT: PASSED' }
    @{ Name = 'svelte';    Sample = 'SvelteHelloWorld'; Fault = '';     Passed = $true;  Expect = 'HERMES_SMOKE_RESULT: PASSED' }
)

$extension = if ($IsWindows) { '.exe' } else { '' }
$failures = [System.Collections.Generic.List[string]]::new()
$root = (New-Item -ItemType Directory -Force -Path $OutputDir).FullName

foreach ($case in $cases) {
    $app = (Resolve-Path -LiteralPath (Join-Path $SamplesRoot "$($case.Sample)/bin/Release/net11.0/$($case.Sample)$extension")).Path
    $dir = (New-Item -ItemType Directory -Force -Path (Join-Path $root $case.Name)).FullName
    $stdout = Join-Path $dir 'app-stdout.log'
    $stderr = Join-Path $dir 'app-stderr.log'
    $result = Join-Path $dir 'result.json'

    $env:HERMES_SMOKE_TEST = '1'
    $env:HERMES_SMOKE_TEST_TIMEOUT = "$Timeout"
    $env:HERMES_SMOKE_TEST_RESULT = $result
    $env:WEB_SAMPLE_FAULT = $case.Fault

    Write-Output "=== $($case.Name): $($case.Sample)"
    $process = Start-Process -FilePath $app -WorkingDirectory (Split-Path $app) -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
    # Reading Handle now keeps ExitCode available after the process ends on Windows.
    $null = $process.Handle
    $timedOut = -not $process.WaitForExit(($Timeout + 30) * 1000)
    if ($timedOut) {
        $process.Kill($true)
        $null = $process.WaitForExit(10000)
    }
    else {
        # Only the overload without a timeout waits for the redirected output to reach the log file.
        $process.WaitForExit()
    }

    $log = @(Get-Content -LiteralPath $stdout -ErrorAction SilentlyContinue)
    $json = if (Test-Path -LiteralPath $result) { [string](Get-Content -LiteralPath $result -Raw) } else { '' }
    $exitCode = if ($timedOut) { $null } else { $process.ExitCode }
    $verdict = Get-SmokeVerdict -LogLines $log -ResultJson $json -Mode verdict -ExitCode $exitCode -TimedOut $timedOut

    $log | ForEach-Object { Write-Output "  $_" }
    Write-Output "  verdict: passed=$($verdict.Passed) reason=$($verdict.Reason)"

    if ($verdict.Passed -ne $case.Passed) {
        $failures.Add("$($case.Name): expected passed=$($case.Passed) but the verdict was passed=$($verdict.Passed) ($($verdict.Reason))")
    }
    elseif (-not (($log -join "`n").Contains($case.Expect))) {
        $failures.Add("$($case.Name): the log never contained '$($case.Expect)'")
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Output "::error::$_" }
    exit 1
}

Write-Output "All $($cases.Count) web smoke cases behaved as expected"
