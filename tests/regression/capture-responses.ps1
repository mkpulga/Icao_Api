<#
.SYNOPSIS
  Sends a fixed set of requests to a running obsIcaoApi and saves every response, so two
  builds can be compared byte for byte with compare-responses.ps1.

.DESCRIPTION
  The verifyPhoto inputs are generated from one face photo you supply (they are biometric
  data, so none are stored in the repository). Each one reaches a different path of the
  endpoint:
    face.jpg            the photo as given
    face_flip.jpg       mirrored
    face_rot<N>.jpg     rotated by N degrees (exercises the roll check)
    face_small.png      half size, PNG, 24 bpp
    face_argb.png       32 bpp with alpha
    face_gray8.png      8 bpp indexed (a Graphics object cannot be created on it)
    blank.jpg           no face at all
    tiny.jpg            16 x 16 pixels
    not-an-image.jpg    text bytes with a .jpg name (HTTP 400)
    (empty upload)      a zero-byte file (HTTP 400 "No File Uploaded")
  It also captures Hello, checkLicense, verifyFinger and verifyFinger_v1 (with -FingerImage),
  to show that the other endpoints did not change.

  Requests are sent one at a time; this is a correctness check, not a load test.

.EXAMPLE
  .\capture-responses.ps1 -BaseUrl http://localhost:7006 -FaceImage C:\data\face.jpg -FingerImage C:\data\finger.bmp -OutDir .\captures\original
#>
param(
    [string]$BaseUrl = 'http://localhost:7006',
    [Parameter(Mandatory = $true)][string]$FaceImage,
    [string]$FingerImage = '',
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$FieldName = 'file',
    [int]$RepeatOriginal = 3
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$BaseUrl = $BaseUrl.TrimEnd('/')
$inputs = Join-Path $OutDir 'inputs'
New-Item -ItemType Directory -Force -Path $inputs | Out-Null

function Save-Jpeg([System.Drawing.Image]$image, [string]$path) {
    $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
    $params = New-Object System.Drawing.Imaging.EncoderParameters 1
    $params.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality, [long]95)
    $image.Save($path, $codec, $params)
}

function New-Canvas([int]$w, [int]$h, [System.Drawing.Imaging.PixelFormat]$format) {
    New-Object System.Drawing.Bitmap $w, $h, $format
}

# --- Generate the inputs (deterministic: same photo in, same files out) -------------------
Copy-Item -LiteralPath $FaceImage -Destination (Join-Path $inputs 'face.jpg') -Force
$src = [System.Drawing.Image]::FromFile((Resolve-Path $FaceImage).Path)
try {
    $flip = New-Object System.Drawing.Bitmap $src
    $flip.RotateFlip([System.Drawing.RotateFlipType]::RotateNoneFlipX)
    Save-Jpeg $flip (Join-Path $inputs 'face_flip.jpg'); $flip.Dispose()

    foreach ($deg in -6, -3, 3, 6) {
        $rot = New-Canvas $src.Width $src.Height ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $g = [System.Drawing.Graphics]::FromImage($rot)
        $g.Clear([System.Drawing.Color]::White)
        $g.TranslateTransform($src.Width / 2, $src.Height / 2); $g.RotateTransform($deg); $g.TranslateTransform(-$src.Width / 2, -$src.Height / 2)
        $g.DrawImage($src, 0, 0, $src.Width, $src.Height); $g.Dispose()
        $name = 'face_rot{0}.jpg' -f ($(if ($deg -lt 0) { "m$(-$deg)" } else { "p$deg" }))
        Save-Jpeg $rot (Join-Path $inputs $name); $rot.Dispose()
    }

    $small = New-Canvas ([int]($src.Width / 2)) ([int]($src.Height / 2)) ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($small); $g.DrawImage($src, 0, 0, $small.Width, $small.Height); $g.Dispose()
    $small.Save((Join-Path $inputs 'face_small.png'), [System.Drawing.Imaging.ImageFormat]::Png); $small.Dispose()

    $argb = New-Canvas $src.Width $src.Height ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($argb); $g.DrawImage($src, 0, 0, $src.Width, $src.Height); $g.Dispose()
    $argb.Save((Join-Path $inputs 'face_argb.png'), [System.Drawing.Imaging.ImageFormat]::Png); $argb.Dispose()

    # 8 bpp indexed grayscale, written pixel by pixel through LockBits.
    $w = [int]($src.Width / 2); $h = [int]($src.Height / 2)
    $rgb = New-Canvas $w $h ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $g = [System.Drawing.Graphics]::FromImage($rgb); $g.DrawImage($src, 0, 0, $w, $h); $g.Dispose()
    $gray = New-Canvas $w $h ([System.Drawing.Imaging.PixelFormat]::Format8bppIndexed)
    $pal = $gray.Palette; for ($i = 0; $i -lt 256; $i++) { $pal.Entries[$i] = [System.Drawing.Color]::FromArgb($i, $i, $i) }; $gray.Palette = $pal
    $rect = New-Object System.Drawing.Rectangle 0, 0, $w, $h
    $sd = $rgb.LockBits($rect, 'ReadOnly', [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $dd = $gray.LockBits($rect, 'WriteOnly', [System.Drawing.Imaging.PixelFormat]::Format8bppIndexed)
    $sb = New-Object byte[] ($sd.Stride * $h); [Runtime.InteropServices.Marshal]::Copy($sd.Scan0, $sb, 0, $sb.Length)
    $db = New-Object byte[] ($dd.Stride * $h)
    for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) { $o = $y * $sd.Stride + 3 * $x; $db[$y * $dd.Stride + $x] = [byte](($sb[$o] * 29 + $sb[$o + 1] * 150 + $sb[$o + 2] * 77) -shr 8) } }
    [Runtime.InteropServices.Marshal]::Copy($db, 0, $dd.Scan0, $db.Length)
    $rgb.UnlockBits($sd); $gray.UnlockBits($dd)
    $gray.Save((Join-Path $inputs 'face_gray8.png'), [System.Drawing.Imaging.ImageFormat]::Png); $gray.Dispose(); $rgb.Dispose()
}
finally { $src.Dispose() }

