<#
.SYNOPSIS
  Records CPU, memory, handles and threads of the API process, the JMeter process and the
  machine during a test, and asks JMeter to stop when a resource limit is crossed.

.DESCRIPTION
  Writes one CSV row every -IntervalSeconds to -OutFile. When a limit below is crossed it
  writes the reason to -StopFile; the test plan checks for that file every 2 seconds and
  stops gracefully (nothing new is sent, requests in flight finish). Recording continues
  afterwards so the recovery is captured.

  Stop conditions (each one configurable):
    - the API process exits or disappears                                   (immediately)
    - available memory below -MinAvailableMemoryMB for -BreachSamples samples
    - API private memory above -MaxApiPrivateMB                             (immediately)
    - API handle count above -MaxApiHandles                                 (immediately)
    - API thread count above -MaxApiThreads                                 (immediately)
    - total CPU at or above -MaxCpuPercent for -CpuBreachSamples samples (0 = off)

  The monitor must run on the machine running the API. When JMeter runs elsewhere, point
  -StopFile at a folder both machines can reach (a share), or watch the console and stop
  the test with stop-test.ps1 on the JMeter machine.

  It exits when -DoneFile appears (run-verifyphoto-test.ps1 creates it when JMeter ends)
  or when the JMeter process it saw has been gone for three samples.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\monitor-resources.ps1 -OutFile results\run1\resources.csv -StopFile results\run1\STOP
#>
param(
    [string]$ApiProcessName = 'obsIcaoApi',
    [int]$IntervalSeconds = 5,
    [string]$OutFile = (Join-Path (Get-Location) 'resources.csv'),
    [string]$StopFile = '',
    [string]$DoneFile = '',
    [switch]$RecordOnly,
    [int]$MinAvailableMemoryMB = 500,
    [int]$MaxApiPrivateMB = 3000,
    [int]$MaxApiHandles = 20000,
    [int]$MaxApiThreads = 500,
    [int]$MaxCpuPercent = 0,
    [int]$BreachSamples = 3,
    [int]$CpuBreachSamples = 6
)

$ErrorActionPreference = 'Stop'
$inv = [Globalization.CultureInfo]::InvariantCulture
$cores = [Environment]::ProcessorCount
function Fmt([double]$value) { $value.ToString('0.0', $inv) }

function Get-JMeterProcess {
    $found = @(Get-CimInstance Win32_Process -Filter "Name='java.exe'" -OperationTimeoutSec 10 |
        Where-Object { $_.CommandLine -like '*ApacheJMeter.jar*' -and $_.CommandLine -notlike '*ShutdownClient*' } |
        Sort-Object CreationDate -Descending)
    if ($found.Count -eq 0) { return $null }
    Get-Process -Id $found[0].ProcessId -ErrorAction SilentlyContinue
}

$outDir = Split-Path -Parent $OutFile
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
if (-not (Test-Path $OutFile)) {
    'epoch_ms,time,system_cpu_pct,available_memory_mb,api_pid,api_cores_used,api_cpu_pct,api_private_mb,api_working_set_mb,api_handles,api_threads,jmeter_cores_used,jmeter_working_set_mb,event' |
        Set-Content -Path $OutFile -Encoding ASCII
}

