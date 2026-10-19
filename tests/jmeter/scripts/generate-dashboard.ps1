<#
.SYNOPSIS
  Rebuilds the JMeter HTML dashboard of a finished run from its results.jtl.

.EXAMPLE
  .\scripts\generate-dashboard.ps1 -RunDir results\20261019-170000-optimized
#>
param(
    [Parameter(Mandatory = $true)][string]$RunDir,
    [string]$JMeterHome = $(if ($env:JMETER_HOME) { $env:JMETER_HOME } else { 'C:\tools\apache-jmeter-5.6.3' })
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $RunDir 'dashboard'
if (Test-Path $out) { Remove-Item -Recurse -Force $out }
& (Join-Path $JMeterHome 'bin\jmeter.bat') -g (Join-Path $RunDir 'results.jtl') -o $out -q (Join-Path $root 'config\results.properties') -j (Join-Path $RunDir 'dashboard.log')
Write-Host "Dashboard: $(Join-Path $out 'index.html')"
