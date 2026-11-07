# Génère ravenne.ico (ICO moderne encapsulant un PNG 256x256) pour Ravenne's Tools Menu.
param(
    [string]$OutPath = "$PSScriptRoot\..\src\RavenneToolsMenu\ravenne.ico"
)

Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$accent = [System.Drawing.Color]::FromArgb(124, 77, 255)
$bg     = [System.Drawing.Color]::FromArgb(24, 24, 34)
$fg     = [System.Drawing.Color]::FromArgb(238, 238, 248)

# fond arrondi
$r = 58
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc(10, 10, $r, $r, 180, 90)
$path.AddArc($size - 10 - $r, 10, $r, $r, 270, 90)
$path.AddArc($size - 10 - $r, $size - 10 - $r, $r, $r, 0, 90)
$path.AddArc(10, $size - 10 - $r, $r, $r, 90, 90)
$path.CloseFigure()
$bgBrush = New-Object System.Drawing.SolidBrush($bg)
$g.FillPath($bgBrush, $path)

# anneau accent
$ringPen = New-Object System.Drawing.Pen($accent, 16)
$g.DrawEllipse($ringPen, 58, 58, 140, 140)

# six plots sur l'anneau
$dotBrush = New-Object System.Drawing.SolidBrush($fg)
$cx = 128; $cy = 128; $rr = 70; $dot = 13
for ($i = 0; $i -lt 6; $i++) {
    $a = -[Math]::PI / 2 + $i * [Math]::PI / 3
    $dx = $cx + $rr * [Math]::Cos($a) - $dot / 2
    $dy = $cy + $rr * [Math]::Sin($a) - $dot / 2
    $g.FillEllipse($dotBrush, [float]$dx, [float]$dy, $dot, $dot)
}
# point central accent
$centerBrush = New-Object System.Drawing.SolidBrush($accent)
$g.FillEllipse($centerBrush, 118, 118, 20, 20)

$g.Dispose()

# PNG -> ICO (une seule image 256)
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$png = $ms.ToArray()
$bmp.Dispose()

$outDir = Split-Path -Parent $OutPath
if (!(Test-Path $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
$fs = [System.IO.File]::Create($OutPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)   # réservé
$bw.Write([uint16]1)   # type icône
$bw.Write([uint16]1)   # nombre d'images
$bw.Write([byte]0)     # largeur 256
$bw.Write([byte]0)     # hauteur 256
$bw.Write([byte]0)     # palette
$bw.Write([byte]0)     # réservé
$bw.Write([uint16]1)   # plans
$bw.Write([uint16]32)  # bpp
$bw.Write([uint32]$png.Length)
$bw.Write([uint32]22)  # offset données
$bw.Write($png)
$bw.Close()

Write-Host "ICO écrit : $OutPath"