$blank = New-Canvas 640 480 ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($blank); $g.Clear([System.Drawing.Color]::FromArgb(180, 190, 200)); $g.Dispose()
Save-Jpeg $blank (Join-Path $inputs 'blank.jpg'); $blank.Dispose()
$tiny = New-Canvas 16 16 ([System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
Save-Jpeg $tiny (Join-Path $inputs 'tiny.jpg'); $tiny.Dispose()
[IO.File]::WriteAllText((Join-Path $inputs 'not-an-image.jpg'), 'this is not an image')
[IO.File]::WriteAllBytes((Join-Path $inputs 'empty.jpg'), [byte[]]@())

# --- Send the requests -------------------------------------------------------------------
$results = New-Object System.Collections.ArrayList
function Invoke-Capture([string]$name, [string[]]$curlArgs) {
    $body = Join-Path $OutDir "$name.body"
    $meta = & curl.exe -s -o $body -w '%{http_code}|%{content_type}|%{time_total}' --max-time 120 @curlArgs
    $parts = "$meta".Split('|')
    $item = [pscustomobject]@{ Name = $name; Status = $parts[0]; ContentType = $parts[1]; Seconds = $parts[2]; Bytes = (Get-Item $body).Length }
    [void]$results.Add($item)
    Write-Host ("{0,-28} HTTP {1}  {2,9:N0} bytes  {3}s" -f $name, $item.Status, $item.Bytes, $item.Seconds)
}

Invoke-Capture 'Hello' @("$BaseUrl/Hello")
Invoke-Capture 'checkLicense' @("$BaseUrl/checkLicense")

$faceInputs = Get-ChildItem $inputs -File | Where-Object Name -ne 'empty.jpg' | Sort-Object Name
foreach ($f in $faceInputs) {
    $mime = switch ($f.Extension) { '.png' { 'image/png' } default { 'image/jpeg' } }
    Invoke-Capture "verifyPhoto_$($f.BaseName)" @('-F', "$FieldName=@$($f.FullName);type=$mime", "$BaseUrl/verifyPhoto")
}
for ($i = 2; $i -le $RepeatOriginal; $i++) {
    Invoke-Capture "verifyPhoto_face_repeat$i" @('-F', "$FieldName=@$(Join-Path $inputs 'face.jpg');type=image/jpeg", "$BaseUrl/verifyPhoto")
}
Invoke-Capture 'verifyPhoto_empty-file' @('-F', "$FieldName=@$(Join-Path $inputs 'empty.jpg');type=image/jpeg", "$BaseUrl/verifyPhoto")
Invoke-Capture 'verifyPhoto_no-file' @('-F', 'note=no file here', "$BaseUrl/verifyPhoto")

if ($FingerImage) {
    foreach ($ep in 'verifyFinger', 'verifyFinger_v1') {
        Invoke-Capture "${ep}_finger" @('-F', "$FieldName=@$((Resolve-Path $FingerImage).Path);type=image/bmp", "$BaseUrl/$ep")
    }
    Invoke-Capture 'verifyFinger_v1_face' @('-F', "$FieldName=@$(Join-Path $inputs 'face.jpg');type=image/jpeg", "$BaseUrl/verifyFinger_v1")
}

$results | Export-Csv -NoTypeInformation -Path (Join-Path $OutDir 'index.csv')
Write-Host "Saved $($results.Count) responses to $OutDir"
