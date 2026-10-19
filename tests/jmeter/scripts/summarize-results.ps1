<#
.SYNOPSIS
  Turns one run folder (results.jtl, run-info.properties, resources.csv) into summary.md
  and summary.json: successful throughput against the offered rate, latency percentiles,
  failures by cause, resource use and the acceptance criteria.

.DESCRIPTION
  A successful verification is a load request that passed the plan's assertion: HTTP 200,
  a verified/unverified verdict, the expected verdict, and (STRICT_BODY_MATCH) a body
  identical to the baseline response. Everything else is a failure, counted by FAIL_CLASS.

  Rates are measured in 10-second windows from the schedule start:
    planned    = arrivals the rate profile asks for in the window (the offered rate)
    sent       = requests JMeter actually started in the window
    successful = successful verifications that completed in the window

  Acceptance criteria (thresholds are parameters):
    1. Ramp-up reaches the target: in every full ramp window and the first sustained
       window, successful completions are at least -WindowMinPercent of plan.
    2. Sustained throughput: successful verifications per second over the sustained phase
       are at least -SustainMinPercent of TARGET_TPS, and every full sustained window
       reaches -WindowMinPercent of TARGET_TPS.
    3. Errors: failed requests at most -MaxErrorPercent of all load requests, and no
       safety stop.
    4. Correct results: no invalid or inconsistent verification results.
    5. Responsive and stable: no timeouts, the schedule completed, requests went out on
       time (p99 send lag at most -MaxLagP99Ms) and no resource limit was crossed.
  Latency is reported, not judged: no latency limit was specified.

.EXAMPLE
  .\scripts\summarize-results.ps1 -RunDir results\20261019-170000-optimized
#>
param(
    [Parameter(Mandatory = $true)][string]$RunDir,
    [double]$SustainMinPercent = 99.5,
    [double]$WindowMinPercent = 90,
    [double]$MaxErrorPercent = 1,
    [int]$MaxLagP99Ms = 1000,
    [int]$WindowSeconds = 10
)

$ErrorActionPreference = 'Stop'
$inv = [Globalization.CultureInfo]::InvariantCulture
function N([double]$v, [string]$f = '0.0') { $v.ToString($f, $inv) }
function Pct([double[]]$sorted, [double]$p) {
    if ($sorted.Count -eq 0) { return [double]::NaN }
    $sorted[[Math]::Max(0, [Math]::Ceiling($p / 100 * $sorted.Count) - 1)]
}
function Stats($rows) {
    $ms = [double[]]@($rows | ForEach-Object { [double]$_.elapsed } | Sort-Object)
    if ($ms.Count -eq 0) { return $null }
    [ordered]@{
        count = $ms.Count
        avg = ($ms | Measure-Object -Average).Average
        median = Pct $ms 50; p90 = Pct $ms 90; p95 = Pct $ms 95; p99 = Pct $ms 99
        min = $ms[0]; max = $ms[-1]
    }
}

