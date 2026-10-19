<#
.SYNOPSIS
  Compares two folders written by capture-responses.ps1, response by response.

.DESCRIPTION
  A response matches when the HTTP status, the content type and the body bytes are all the
  same. checkLicense is compared without its checkedAt timestamp, which changes on every
  restart, and without licensePath, which differs when the builds run from different folders. For verifyPhoto it also prints the verdict (result.message) of each side.
  Exits with code 1 when anything differs.

.EXAMPLE
  .\compare-responses.ps1 -Expected .\captures\original -Actual .\captures\optimized
#>
param(
    [Parameter(Mandatory = $true)][string]$Expected,
    [Parameter(Mandatory = $true)][string]$Actual
)

$ErrorActionPreference = 'Stop'
$exp = Import-Csv (Join-Path $Expected 'index.csv')
$act = @{}; Import-Csv (Join-Path $Actual 'index.csv') | ForEach-Object { $act[$_.Name] = $_ }

function Read-Body([string]$dir, [string]$name) {
    $bytes = [IO.File]::ReadAllBytes((Join-Path $dir "$name.body"))
    if ($name -eq 'checkLicense') {
        $text = [Text.Encoding]::UTF8.GetString($bytes) -replace '"checkedAt":"[^"]*"', '"checkedAt":"*"' -replace '"licensePath":"[^"]*"', '"licensePath":"*"'
        return [Text.Encoding]::UTF8.GetBytes($text)
    }
    return $bytes
}

function Get-Verdict([byte[]]$body) {
    $head = [Text.Encoding]::UTF8.GetString($body, 0, [Math]::Min(400, $body.Length))
    if ($head -match '"message":"((?:[^"\\]|\\.)*)"') { return $matches[1] }
    if ($head -match '"result":"((?:[^"\\]|\\.)*)"') { return $matches[1] }
    return $head.Substring(0, [Math]::Min(80, $head.Length))
}

$failures = 0
$rows = foreach ($e in $exp) {
    $a = $act[$e.Name]
    if (-not $a) { $failures++; [pscustomobject]@{ Name = $e.Name; Match = 'MISSING'; Status = $e.Status; Bytes = $e.Bytes; Verdict = '' }; continue }
    $eb = Read-Body $Expected $e.Name
    $ab = Read-Body $Actual $e.Name
    $same = ($e.Status -eq $a.Status) -and ($e.ContentType -eq $a.ContentType) -and ($eb.Length -eq $ab.Length)
    if ($same) {
        for ($i = 0; $i -lt $eb.Length; $i++) { if ($eb[$i] -ne $ab[$i]) { $same = $false; break } }
    }
    if (-not $same) { $failures++ }
    $ev = Get-Verdict $eb; $av = Get-Verdict $ab
    [pscustomobject]@{
        Name    = $e.Name
        Match   = $(if ($same) { 'identical' } else { 'DIFFERENT' })
        Status  = $(if ($e.Status -eq $a.Status) { $e.Status } else { "$($e.Status) -> $($a.Status)" })
        Bytes   = $(if ($eb.Length -eq $ab.Length) { $eb.Length } else { "$($eb.Length) -> $($ab.Length)" })
        Verdict = $(if ($ev -eq $av) { $ev } else { "$ev -> $av" })
    }
}
$rows | Format-Table -AutoSize | Out-String -Width 250 | Write-Host
if ($failures -eq 0) { Write-Host "All $($exp.Count) responses are identical." -ForegroundColor Green; exit 0 }
Write-Host "$failures of $($exp.Count) responses differ." -ForegroundColor Red
exit 1