Write-Host "Recording every $IntervalSeconds s to $OutFile ($cores logical CPUs, API process '$ApiProcessName')"
if (-not $RecordOnly) {
    $cpuRule = if ($MaxCpuPercent -gt 0) { "CPU >= $MaxCpuPercent% x$CpuBreachSamples" } else { 'CPU rule off' }
    Write-Host ("Stop limits: API exits | available memory < {0} MB x{1} | API private > {2} MB | API handles > {3} | API threads > {4} | {5}" -f `
        $MinAvailableMemoryMB, $BreachSamples, $MaxApiPrivateMB, $MaxApiHandles, $MaxApiThreads, $cpuRule)
}

$previousCpu = @{}
$clock = [Diagnostics.Stopwatch]::StartNew()
$lastSeconds = 0.0
$apiSeen = $false; $jmeterSeen = $false; $jmeterMissing = 0
$lowMemory = 0; $highCpu = 0; $stopSent = $false

while ($true) {
    if ($DoneFile -and (Test-Path $DoneFile)) { Write-Host 'Test finished; monitor exiting.'; break }
    $nowSeconds = $clock.Elapsed.TotalSeconds
    $wall = $nowSeconds - $lastSeconds
    $lastSeconds = $nowSeconds
    $events = @()

    $systemCpu = [double](Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'" -OperationTimeoutSec 10).PercentProcessorTime
    $availableMb = [double](Get-CimInstance Win32_PerfFormattedData_PerfOS_Memory -OperationTimeoutSec 10).AvailableMBytes
    $api = @(Get-Process -Name $ApiProcessName -ErrorAction SilentlyContinue) | Sort-Object StartTime | Select-Object -First 1
    $jmeter = Get-JMeterProcess

    $coresUsed = @{}
    foreach ($pair in @(@('api', $api), @('jmeter', $jmeter))) {
        $coresUsed[$pair[0]] = ''
        if ($pair[1]) {
            $key = "$($pair[0])-$($pair[1].Id)"
            $cpu = $pair[1].TotalProcessorTime.TotalSeconds
            if ($previousCpu.ContainsKey($key) -and $wall -gt 0) { $coresUsed[$pair[0]] = ($cpu - $previousCpu[$key]) / $wall }
            $previousCpu[$key] = $cpu
        }
    }

    $apiPrivate = if ($api) { $api.PrivateMemorySize64 / 1MB } else { 0 }
    $apiHandles = if ($api) { $api.HandleCount } else { 0 }
    $apiThreads = if ($api) { $api.Threads.Count } else { 0 }

    if ($api) { $apiSeen = $true }
    if ($jmeter) { $jmeterSeen = $true; $jmeterMissing = 0 } elseif ($jmeterSeen) { $jmeterMissing++ }
    $lowMemory = if ($availableMb -lt $MinAvailableMemoryMB) { $lowMemory + 1 } else { 0 }
    $highCpu = if ($MaxCpuPercent -gt 0 -and $systemCpu -ge $MaxCpuPercent) { $highCpu + 1 } else { 0 }

    $reason = $null
    if ($apiSeen -and -not $api) { $reason = "API process '$ApiProcessName' is no longer running" }
    elseif ($api -and $apiPrivate -gt $MaxApiPrivateMB) { $reason = "API private memory $(Fmt $apiPrivate) MB is above MaxApiPrivateMB=$MaxApiPrivateMB" }
    elseif ($api -and $apiHandles -gt $MaxApiHandles) { $reason = "API handle count $apiHandles is above MaxApiHandles=$MaxApiHandles" }
    elseif ($api -and $apiThreads -gt $MaxApiThreads) { $reason = "API thread count $apiThreads is above MaxApiThreads=$MaxApiThreads" }
    elseif ($lowMemory -ge $BreachSamples) { $reason = "available memory below MinAvailableMemoryMB=$MinAvailableMemoryMB for $lowMemory samples (now $(Fmt $availableMb) MB)" }
    elseif ($highCpu -ge $CpuBreachSamples) { $reason = "total CPU at or above MaxCpuPercent=$MaxCpuPercent% for $highCpu samples" }

    if ($reason) {
        $events += $reason
        if (-not $RecordOnly -and -not $stopSent -and $StopFile) {
            $stopSent = $true
            "RESOURCE LIMIT: $reason" | Set-Content -Path $StopFile -Encoding ASCII
            Write-Host "$(Get-Date -Format 'HH:mm:ss')  RESOURCE LIMIT: $reason -> wrote $StopFile" -ForegroundColor Red
        }
    }
    if (-not $api) { $events += 'API process not found' }
    if ($availableMb -lt 2 * $MinAvailableMemoryMB) {
        # Names the largest processes, so a stop caused by another application is recognisable.
        $largest = Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 4 |
            ForEach-Object { "$($_.ProcessName) $([int]($_.WorkingSet64 / 1MB)) MB" }
        $events += 'low memory; largest processes: ' + ($largest -join ' / ')
    }

    $apiCores = $coresUsed['api']
    $row = @(
        [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds(),
        (Get-Date).ToString('yyyy-MM-dd HH:mm:ss'),
        (Fmt $systemCpu),
        (Fmt $availableMb),
        $(if ($api) { $api.Id } else { '' }),
        $(if ($apiCores -ne '') { Fmt $apiCores } else { '' }),
        $(if ($apiCores -ne '') { Fmt (100 * $apiCores / $cores) } else { '' }),
        $(if ($api) { Fmt $apiPrivate } else { '' }),
        $(if ($api) { Fmt ($api.WorkingSet64 / 1MB) } else { '' }),
        $(if ($api) { $apiHandles } else { '' }),
        $(if ($api) { $apiThreads } else { '' }),
        $(if ($coresUsed['jmeter'] -ne '') { Fmt $coresUsed['jmeter'] } else { '' }),
        $(if ($jmeter) { Fmt ($jmeter.WorkingSet64 / 1MB) } else { '' }),
        (($events -join '; ') -replace ',', ' ')
    )
    Add-Content -Path $OutFile -Value ($row -join ',') -Encoding ASCII
    Write-Host ("{0}  CPU {1,5}%  avail {2,6} MB | API {3,4} cores priv {4,7} MB handles {5,5} threads {6,3} | JMeter {7,4} cores {8}" -f `
        $row[1], $row[2], $row[3], $row[5], $row[7], $row[9], $row[10], $row[11], $row[13])

    if ($jmeterMissing -ge 3) { Write-Host 'JMeter is no longer running; monitor exiting.'; break }
    Start-Sleep -Seconds $IntervalSeconds
}