# --- Inputs ---------------------------------------------------------------------------------
$info = @{}
$infoFile = Join-Path $RunDir 'run-info.properties'
if (Test-Path $infoFile) {
    Get-Content $infoFile | Where-Object { $_ -match '^[^#].*=' } | ForEach-Object {
        $k, $v = $_ -split '=', 2
        $info[$k.Trim()] = ($v.Trim() -replace '\\:', ':' -replace '\\\\', '\')
    }
}
$rows = @(Import-Csv (Join-Path $RunDir 'results.jtl'))
$load = @($rows | Where-Object { $_.label -like 'verifyPhoto_*' -and $_.label -ne 'verifyPhoto_baseline' })
$baselineRow = $rows | Where-Object label -eq 'verifyPhoto_baseline' | Select-Object -First 1
if ($load.Count -eq 0) { throw "No load requests in $RunDir\results.jtl" }

$target = [double]::Parse($info['target_tps'], $inv)
$r0 = [double]::Parse($info['ramp_start_tps'], $inv)
$rampS = [double]::Parse($info['ramp_up_seconds'], $inv)
$sustainS = [double]::Parse($info['sustain_seconds'], $inv)
$start = [long]$info['schedule_start_ms']
$planned = [long]$info['planned_requests']
$stopReason = $info['stop_reason']
$scheduleCompleted = $stopReason -like 'none*'

# Cumulative planned arrivals at t seconds after the schedule start.
function Arrivals([double]$t) {
    if ($t -le 0) { return 0.0 }
    if ($t -le $rampS) { return $r0 * $t + ($target - $r0) * $t * $t / (2 * $rampS) }
    return ($r0 + $target) / 2 * $rampS + $target * ([Math]::Min($t, $rampS + $sustainS) - $rampS)
}

foreach ($r in $load) {
    $r | Add-Member -NotePropertyName startMs -NotePropertyValue ([long]$r.timeStamp)
    $r | Add-Member -NotePropertyName endMs -NotePropertyValue ([long]$r.timeStamp + [long]$r.elapsed)
    $r | Add-Member -NotePropertyName ok -NotePropertyValue ($r.success -eq 'true')
}
$ramp = @($load | Where-Object label -eq 'verifyPhoto_ramp')
$sustain = @($load | Where-Object label -eq 'verifyPhoto_sustain')
$okAll = @($load | Where-Object ok)
$failedAll = @($load | Where-Object { -not $_.ok })

# --- Windows ---------------------------------------------------------------------------------
$lastEnd = ($load | Measure-Object endMs -Maximum).Maximum
$windowCount = [int][Math]::Floor(($lastEnd - $start) / 1000 / $WindowSeconds) + 1
$sentIn = New-Object int[] $windowCount
$okIn = New-Object int[] $windowCount
$failedIn = New-Object int[] $windowCount
foreach ($r in $load) {
    $i = [int][Math]::Floor(($r.startMs - $start) / 1000 / $WindowSeconds)
    if ($i -ge 0 -and $i -lt $windowCount) { $sentIn[$i]++ }
    $i = [int][Math]::Floor(($r.endMs - $start) / 1000 / $WindowSeconds)
    if ($i -ge 0 -and $i -lt $windowCount) { if ($r.ok) { $okIn[$i]++ } else { $failedIn[$i]++ } }
}
$windows = for ($w = 0; $w -lt $windowCount; $w++) {
    $t0 = $w * $WindowSeconds; $t1 = $t0 + $WindowSeconds
    [pscustomobject]@{
        t0 = $t0; t1 = $t1
        phase = $(if ($t1 -le $rampS) { 'ramp' } elseif ($t0 -ge $rampS -and $t1 -le $rampS + $sustainS) { 'sustain' } elseif ($t0 -ge $rampS + $sustainS) { 'drain' } else { 'boundary' })
        plannedTps = ((Arrivals $t1) - (Arrivals $t0)) / $WindowSeconds
        sentTps = $sentIn[$w] / $WindowSeconds
        okTps = $okIn[$w] / $WindowSeconds
        failed = $failedIn[$w]
    }
}
$rampWindows = @($windows | Where-Object phase -eq 'ramp')
$sustainWindows = @($windows | Where-Object phase -eq 'sustain')
$expectedSustainWindows = [Math]::Floor($sustainS / $WindowSeconds)

# --- Phase figures ---------------------------------------------------------------------------
$sustainOk = @($sustain | Where-Object ok).Count
$sustainTps = if ($sustainS -gt 0) { $sustainOk / $sustainS } else { 0 }
$rampOk = @($ramp | Where-Object ok).Count
$lags = [double[]]@($load | ForEach-Object { [double]$_.LAG_MS } | Sort-Object)
$failClasses = $failedAll | Group-Object FAIL_CLASS | Sort-Object Count -Descending
$badVerdicts = @($failedAll | Where-Object { $_.FAIL_CLASS -in 'INVALID_VERDICT', 'VERDICT_MISMATCH', 'BODY_MISMATCH', 'INVALID_RESPONSE' }).Count
$timeouts = @($failedAll | Where-Object FAIL_CLASS -eq 'TIMEOUT').Count
$errorPct = 100 * $failedAll.Count / $load.Count
$verdicts = $okAll | Group-Object VERDICT | ForEach-Object { "$($_.Name) x$($_.Count)" }

# --- Resources -------------------------------------------------------------------------------
$res = $null
$resFile = Join-Path $RunDir 'resources.csv'
$resourceStop = $false
if (Test-Path $resFile) {
    $all = @(Import-Csv $resFile)
    $resourceStop = @($all | Where-Object { $_.event -match 'above|below|no longer running' }).Count -gt 0
    $during = @($all | Where-Object { [long]$_.epoch_ms -ge $start -and [long]$_.epoch_ms -le $lastEnd })
    $sus = @($during | Where-Object { [long]$_.epoch_ms -ge $start + $rampS * 1000 })
    function Col($set, $name, $op) {
        $v = @($set | Where-Object { $_.$name -ne '' } | ForEach-Object { [double]::Parse($_.$name, $inv) })
        if ($v.Count -eq 0) { return [double]::NaN }
        ($v | Measure-Object -Average -Maximum -Minimum).$op
    }
    $res = [ordered]@{
        samples = $during.Count
        api_cores_avg_sustain = Col $sus 'api_cores_used' 'Average'
        api_cores_max = Col $during 'api_cores_used' 'Maximum'
        api_private_mb_first = $(if ($during.Count) { [double]::Parse($during[0].api_private_mb, $inv) } else { [double]::NaN })
        api_private_mb_max = Col $during 'api_private_mb' 'Maximum'
        api_private_mb_last = $(if ($during.Count) { [double]::Parse($during[-1].api_private_mb, $inv) } else { [double]::NaN })
        api_handles_max = Col $during 'api_handles' 'Maximum'
        api_threads_max = Col $during 'api_threads' 'Maximum'
        system_cpu_avg = Col $during 'system_cpu_pct' 'Average'
        system_cpu_max = Col $during 'system_cpu_pct' 'Maximum'
        available_mb_min = Col $during 'available_memory_mb' 'Minimum'
        jmeter_cores_avg = Col $during 'jmeter_cores_used' 'Average'
        jmeter_cores_max = Col $during 'jmeter_cores_used' 'Maximum'
        jmeter_ws_mb_max = Col $during 'jmeter_working_set_mb' 'Maximum'
    }
}

# --- Acceptance ------------------------------------------------------------------------------
$firstSustain = $sustainWindows | Select-Object -First 1
$rampTracked = (@($rampWindows | Where-Object { $_.okTps -lt $_.plannedTps * $WindowMinPercent / 100 }).Count -eq 0) -and
               $firstSustain -and ($firstSustain.okTps -ge $target * $WindowMinPercent / 100)
$minSustainWindow = if ($sustainWindows.Count) { ($sustainWindows | Measure-Object okTps -Minimum).Minimum } else { 0 }
$sustainPass = $scheduleCompleted -and ($sustainWindows.Count -ge $expectedSustainWindows) -and
               ($sustainTps -ge $target * $SustainMinPercent / 100) -and ($minSustainWindow -ge $target * $WindowMinPercent / 100)
$errorPass = ($errorPct -le $MaxErrorPercent) -and $scheduleCompleted
$correctPass = $badVerdicts -eq 0
$lagP99 = Pct $lags 99
$stablePass = ($timeouts -eq 0) -and $scheduleCompleted -and ($lagP99 -le $MaxLagP99Ms) -and -not $resourceStop
$criteria = @(
    [pscustomobject]@{ n = 1; name = 'Reaches the target during ramp-up'; pass = [bool]$rampTracked; evidence = "every full ramp window >= $WindowMinPercent% of plan; first sustained window $(if ($firstSustain) { N $firstSustain.okTps '0.0' } else { 'n/a' }) TPS" }
    [pscustomobject]@{ n = 2; name = "Sustains >= $target successful verifications/s"; pass = [bool]$sustainPass; evidence = "$(N $sustainTps '0.00') TPS over $sustainS s ($sustainOk successful); lowest 10 s window $(N $minSustainWindow '0.0') TPS; $($sustainWindows.Count)/$expectedSustainWindows windows" }
    [pscustomobject]@{ n = 3; name = "Errors within limits (<= $MaxErrorPercent%, no safety stop)"; pass = [bool]$errorPass; evidence = "$($failedAll.Count) of $($load.Count) failed ($(N $errorPct '0.00')%)" }
    [pscustomobject]@{ n = 4; name = 'Verification results correct and consistent'; pass = [bool]$correctPass; evidence = "$badVerdicts invalid/inconsistent; verdicts: $($verdicts -join ', ')" }
    [pscustomobject]@{ n = 5; name = 'Responsive and stable'; pass = [bool]$stablePass; evidence = "$timeouts timeouts; p99 send lag $(N $lagP99 '0') ms; resource stop: $resourceStop; end: $stopReason" }
)
$allPass = @($criteria | Where-Object { -not $_.pass }).Count -eq 0

# --- Write summary.md / summary.json --------------------------------------------------------
$sb = New-Object Text.StringBuilder
function L([string]$s = '') { [void]$sb.AppendLine($s) }
function LatRow([string]$name, $st, [int]$ok) {
    if (-not $st) { return "| $name | 0 | 0 | | | | | | | |" }
    "| $name | $($st.count) | $ok | $(N $st.avg '0') | $(N $st.median '0') | $(N $st.p90 '0') | $(N $st.p95 '0') | $(N $st.p99 '0') | $(N $st.min '0') | $(N $st.max '0') |"
}
$runName = Split-Path -Leaf $RunDir
L "# verifyPhoto stress test: $runName"
L
L "**Result: $(if ($allPass) { 'PASSED' } else { 'NOT PASSED' })** - target $target TPS; ramp $r0 to $target TPS over $rampS s, then $sustainS s sustained."
L
L "| Item | Value |"
L "| --- | --- |"
L "| Target | $($info['target']) |"
L "| Image | $($info['image_path']) |"
L "| Baseline | verdict ``$($info['baseline_verdict'])``, $($info['baseline_bytes']) bytes, $(if ($baselineRow) { "$($baselineRow.elapsed) ms" }) |"
L "| Planned / sent requests | $planned / $($load.Count) |"
L "| How the run ended | $stopReason |"
L
L "## Acceptance criteria"
L
L "| # | Criterion | Result | Evidence |"
L "| --- | --- | --- | --- |"
foreach ($c in $criteria) { L "| $($c.n) | $($c.name) | $(if ($c.pass) { 'pass' } else { '**FAIL**' }) | $($c.evidence) |" }
L
L "## Throughput"
L
L "| Phase | Seconds | Requests | Successful | Successful TPS | Failed |"
L "| --- | --- | --- | --- | --- | --- |"
L "| Ramp-up | $rampS | $($ramp.Count) | $rampOk | $(if ($rampS) { N ($rampOk / $rampS) '0.00' } else { '-' }) | $($ramp.Count - $rampOk) |"
L "| Sustained | $sustainS | $($sustain.Count) | $sustainOk | $(N $sustainTps '0.00') | $($sustain.Count - $sustainOk) |"
L
L "Successful TPS per 10 s window against the plan (offered rate):"
L
L "| Window (s) | Phase | Planned TPS | Sent TPS | Successful TPS | Failed |"
L "| --- | --- | --- | --- | --- | --- |"
foreach ($w in $windows) { L "| $($w.t0)-$($w.t1) | $($w.phase) | $(N $w.plannedTps) | $(N $w.sentTps) | $(N $w.okTps) | $($w.failed) |" }
L
L "## Response time (ms)"
L
L "Over every load request, failed ones included. Percentiles are nearest-rank, so they can differ slightly from the JMeter dashboard."
L
L "| Requests | Count | Successful | Avg | Median | p90 | p95 | p99 | Min | Max |"
L "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |"
L (LatRow 'Ramp-up' (Stats $ramp) $rampOk)
L (LatRow 'Sustained' (Stats $sustain) $sustainOk)
L (LatRow 'All load' (Stats $load) $okAll.Count)
L
L "Send lag (how late requests left against the schedule): median $(N (Pct $lags 50) '0') ms, p99 $(N $lagP99 '0') ms, max $(N $(if ($lags.Count) { $lags[-1] } else { 0 }) '0') ms."
L
L "## Failures"
L
if ($failedAll.Count -eq 0) { L "None." } else {
    L "| Class | Count | First message |"
    L "| --- | --- | --- |"
    foreach ($g in $failClasses) { L "| $(if ($g.Name) { $g.Name } else { '(transport)' }) | $($g.Count) | $(($g.Group[0].failureMessage + ' ' + $g.Group[0].responseMessage).Trim() -replace '\|', '/') |" }
}
L
L "## Resources"
L
if ($res) {
    L "| Measure | Value |"
    L "| --- | --- |"
    L "| API CPU, sustained average / peak (cores of $([Environment]::ProcessorCount)) | $(N $res.api_cores_avg_sustain '0.00') / $(N $res.api_cores_max '0.00') |"
    L "| API private memory, first / peak / last sample (MB) | $(N $res.api_private_mb_first '0') / $(N $res.api_private_mb_max '0') / $(N $res.api_private_mb_last '0') |"
    L "| API handles / threads, peak | $(N $res.api_handles_max '0') / $(N $res.api_threads_max '0') |"
    L "| Machine CPU, average / peak (%) | $(N $res.system_cpu_avg '0') / $(N $res.system_cpu_max '0') |"
    L "| Machine available memory, lowest (MB) | $(N $res.available_mb_min '0') |"
    L "| JMeter CPU average / peak (cores), working set peak (MB) | $(N $res.jmeter_cores_avg '0.00') / $(N $res.jmeter_cores_max '0.00'), $(N $res.jmeter_ws_mb_max '0') |"
    L "| Resource limit crossed | $resourceStop |"
} else { L "No resources.csv in this run (the monitor did not run on this machine)." }

$summaryMd = Join-Path $RunDir 'summary.md'
[IO.File]::WriteAllText($summaryMd, $sb.ToString(), (New-Object Text.UTF8Encoding $false))
[ordered]@{
    run = $runName; passed = $allPass; target_tps = $target; ramp_start_tps = $r0; ramp_seconds = $rampS; sustain_seconds = $sustainS
    planned = $planned; sent = $load.Count; successful = $okAll.Count; failed = $failedAll.Count; error_pct = $errorPct
    ramp_successful_tps = $(if ($rampS) { $rampOk / $rampS } else { $null }); sustain_successful_tps = $sustainTps
    min_sustain_window_tps = $minSustainWindow; sustain_windows = $sustainWindows.Count
    latency_ramp = Stats $ramp; latency_sustain = Stats $sustain; latency_all = Stats $load
    lag_p99_ms = $lagP99; timeouts = $timeouts; bad_verdicts = $badVerdicts
    fail_classes = @($failClasses | ForEach-Object { [ordered]@{ class = $_.Name; count = $_.Count } })
    verdicts = $verdicts; stop_reason = $stopReason; resources = $res
    criteria = @($criteria | ForEach-Object { [ordered]@{ n = $_.n; name = $_.name; pass = $_.pass; evidence = $_.evidence } })
} | ConvertTo-Json -Depth 6 | Set-Content -Path (Join-Path $RunDir 'summary.json') -Encoding UTF8

Write-Host ''
Write-Host "=== $runName : $(if ($allPass) { 'PASSED' } else { 'NOT PASSED' }) ==="
foreach ($c in $criteria) { Write-Host ("  [{0}] {1}: {2}" -f $(if ($c.pass) { 'pass' } else { 'FAIL' }), $c.name, $c.evidence) }
$st = Stats $sustain
if ($st) { Write-Host ("  Sustained latency ms: avg {0}  median {1}  p90 {2}  p95 {3}  p99 {4}  max {5}" -f (N $st.avg '0'), (N $st.median '0'), (N $st.p90 '0'), (N $st.p95 '0'), (N $st.p99 '0'), (N $st.max '0')) }
Write-Host "  Summary: $summaryMd"
