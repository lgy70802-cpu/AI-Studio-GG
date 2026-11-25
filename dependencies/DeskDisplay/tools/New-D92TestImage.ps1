param(
    [Parameter(Mandatory)][ValidateRange(1,8192)][int]$Width,
    [Parameter(Mandatory)][ValidateRange(1,8192)][int]$Height,
    [Parameter(Mandatory)][string]$OutputPath,
    [ValidateSet(0,90,180,270)][int]$PixelRotation = 0,
    [string]$Title = 'DIRECT USB TEST'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$visual = [System.Windows.Media.DrawingVisual]::new()
$drawing = $visual.RenderOpen()
try {
    $background = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(12,24,40))
    $accent = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(44,230,194))
    $drawing.DrawRectangle($background, $null, [System.Windows.Rect]::new(0,0,$Width,$Height))
    $drawing.DrawRectangle($accent, $null, [System.Windows.Rect]::new(0,0,$Width*0.018,$Height))
    $scale = [Math]::Min($Width/1600.0,$Height/400.0)
    $typeface = [System.Windows.Media.Typeface]::new('Segoe UI')
    $labels = @(
        @{Text=$Title; Size=70; Y=65; Brush=$accent},
        @{Text='Craft stopped | C# -> HID -> D92'; Size=36; Y=174; Brush=[System.Windows.Media.Brushes]::White},
        @{Text=('{0} x {1}  |  {2}' -f $Width,$Height,(Get-Date -Format 'HH:mm:ss')); Size=28; Y=265; Brush=[System.Windows.Media.Brushes]::LightGray}
    )
    foreach ($label in $labels) {
        $text = [System.Windows.Media.FormattedText]::new($label.Text,[Globalization.CultureInfo]::InvariantCulture,[System.Windows.FlowDirection]::LeftToRight,$typeface,$label.Size*$scale,$label.Brush,1.0)
        $drawing.DrawText($text,[System.Windows.Point]::new(70*$scale,$label.Y*$scale))
    }
} finally { $drawing.Close() }
$bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($Width,$Height,96,96,[System.Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($visual)
$encoder = [System.Windows.Media.Imaging.JpegBitmapEncoder]::new()
$encoder.QualityLevel = 90
$outputBitmap = $bitmap
if ($PixelRotation -ne 0) {
    $outputBitmap = [System.Windows.Media.Imaging.TransformedBitmap]::new($bitmap,[System.Windows.Media.RotateTransform]::new($PixelRotation))
}
$encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($outputBitmap))
$stream = [IO.File]::Create([IO.Path]::GetFullPath($OutputPath))
try { $encoder.Save($stream) } finally { $stream.Dispose() }
Get-Item -LiteralPath $OutputPath | Select-Object FullName,Length
