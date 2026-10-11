[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Add-Type -AssemblyName System.Drawing
$repository = Split-Path $PSScriptRoot -Parent
$assets = Join-Path $repository 'packaging/Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null

function Write-BridgeAsset {
    param([string] $Name, [int] $Width, [int] $Height)
    $bitmap = [Drawing.Bitmap]::new($Width, $Height)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $background = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, 19, 32, 55))
    $key = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, 239, 245, 255))
    $accent = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255, 68, 202, 177))
    $pen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(255, 68, 202, 177), [single]([Math]::Max(1, [Math]::Min($Width, $Height) * 0.055)))
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.FillRectangle($background, 0, 0, $Width, $Height)
        $size = [single][Math]::Min($Width, $Height)
        $left = [single](($Width - $size) / 2)
        $top = [single](($Height - $size) / 2)
        # Original paired keycaps joined by a bridge; no third-party marks.
        $graphics.FillRectangle($key, ($left + $size * .15), ($top + $size * .29), ($size * .28), ($size * .42))
        $graphics.FillRectangle($key, ($left + $size * .57), ($top + $size * .29), ($size * .28), ($size * .42))
        $graphics.DrawLine($pen, ($left + $size * .30), ($top + $size * .5), ($left + $size * .70), ($top + $size * .5))
        $graphics.FillPolygon($accent, [Drawing.PointF[]]@(
            [Drawing.PointF]::new(($left + $size * .65), ($top + $size * .38)),
            [Drawing.PointF]::new(($left + $size * .77), ($top + $size * .50)),
            [Drawing.PointF]::new(($left + $size * .65), ($top + $size * .62))
        ))
        $bitmap.Save((Join-Path $assets $Name), [Drawing.Imaging.ImageFormat]::Png)
    } finally {
        $pen.Dispose(); $accent.Dispose(); $key.Dispose(); $background.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
    }
}

$definitions = @(
    @{ Name = 'StoreLogo'; Width = 50; Height = 50 },
    @{ Name = 'Square44x44Logo'; Width = 44; Height = 44 },
    @{ Name = 'Square150x150Logo'; Width = 150; Height = 150 },
    @{ Name = 'Wide310x150Logo'; Width = 310; Height = 150 }
)
foreach ($definition in $definitions) {
    Write-BridgeAsset "$($definition.Name).png" $definition.Width $definition.Height
    foreach ($scale in @(100, 125, 150, 200, 400)) {
        Write-BridgeAsset "$($definition.Name).scale-$scale.png" ([int]($definition.Width * $scale / 100)) ([int]($definition.Height * $scale / 100))
    }
}
foreach ($size in @(16, 24, 32, 48, 64, 256)) {
    Write-BridgeAsset "Square44x44Logo.targetsize-$size.png" $size $size
    Write-BridgeAsset "Square44x44Logo.targetsize-${size}_altform-unplated.png" $size $size
}
Write-Host "Generated original package assets in $assets."
