param([switch]$Preview)

# Builds ACNoxxer.exe with the .NET Framework compiler that ships with Windows (no SDK needed).
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$fx   = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$wpf  = "$fx\WPF"
$outName = if ($Preview) { 'ACNoxxer-preview.exe' } else { 'ACNoxxer.exe' }
$out  = Join-Path $root $outName
$ico  = Join-Path $root 'noxxer.ico'

# ---- icon (solid black tile, white frame, white N) -------------------------------------------
Add-Type -AssemblyName System.Drawing
function New-Frame([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Black)
    $pw = [Math]::Max(2, [int]($s / 12))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $pw
    $g.DrawRectangle($pen, $pw / 2, $pw / 2, $s - $pw, $s - $pw)
    $font = New-Object System.Drawing.Font 'Segoe UI Black', ([single]($s * 0.52)), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = 'Center'; $sf.LineAlignment = 'Center'
    $g.DrawString('N', $font, [System.Drawing.Brushes]::White, (New-Object System.Drawing.RectangleF 0, 0, $s, ($s * 1.04)), $sf)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}
$sizes = 16, 32, 48, 256
$frames = @(); foreach ($s in $sizes) { $frames += , (New-Frame $s) }
$fs = [System.IO.File]::Create($ico)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$d); $bw.Write([byte]$d); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$frames[$i].Length); $bw.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($f in $frames) { $bw.Write($f) }
$bw.Close(); $fs.Close()

# ---- compile ---------------------------------------------------------------------------------
$refs = @(
    "$wpf\PresentationFramework.dll", "$wpf\PresentationCore.dll", "$wpf\WindowsBase.dll", "$fx\System.Xaml.dll",
    "$fx\System.dll", "$fx\System.Core.dll", "$fx\System.Management.dll",
    "$fx\System.Windows.Forms.dll", "$fx\System.Drawing.dll"
) | ForEach-Object { "/r:$_" }

$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$manifestArgs = @()
if (-not $Preview) { $manifestArgs = @("/win32manifest:$(Join-Path $root 'app.manifest')") }
& "$fx\csc.exe" /nologo /target:winexe /platform:x64 /optimize+ /warn:3 `
    "/out:$out" "/win32icon:$ico" @manifestArgs @refs @src
if ($LASTEXITCODE -ne 0) { throw "Compilation failed" }
Write-Host "Built $out" -ForegroundColor Green

