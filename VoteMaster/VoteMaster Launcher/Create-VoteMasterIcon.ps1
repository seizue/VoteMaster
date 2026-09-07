Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase, System.Drawing

function New-VoteMasterIcon {
    param(
        [string]$OutputPath = "$PSScriptRoot\VoteMaster.ico"
    )

    $sizes = @(256, 128, 64, 48, 32, 16)
    $pngStreams = @()

    # Geometry from bi-check2-square
    $path1Data = "M3 14.5A1.5 1.5 0 0 1 1.5 13V3A1.5 1.5 0 0 1 3 1.5h8a.5.5 0 0 1 0 1H3a.5.5 0 0 0-.5.5v10a.5.5 0 0 0 .5.5h10a.5.5 0 0 0 .5-.5V8a.5.5 0 0 1 1 0v5a1.5 1.5 0 0 1-1.5 1.5z"
    $path2Data = "m8.354 10.354 7-7a.5.5 0 0 0-.708-.708L8 9.293 5.354 6.646a.5.5 0 1 0-.708.708l3 3a.5.5 0 0 0 .708 0"

    $geom1 = [System.Windows.Media.Geometry]::Parse($path1Data)
    $geom2 = [System.Windows.Media.Geometry]::Parse($path2Data)
    $geomGroup = New-Object System.Windows.Media.GeometryGroup
    $geomGroup.Children.Add($geom1)
    $geomGroup.Children.Add($geom2)

    foreach ($size in $sizes) {
        $scale = $size / 256.0
        $visual = New-Object System.Windows.Media.DrawingVisual
        $dc = $visual.RenderOpen()

        # Scale transform
        $dc.PushTransform((New-Object System.Windows.Media.ScaleTransform($scale, $scale)))

        # Background gradient (teal #0d9488 to #14b8a6)
        $startColor = [System.Windows.Media.Color]::FromRgb(13, 148, 136)
        $endColor = [System.Windows.Media.Color]::FromRgb(20, 184, 166)
        $brush = New-Object System.Windows.Media.LinearGradientBrush(
            $startColor, $endColor,
            (New-Object System.Windows.Point(0, 0)),
            (New-Object System.Windows.Point(1, 1))
        )

        # Rounded rectangle background
        $rect = New-Object System.Windows.Rect(16, 16, 224, 224)
        $dc.DrawRoundedRectangle($brush, $null, $rect, 52, 52)

        # White inner stroke / highlight
        $rimColor = [System.Windows.Media.Color]::FromArgb(60, 255, 255, 255)
        $pen = New-Object System.Windows.Media.Pen((New-Object System.Windows.Media.SolidColorBrush($rimColor)), 2)
        $dc.DrawRoundedRectangle($null, $pen, (New-Object System.Windows.Rect(17, 17, 222, 222)), 51, 51)

        # Draw icon
        $iconBrush = New-Object System.Windows.Media.SolidColorBrush([System.Windows.Media.Colors]::White)
        $iconTransform = New-Object System.Windows.Media.TransformGroup
        $iconTransform.Children.Add((New-Object System.Windows.Media.ScaleTransform(8.5, 8.5)))
        $iconTransform.Children.Add((New-Object System.Windows.Media.TranslateTransform(60, 60)))
        $dc.PushTransform($iconTransform)
        $dc.DrawGeometry($iconBrush, $null, $geomGroup)
        $dc.Pop() # icon transform

        $dc.Pop() # scale transform
        $dc.Close()

        # Render to bitmap
        $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
        $rtb.Render($visual)

        # Save to PNG stream
        $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
        $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))
        $ms = New-Object System.IO.MemoryStream
        $encoder.Save($ms)
        $pngStreams += $ms
    }

    # Write ICO file
    $fs = [System.IO.File]::Create($OutputPath)
    $writer = New-Object System.IO.BinaryWriter($fs)

    # ICONDIR
    $writer.Write([uint16]0) # Reserved
    $writer.Write([uint16]1) # Type 1 = ICO
    $writer.Write([uint16]$sizes.Count) # Image count

    # Calculate offset
    $headerSize = 6 + ($sizes.Count * 16)
    $offset = $headerSize

    # ICONDIRENTRY for each
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $s = $sizes[$i]
        $w = if ($s -eq 256) { 0 } else { $s }
        $h = if ($s -eq 256) { 0 } else { $s }
        $bytes = $pngStreams[$i].ToArray()

        $writer.Write([byte]$w)            # Width
        $writer.Write([byte]$h)            # Height
        $writer.Write([byte]0)             # ColorCount
        $writer.Write([byte]0)             # Reserved
        $writer.Write([uint16]1)           # Planes
        $writer.Write([uint16]32)          # BitCount
        $writer.Write([uint32]$bytes.Length) # BytesInRes
        $writer.Write([uint32]$offset)     # ImageOffset

        $offset += $bytes.Length
    }

    # Image Data
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $bytes = $pngStreams[$i].ToArray()
        $writer.Write($bytes)
        $pngStreams[$i].Dispose()
    }

    $writer.Flush()
    $writer.Close()
    $fs.Close()

    Write-Host "Created $OutputPath successfully."
}

New-VoteMasterIcon
