<#
.SYNOPSIS
  Runs verifyphoto_stress_test.jmx in JMeter non-GUI mode, records resources, builds the
  HTML dashboard and the summary.

.DESCRIPTION
  1. Checks Java and JMeter, creates results\<time>-<label>\ and copies the settings used.
  2. Asks you to type YES (skip with -Yes): the run sends real load to the API.
  3. Starts monitor-resources.ps1 in the background when the API runs on this machine.
  4. Runs JMeter: results.jtl, jmeter.log, dashboard\index.html.
  5. Writes summary.md and summary.json with summarize-results.ps1.

  Settings come from -Config (properties files, later ones override earlier ones) and -Set
  (NAME=value pairs, applied last). Nothing secret belongs in either; the API has no
  authentication today.

.EXAMPLE
  .\scripts\run-verifyphoto-test.ps1 -Config config\local.properties,config\smoke.properties -Label smoke
.EXAMPLE
  .\scripts\run-verifyphoto-test.ps1 -Label optimized
.EXAMPLE
  .\scripts\run-verifyphoto-test.ps1 -Config config\local.properties,config\server.properties -ApiOnThisMachine:$false -Label server
#>
param(
    [string[]]$Config = @('config\local.properties'),
    [string[]]$Set = @(),
    [string]$Label = 'run',
    [string]$JMeterHome = $(if ($env:JMETER_HOME) { $env:JMETER_HOME } else { 'C:\tools\apache-jmeter-5.6.3' }),
    # 40 requests in flight with ~4 MB responses need ~160 MB; JMeter's own default (1 GB)
    # grows to ~900 MB at 10 TPS, memory the API machine may not have to spare.
    [string]$JMeterHeap = '-Xms128m -Xmx384m',
    [bool]$ApiOnThisMachine = $true,
    [string]$ApiProcessName = 'obsIcaoApi',
    [int]$MinAvailableMemoryMB = 500,
    [int]$MaxApiPrivateMB = 3000,
    [int]$MaxApiHandles = 20000,
    [int]$MaxCpuPercent = 0,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$plan = Join-Path $root 'verifyphoto_stress_test.jmx'
$jmeterBat = Join-Path $JMeterHome 'bin\jmeter.bat'

# --- Prerequisites -------------------------------------------------------------------------
if (-not (Test-Path $jmeterBat)) { throw "JMeter not found at $JMeterHome. Set -JMeterHome or the JMETER_HOME environment variable." }
$java = if ($env:JAVA_HOME -and (Test-Path (Join-Path $env:JAVA_HOME 'bin\java.exe'))) { Join-Path $env:JAVA_HOME 'bin\java.exe' } else { (Get-Command java -ErrorAction SilentlyContinue).Source }
if (-not $java) { throw 'Java not found: install a JDK/JRE 8 or later and put it on PATH or set JAVA_HOME (see README.md).' }

$configFiles = foreach ($c in $Config) {
    $p = if ([IO.Path]::IsPathRooted($c)) { $c } else { Join-Path $root $c }
    if (-not (Test-Path $p)) { throw "Config file not found: $p" }
    (Resolve-Path $p).Path
}

# Effective settings, for the confirmation and the record of the run.
$settings = [ordered]@{}
foreach ($f in $configFiles) {
    Get-Content $f | Where-Object { $_ -match '^\s*[A-Za-z_][A-Za-z0-9_.]*\s*=' } | ForEach-Object {
        $k, $v = $_ -split '=', 2; $settings[$k.Trim()] = $v.Trim()
    }
}
foreach ($s in $Set) { $k, $v = $s -split '=', 2; $settings[$k.Trim()] = $v.Trim() }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$runDir = Join-Path $root "results\$stamp-$Label"
New-Item -ItemType Directory -Force -Path $runDir | Out-Null
$i = 0; foreach ($f in $configFiles) { $i++; Copy-Item $f (Join-Path $runDir ("config-{0}-{1}" -f $i, (Split-Path -Leaf $f))) }
($settings.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) | Set-Content (Join-Path $runDir 'settings-effective.properties') -Encoding ASCII

$stopFile = Join-Path $runDir 'STOP'
$doneFile = Join-Path $runDir 'DONE'

Write-Host ''
Write-Host 'verifyPhoto stress test' -ForegroundColor Cyan
Write-Host ("  Target     {0}://{1}:{2}{3}" -f $settings['PROTOCOL'], $settings['API_HOST'], $settings['API_PORT'], $settings['API_PATH'])
Write-Host ("  Load       ramp {0} -> {1} TPS over {2} s, then {1} TPS for {3} s; at most {4} in flight" -f `
    $settings['RAMP_START_TPS'], $settings['TARGET_TPS'], $settings['RAMP_UP_SECONDS'], $settings['SUSTAIN_SECONDS'], $settings['MAX_IN_FLIGHT'])
Write-Host "  Image      $($settings['IMAGE_PATH'])"
Write-Host "  Results    $runDir"
Write-Host "  Java       $java"
Write-Host "  JMeter     $JMeterHome (heap $JMeterHeap)"
Write-Host "  Monitor    $(if ($ApiOnThisMachine) { "on: process '$ApiProcessName', stop if available memory < $MinAvailableMemoryMB MB, API private > $MaxApiPrivateMB MB, handles > $MaxApiHandles$(if ($MaxCpuPercent) { ", CPU >= $MaxCpuPercent%" })" } else { 'off (API on another machine: run monitor-resources.ps1 there)' })"
if (-not $Yes) {
    $answer = Read-Host 'This sends real load to the API. Type YES to start'
    if ($answer -ne 'YES') { Write-Host 'Cancelled.'; Remove-Item -Recurse -Force $runDir; exit 1 }
}

# --- Resource monitor ------------------------------------------------------------------------
$monitor = $null
if ($ApiOnThisMachine) {
    # Hidden window: a click in a visible console (QuickEdit) would pause the monitor.
    $monitor = Start-Process -FilePath 'powershell.exe' -WindowStyle Hidden -PassThru -ArgumentList @(
        '-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$(Join-Path $PSScriptRoot 'monitor-resources.ps1')`"",
        '-ApiProcessName', $ApiProcessName, '-OutFile', "`"$(Join-Path $runDir 'resources.csv')`"",
        '-StopFile', "`"$stopFile`"", '-DoneFile', "`"$doneFile`"",
        '-MinAvailableMemoryMB', $MinAvailableMemoryMB, '-MaxApiPrivateMB', $MaxApiPrivateMB,
        '-MaxApiHandles', $MaxApiHandles, '-MaxCpuPercent', $MaxCpuPercent) `
        -RedirectStandardOutput (Join-Path $runDir 'monitor.log') -RedirectStandardError (Join-Path $runDir 'monitor.err.log')
    Start-Sleep -Seconds 6   # one sample before the load starts
}

# --- JMeter ----------------------------------------------------------------------------------
$jmeterArgs = @('-n', '-t', $plan)
foreach ($f in $configFiles) { $jmeterArgs += @('-q', $f) }
$jmeterArgs += @('-q', (Join-Path $root 'config\results.properties'),
    '-l', (Join-Path $runDir 'results.jtl'), '-j', (Join-Path $runDir 'jmeter.log'),
    '-e', '-o', (Join-Path $runDir 'dashboard'),
    "-JRESULTS_DIR=$runDir", "-JSTOP_FILE=$stopFile")
foreach ($s in $Set) { $jmeterArgs += "-J$s" }

$env:HEAP = $JMeterHeap
Write-Host "Starting JMeter $(Get-Date -Format 'HH:mm:ss'). Stop early with: .\scripts\stop-test.ps1" -ForegroundColor Cyan
& $jmeterBat @jmeterArgs
$jmeterExit = $LASTEXITCODE

New-Item -ItemType File -Path $doneFile -Force | Out-Null
if ($monitor) { $monitor.WaitForExit(20000) | Out-Null }

if (-not (Test-Path (Join-Path $runDir 'results.jtl'))) {
    Write-Host "JMeter produced no results (exit code $jmeterExit). Last lines of jmeter.log:" -ForegroundColor Red
    Get-Content (Join-Path $runDir 'jmeter.log') -Tail 30 -ErrorAction SilentlyContinue
    exit 1
}
if (Test-Path (Join-Path $runDir 'stop-reason.txt')) {
    Write-Host 'The run was stopped early:' -ForegroundColor Yellow
    Get-Content (Join-Path $runDir 'stop-reason.txt')
}

& (Join-Path $PSScriptRoot 'summarize-results.ps1') -RunDir $runDir
Write-Host "Dashboard: $(Join-Path $runDir 'dashboard\index.html')"
