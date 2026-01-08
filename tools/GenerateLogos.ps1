$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$icoPath = Join-Path $PSScriptRoot '..\LastShot\Assets\256.ico'
$destRoot = Join-Path $PSScriptRoot '..\LastShot.Package\Images'
$targets = @(
    @{Name='Square44x44Logo.png'; Width=44; Height=44},
    @{Name='Square150x150Logo.png'; Width=150; Height=150},
    @{Name='Square310x310Logo.png'; Width=310; Height=310},
    @{Name='Wide310x150Logo.png'; Width=310; Height=150},
    @{Name='StoreLogo.png'; Width=50; Height=50}
)
if (!(Test-Path $destRoot)) {
    New-Item -ItemType Directory -Path $destRoot | Out-Null
}
foreach ($t in $targets) {
    $bitmap = New-Object System.Drawing.Bitmap($t.Width, $t.Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $icon = New-Object System.Drawing.Icon($icoPath, $t.Width, $t.Height)
    $graphics.DrawIcon($icon, 0, 0)
    $icon.Dispose()
    $graphics.Dispose()
    $destination = Join-Path $destRoot $t.Name
    $bitmap.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}
