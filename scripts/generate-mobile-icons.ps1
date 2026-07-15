$source = Join-Path $PSScriptRoot "..\icone.png"
Add-Type -AssemblyName System.Drawing

function Save-Resize {
    param([int]$Size, [string]$Path)

    $dir = Split-Path $Path -Parent
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }

    $src = [System.Drawing.Image]::FromFile($source)
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = 'HighQualityBicubic'
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($src, 0, 0, $Size, $Size)
    $g.Dispose()
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $src.Dispose()
}

$mobileRoot = Join-Path $PSScriptRoot "..\mobile\school_management_mobile"
$androidRes = Join-Path $mobileRoot "android\app\src\main\res"

Save-Resize -Size 48 -Path (Join-Path $androidRes "mipmap-mdpi\ic_launcher.png")
Save-Resize -Size 72 -Path (Join-Path $androidRes "mipmap-hdpi\ic_launcher.png")
Save-Resize -Size 96 -Path (Join-Path $androidRes "mipmap-xhdpi\ic_launcher.png")
Save-Resize -Size 144 -Path (Join-Path $androidRes "mipmap-xxhdpi\ic_launcher.png")
Save-Resize -Size 192 -Path (Join-Path $androidRes "mipmap-xxxhdpi\ic_launcher.png")

$webIcons = Join-Path $mobileRoot "web\icons"
Save-Resize -Size 192 -Path (Join-Path $webIcons "Icon-192.png")
Save-Resize -Size 512 -Path (Join-Path $webIcons "Icon-512.png")
Save-Resize -Size 192 -Path (Join-Path $webIcons "Icon-maskable-192.png")
Save-Resize -Size 512 -Path (Join-Path $webIcons "Icon-maskable-512.png")
Save-Resize -Size 32 -Path (Join-Path $mobileRoot "web\favicon.png")

$winRes = Join-Path $mobileRoot "windows\runner\resources"
if (-not (Test-Path $winRes)) {
    New-Item -ItemType Directory -Force -Path $winRes | Out-Null
}

$src = [System.Drawing.Image]::FromFile($source)
$bmp = New-Object System.Drawing.Bitmap($src, 256, 256)
$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$fs = [System.IO.File]::Create((Join-Path $winRes "app_icon.ico"))
$icon.Save($fs)
$fs.Close()
$src.Dispose()
$bmp.Dispose()

Write-Output "Mobile icons generated from icone.png"
