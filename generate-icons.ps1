Add-Type -AssemblyName System.Drawing

function Create-NtoNIcon([int]$size, [string]$path) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # Scale factor
    $s = $size / 128.0

    # Colors
    $blueBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 41, 98, 255))
    $orangeBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 152, 0))
    $linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 100, 100, 100), (2.5 * $s))
    $whiteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)

    # Left column nodes (blue) at x=25
    $leftX = 25 * $s
    $nodeSize = 20 * $s
    $leftYs = @((24 * $s), (52 * $s), (80 * $s))

    # Right column nodes (orange) at x=83
    $rightX = 83 * $s
    $rightYs = @((24 * $s), (52 * $s), (80 * $s))

    # Draw connection lines (N:N - each left connects to multiple right)
    foreach ($ly in $leftYs) {
        foreach ($ry in $rightYs) {
            $g.DrawLine($linePen, ($leftX + $nodeSize/2), ($ly + $nodeSize/2), ($rightX + $nodeSize/2), ($ry + $nodeSize/2))
        }
    }

    # Draw left nodes (blue circles with white border)
    foreach ($y in $leftYs) {
        $g.FillEllipse($whiteBrush, ($leftX - 1*$s), ($y - 1*$s), ($nodeSize + 2*$s), ($nodeSize + 2*$s))
        $g.FillEllipse($blueBrush, $leftX, $y, $nodeSize, $nodeSize)
    }

    # Draw right nodes (orange circles with white border)
    foreach ($y in $rightYs) {
        $g.FillEllipse($whiteBrush, ($rightX - 1*$s), ($y - 1*$s), ($nodeSize + 2*$s), ($nodeSize + 2*$s))
        $g.FillEllipse($orangeBrush, $rightX, $y, $nodeSize, $nodeSize)
    }

    # Lightning bolt in center (speed indicator)
    $cx = 54 * $s
    $cy = 44 * $s
    $boltPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 215, 0), (3 * $s))

    $p1 = New-Object System.Drawing.PointF(($cx + 6*$s), ($cy - 12*$s))
    $p2 = New-Object System.Drawing.PointF(($cx - 2*$s), ($cy + 2*$s))
    $p3 = New-Object System.Drawing.PointF(($cx + 4*$s), ($cy + 2*$s))
    $p4 = New-Object System.Drawing.PointF(($cx - 4*$s), ($cy + 16*$s))

    $g.DrawLine($boltPen, $p1, $p2)
    $g.DrawLine($boltPen, $p2, $p3)
    $g.DrawLine($boltPen, $p3, $p4)

    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Created $path ($size x $size)"
}

Create-NtoNIcon 128 "Resources\icon-128.png"
Create-NtoNIcon 80 "Resources\icon-80.png"
Create-NtoNIcon 32 "Resources\icon-32.png"
