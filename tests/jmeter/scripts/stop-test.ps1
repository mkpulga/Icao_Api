<#
.SYNOPSIS
  Stops a running verifyPhoto test gracefully: nothing new is sent, requests in flight finish.

.DESCRIPTION
  Writes the STOP file of the newest run folder (or -RunDir); the plan checks for it every
  2 seconds. With -Now it also sends JMeter's own Shutdown command (UDP port 4445 on this
  machine), which works even when no request is completing.

.EXAMPLE
  .\scripts\stop-test.ps1 -Reason "API logs show errors"
#>
param(
    [string]$RunDir = '',
    [string]$Reason = 'stopped by the operator',
    [switch]$Now,
    [string]$JMeterHome = $(if ($env:JMETER_HOME) { $env:JMETER_HOME } else { 'C:\tools\apache-jmeter-5.6.3' })
)
$ErrorActionPreference = 'Stop'
if (-not $RunDir) {
    $results = Join-Path (Split-Path -Parent $PSScriptRoot) 'results'
    $RunDir = Get-ChildItem $results -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $RunDir) { throw 'No run folder found; pass -RunDir.' }
"OPERATOR: $Reason" | Set-Content -Path (Join-Path $RunDir 'STOP') -Encoding ASCII
Write-Host "Wrote $(Join-Path $RunDir 'STOP'); the test stops within a few seconds of the next completed request."
if ($Now) {
    & (Join-Path $JMeterHome 'bin\shutdown.cmd')
    Write-Host 'Also sent JMeter Shutdown.'
}
