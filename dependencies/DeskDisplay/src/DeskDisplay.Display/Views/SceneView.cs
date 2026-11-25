using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.DataProviders;
using DeskDisplay.Display.Services;

namespace DeskDisplay.Display.Views;

/// <summary>Shared scene renderer for preview and external display.</summary>
public sealed partial class SceneView : FrameworkElement
{
    internal bool SkipThemeBackdrop { get; set; }
    private static readonly Brush BackgroundBrush = FrozenBrush(0x0C, 0x14, 0x22);
    private static readonly Brush ForegroundBrush = FrozenBrush(0xEE, 0xF4, 0xFC);
    private static readonly Brush AccentBrush = FrozenBrush(0x74, 0xC7, 0xEF);
    private static readonly Brush OverlayBrush = FrozenBrush(0x82, 0x05, 0x0A, 0x12);
    private static readonly Brush PanelBrush = FrozenBrush(0xA0, 0x0C, 0x14, 0x22);
    private static readonly Typeface Font = new(new FontFamily("Segoe UI, Microsoft YaHei"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    private static readonly Typeface CameraDigitsFont = new(new FontFamily("Bahnschrift SemiCondensed, Arial Narrow"), FontStyles.Normal, FontWeights.ExtraBold, FontStretches.SemiCondensed);
    private static readonly Lazy<BitmapImage> CameraBackground = new(LoadCameraBackground);
    private Scene? _observedScene;

    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene), typeof(Scene), typeof(SceneView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSceneChanged));

    public static readonly DependencyProperty AspectRatioOverrideProperty = DependencyProperty.Register(
        nameof(AspectRatioOverride), typeof(double), typeof(SceneView),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public Scene? Scene
    {
        get => (Scene?)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }
    public double AspectRatioOverride
    {
        get => (double)GetValue(AspectRatioOverrideProperty);
        set => SetValue(AspectRatioOverrideProperty, value);
    }
    public event Action<Exception>? RenderFailed;

    public SceneView()
    {
        Loaded += (_, _) => ObserveScene();
        Unloaded += (_, _) => StopObserving();
    }

    private static void OnSceneChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var view = (SceneView)sender;
        view.ObserveScene();
    }

    private void ObserveScene()
    {
        StopObserving();
        if (IsLoaded && Scene is { } scene)
        {
            _observedScene = scene;
            scene.PropertyChanged += OnScenePropertyChanged;
            if (scene is VideoScene video) video.Playback.StateChanged += OnVideoStateChanged;
        }
        UpdateAccessibleName();
        InvalidateVisual();
    }

    private void StopObserving()
    {
        if (_observedScene is VideoScene video) video.Playback.StateChanged -= OnVideoStateChanged;
        if (_observedScene is not null) _observedScene.PropertyChanged -= OnScenePropertyChanged;
        _observedScene = null;
    }

    private void OnVideoStateChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnScenePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // CurrentTime atomically changes all clock strings; no need to redraw for each derived property.
        if (e.PropertyName is nameof(ClockScene.TimeText) or nameof(ClockScene.DateText) or nameof(ClockScene.WeekdayText)) return;
        UpdateAccessibleName();
        InvalidateVisual();
    }

    private void UpdateAccessibleName() => AutomationProperties.SetName(this,
        Scene switch
        {
            ClockScene clock => $"{clock.TimeText} {clock.DateText} {clock.WeekdayText}",
            ImageScene image => $"图片预览：{System.IO.Path.GetFileName(image.Path)} · {image.Mode}",
            VideoFrameScene video => $"D92 视频画面：{System.IO.Path.GetFileName(video.Path)}",
            DashboardScene dashboard => $"组合场景：{dashboard.Clock.TimeText} · {(dashboard.ShowPerformance ? "显示性能" : "仅时钟")}",
            StockScene stocks => $"股票行情：{stocks.Quotes.Count} 只股票 · {stocks.Status}",
            RetroCameraClockScene camera => $"复古相机翻页时钟：{camera.CurrentTime:HH:mm:ss}",
            _ => "场景预览"
        });

    protected override AutomationPeer OnCreateAutomationPeer() => new FrameworkElementAutomationPeer(this);

    protected override void OnRender(DrawingContext context)
    {
        try
        {
            base.OnRender(context);
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            var frame = FitScene();
            if (!(SkipThemeBackdrop && Scene is ThemeScene)) context.DrawRectangle(BackgroundBrush, null, frame);
            if (Scene is ThemeScene theme) { DrawTheme(context, theme, frame); return; }
            if (Scene is DashboardScene dashboard)
            {
                DrawDashboard(context, dashboard, frame);
                return;
            }
            if (Scene is PerformanceScene performance)
            {
                DrawPerformance(context, performance, frame);
                return;
            }
            if (Scene is StockScene stocks)
            {
                DrawStocks(context, stocks, frame);
                return;
            }
            if (Scene is RetroCameraClockScene camera)
            {
                DrawRetroCameraClock(context, camera, frame);
                return;
            }
            if (Scene is VideoFrameScene { Frame: WpfSceneImage videoFrame })
            {
                context.DrawImage(videoFrame.Bitmap, frame);
                return;
            }
            if (Scene is VideoScene video)
            {
                if (video.Playback is WindowsVideoPlaybackService { Player: { } player } && player.NaturalVideoWidth > 0 && player.NaturalVideoHeight > 0)
                {
                    var videoScale = Math.Min(frame.Width / player.NaturalVideoWidth, frame.Height / player.NaturalVideoHeight);
                    var width = player.NaturalVideoWidth * videoScale;
                    var height = player.NaturalVideoHeight * videoScale;
                    context.DrawVideo(player, new Rect(frame.Left + (frame.Width - width) / 2, frame.Top + (frame.Height - height) / 2, width, height));
                }
                return;
            }
            if (Scene is ImageScene imageScene && imageScene.Image is WpfSceneImage image)
            {
                var destination = ImageDestination(imageScene, frame);
                context.PushClip(new RectangleGeometry(frame));
                try { context.DrawImage(image.Bitmap, destination); }
                finally { context.Pop(); }
                return;
            }
            var dpi = VisualTreeHelper.GetDpi(this);
            var borderX = Math.Min(frame.Width / 2, 2 / dpi.DpiScaleX);
            var borderY = Math.Min(frame.Height / 2, 2 / dpi.DpiScaleY);
            context.DrawRectangle(AccentBrush, null, new Rect(frame.Left, frame.Top, frame.Width, borderY));
            context.DrawRectangle(AccentBrush, null, new Rect(frame.Left, frame.Bottom - borderY, frame.Width, borderY));
            context.DrawRectangle(AccentBrush, null, new Rect(frame.Left, frame.Top, borderX, frame.Height));
            context.DrawRectangle(AccentBrush, null, new Rect(frame.Right - borderX, frame.Top, borderX, frame.Height));
            if (Scene is not ClockScene clock) return;
            var lines = new[]
            {
                Text(clock.TimeText, 96, ForegroundBrush, dpi.PixelsPerDip),
                Text(clock.DateText, 28, ForegroundBrush, dpi.PixelsPerDip),
                Text(clock.WeekdayText, 24, AccentBrush, dpi.PixelsPerDip)
            };
            const double gap = 16;
            var groupWidth = lines.Max(line => line.WidthIncludingTrailingWhitespace);
            var groupHeight = lines.Sum(line => line.Height) + gap * (lines.Length - 1);
            var scale = Math.Min(frame.Width * 0.85 / groupWidth, frame.Height * 0.7 / groupHeight);
            var y = frame.Top + (frame.Height - groupHeight * scale) / 2;
            foreach (var line in lines)
            {
                context.PushTransform(new MatrixTransform(scale, 0, 0, scale, frame.Left + (frame.Width - line.Width * scale) / 2, y));
                try { context.DrawText(line, new Point(0, 0)); }
                finally { context.Pop(); }
                y += (line.Height + gap) * scale;
            }
        }
        catch (Exception exception)
        {
            Serilog.Log.Error(exception, "SceneView rendering failed");
            RenderFailed?.Invoke(exception);
        }
    }

    private Rect FitScene()
    {
        var ratio = double.IsFinite(AspectRatioOverride) && AspectRatioOverride > 0
            ? AspectRatioOverride : Scene?.AspectRatio ?? ActualWidth / ActualHeight;
        var width = Math.Min(ActualWidth, ActualHeight * ratio);
        var height = width / ratio;
        return new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height);
    }

    private void DrawPerformance(DrawingContext context, PerformanceScene scene, Rect frame)
    {
        var values = scene.Values;
        var scale = Math.Min(frame.Width / 900, frame.Height / 330);
        context.PushTransform(new MatrixTransform(scale, 0, 0, scale,
            frame.Left + (frame.Width - 900 * scale) / 2, frame.Top + (frame.Height - 330 * scale) / 2));
        try
        {
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var rows = new[] { ("CPU", values.CpuLoad, values.CpuTemperature), ("GPU", values.GpuLoad, values.GpuTemperature), ("RAM", values.MemoryLoad, (double?)null) };
            for (var index = 0; index < rows.Length; index++)
            {
                var (name, load, temperature) = rows[index];
                var x = 30 + index * 300;
                context.DrawText(Text(name, 30, AccentBrush, dpi), new Point(x, 35));
                context.DrawText(Text(FormatSensor(load, "%"), 48, ForegroundBrush, dpi), new Point(x, 95));
                if (name != "RAM") context.DrawText(Text(FormatSensor(temperature, " °C"), 26, ForegroundBrush, dpi), new Point(x, 175));
                context.DrawRoundedRectangle(ForegroundBrush, null, new Rect(x, 240, 230, 8), 4, 4);
                if (load.HasValue) context.DrawRoundedRectangle(AccentBrush, null, new Rect(x, 240, 230 * Math.Clamp(load.Value, 0, 100) / 100, 8), 4, 4);
            }
        }
        finally { context.Pop(); }
    }

    private void DrawStocks(DrawingContext context, StockScene scene, Rect frame)
    {
        const double designWidth = 1920, designHeight = 462;
        var scale = Math.Min(frame.Width / designWidth, frame.Height / designHeight);
        context.PushTransform(new MatrixTransform(scale, 0, 0, scale,
            frame.Left + (frame.Width - designWidth * scale) / 2, frame.Top + (frame.Height - designHeight * scale) / 2));
        try
        {
            var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            context.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, designWidth, designHeight));
            context.DrawText(Text("A 股自选行情", 34, ForegroundBrush, dpi), new Point(34, 25));
            var clock = DateTime.Now.ToString("yyyy-MM-dd  dddd  HH:mm:ss", CultureInfo.GetCultureInfo("zh-CN"));
            var time = Text(clock, 20, FrozenBrush(0x91, 0xA0, 0xB5), dpi);
            context.DrawText(time, new Point(designWidth - time.Width - 34, 34));
            var quotes = scene.Quotes.Take(4).ToArray();
            if (quotes.Length == 0)
            {
                var empty = Text(scene.Status, 28, FrozenBrush(0x91, 0xA0, 0xB5), dpi);
                context.DrawText(empty, new Point((designWidth - empty.Width) / 2, 210));
                return;
            }
            const double gap = 18, left = 32, top = 91, height = 322;
            var width = (designWidth - left * 2 - gap * 3) / 4;
            for (var index = 0; index < quotes.Length; index++)
            {
                var quote = quotes[index];
                var x = left + index * (width + gap);
                context.DrawRoundedRectangle(FrozenBrush(0x14, 0x1D, 0x29), null, new Rect(x, top, width, height), 14, 14);
                var positive = quote.Percent > 0;
                var valueBrush = quote.Percent == 0 ? FrozenBrush(0x91, 0xA0, 0xB5) : positive ? FrozenBrush(0xFF, 0x64, 0x6D) : FrozenBrush(0x3B, 0xD6, 0xA0);
                context.DrawText(Text(quote.Name, 30, ForegroundBrush, dpi), new Point(x + 26, top + 27));
                context.DrawText(Text(quote.Symbol[2..], 18, FrozenBrush(0x91, 0xA0, 0xB5), dpi), new Point(x + 27, top + 72));
                context.DrawText(Text(quote.Price.ToString("N2", CultureInfo.InvariantCulture), 58, ForegroundBrush, dpi), new Point(x + 25, top + 119));
                var percent = (quote.Percent > 0 ? "+" : "") + quote.Percent.ToString("0.00", CultureInfo.InvariantCulture) + "%";
                context.DrawText(Text(percent, 31, valueBrush, dpi), new Point(x + 27, top + 195));
                context.DrawRoundedRectangle(FrozenBrush(0x26, 0x33, 0x47), null, new Rect(x + 26, top + 264, width - 52, 7), 3.5, 3.5);
                var marker = Math.Clamp(0.5 + quote.Percent / 20, 0.03, 0.97);
                context.DrawRoundedRectangle(valueBrush, null, new Rect(x + 26, top + 264, (width - 52) * marker, 7), 3.5, 3.5);
            }
            var status = Text(scene.Status, 16, FrozenBrush(0x91, 0xA0, 0xB5), dpi);
            context.DrawText(status, new Point(designWidth - status.Width - 34, 431));
        }
        finally { context.Pop(); }
    }

    private void DrawRetroCameraClock(DrawingContext context, RetroCameraClockScene scene, Rect frame)
    {
        const double sourceWidth = 1981, sourceHeight = 794, designWidth = 1920, designHeight = 462;
        var fit = Math.Min(frame.Width / designWidth, frame.Height / designHeight);
        var originX = frame.Left + (frame.Width - designWidth * fit) / 2;
        var originY = frame.Top + (frame.Height - designHeight * fit) / 2;
        context.PushClip(new RectangleGeometry(new Rect(originX, originY, designWidth * fit, designHeight * fit)));
        context.PushTransform(new MatrixTransform(fit, 0, 0, fit, originX, originY));
        try
        {
            var sourceScale = designWidth / sourceWidth;
            context.DrawImage(CameraBackground.Value,
                new Rect(0, -scene.CropY * sourceScale, designWidth, sourceHeight * sourceScale));
            var panels = new[]
            {
                new Rect(304,370,186,157), new Rect(594,370,188,157), new Rect(936,370,193,157),
                new Rect(1232,370,193,157), new Rect(1521,370,184,157)
            };
            var from = CameraValues(scene.PreviousTime.LocalDateTime, scene.Hour12);
            var to = CameraValues(scene.CurrentTime.LocalDateTime, scene.Hour12);
            for (var index = 0; index < panels.Length; index++)
            {
                var panel = panels[index];
                var rect = new Rect(panel.X * sourceScale, panel.Y * sourceScale - scene.CropY * sourceScale,
                    panel.Width * sourceScale, panel.Height * sourceScale);
                DrawCameraFlip(context, from[index], to[index], scene.AnimationProgress, rect);
            }
        }
        finally
        {
            context.Pop();
            context.Pop();
        }
    }

    private static string[] CameraValues(DateTime time, bool hour12)
    {
        var hour = hour12 ? time.Hour % 12 is var value && value != 0 ? value : 12 : time.Hour;
        return [time.Month.ToString("00"), time.Day.ToString("00"), hour.ToString("00"), time.Minute.ToString("00"), time.Second.ToString("00")];
    }

    private static void DrawCameraFlip(DrawingContext context, string from, string to, double progress, Rect rect)
    {
        if (progress >= 1 || from == to) { DrawCameraTile(context, to, rect, rect); return; }
        var half = rect.Height / 2;
        var top = new Rect(rect.Left, rect.Top, rect.Width, half);
        var bottom = new Rect(rect.Left, rect.Top + half, rect.Width, rect.Height - half);
        DrawCameraTile(context, to, rect, top);
        DrawCameraTile(context, from, rect, bottom);
        if (progress < .5)
        {
            var folded = half * Math.Cos(progress * Math.PI);
            var fold = new Rect(rect.Left, rect.Top + half - folded, rect.Width, folded);
            DrawCameraTile(context, from, rect, fold);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(118 * (1 - folded / half)), 0, 0, 0)), null, fold);
        }
        else
        {
            var folded = half * Math.Sin((progress - .5) * Math.PI);
            var fold = new Rect(rect.Left, rect.Top + half, rect.Width, folded);
            DrawCameraTile(context, to, rect, fold);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(107 * (1 - folded / half)), 0, 0, 0)), null, fold);
        }
        context.DrawRectangle(FrozenBrush(0xB0, 0x00, 0x00, 0x00), null, new Rect(rect.Left, rect.Top + half - 1, rect.Width, 2));
    }

    private static void DrawCameraTile(DrawingContext context, string value, Rect tileRect, Rect clip)
    {
        if (clip.Width <= 0 || clip.Height <= .25) return;
        context.PushClip(new RectangleGeometry(clip));
        try
        {
            var radius = Math.Max(3, tileRect.Height * .045);
            var upper = new LinearGradientBrush(Color.FromRgb(0x28, 0x24, 0x1E), Color.FromRgb(0x17, 0x14, 0x0F), 90);
            var lower = new LinearGradientBrush(Color.FromRgb(0x1B, 0x18, 0x13), Color.FromRgb(0x28, 0x24, 0x1F), 90);
            context.DrawRoundedRectangle(upper, null, new Rect(tileRect.Left, tileRect.Top, tileRect.Width, tileRect.Height / 2 + 1), radius, radius);
            context.DrawRoundedRectangle(lower, null, new Rect(tileRect.Left, tileRect.Top + tileRect.Height / 2, tileRect.Width, tileRect.Height / 2), radius, radius);
            var dpi = 1d;
            var cellWidth = tileRect.Width * .43;
            for (var digit = 0; digit < 2; digit++)
            {
                var text = new FormattedText(value[digit].ToString(), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    CameraDigitsFont, tileRect.Height * .79, FrozenBrush(0xEA, 0xD8, 0xBA), dpi);
                var x = tileRect.Left + tileRect.Width * .07 + cellWidth * (digit + .5) - text.Width / 2;
                var y = tileRect.Top + tileRect.Height * .525 - text.Height / 2;
                context.DrawText(text, new Point(x, y));
            }
            context.DrawRectangle(FrozenBrush(0xC7, 0x00, 0x00, 0x00), null,
                new Rect(tileRect.Left, tileRect.Top + tileRect.Height / 2 - 1, tileRect.Width, 2));
        }
        finally { context.Pop(); }
    }

    private static BitmapImage LoadCameraBackground()
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri("pack://application:,,,/DeskDisplay.Display;component/Assets/retro-camera-blank.png", UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void DrawDashboard(DrawingContext context, DashboardScene scene, Rect frame)
    {
        if (scene.Background?.Image is WpfSceneImage image)
        {
            var destination = ImageDestination(scene.Background, frame, scene.PixelWidth);
            context.PushClip(new RectangleGeometry(frame));
            try { context.DrawImage(image.Bitmap, destination); }
            finally { context.Pop(); }
        }
        context.DrawRectangle(OverlayBrush, null, frame);
        var inset = Math.Max(8, frame.Height * 0.06);
        var content = new Rect(frame.Left + inset, frame.Top + inset,
            Math.Max(1, frame.Width - inset * 2), Math.Max(1, frame.Height - inset * 2));
        if (!scene.ShowPerformance)
        {
            DrawDashboardClock(context, scene.Clock, scene.Layout == DashboardLayout.ClockLeft
                ? new Rect(content.Left, content.Top, content.Width * 0.62, content.Height)
                : content);
            return;
        }
        switch (scene.Layout)
        {
            case DashboardLayout.ClockCenter:
                DrawDashboardClock(context, scene.Clock, new Rect(content.Left + content.Width * 0.12, content.Top,
                    content.Width * 0.76, content.Height * 0.66));
                DrawDashboardPerformance(context, scene.Performance, new Rect(content.Left, content.Top + content.Height * 0.72,
                    content.Width, content.Height * 0.28), true);
                break;
            case DashboardLayout.ClockLeft:
                DrawDashboardClock(context, scene.Clock, new Rect(content.Left, content.Top, content.Width * 0.62, content.Height * 0.68));
                DrawDashboardPerformance(context, scene.Performance, new Rect(content.Left, content.Top + content.Height * 0.74,
                    content.Width, content.Height * 0.26), true);
                break;
            default:
                DrawDashboardClock(context, scene.Clock, new Rect(content.Left, content.Top, content.Width * 0.56, content.Height));
                DrawDashboardPerformance(context, scene.Performance, new Rect(content.Left + content.Width * 0.61, content.Top,
                    content.Width * 0.39, content.Height), false);
                break;
        }
    }

    private void DrawDashboardClock(DrawingContext context, ClockScene clock, Rect area)
    {
        context.DrawRoundedRectangle(PanelBrush, null, area, Math.Max(8, area.Height * 0.08), Math.Max(8, area.Height * 0.08));
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var lines = new[]
        {
            Text(clock.TimeText, 96, ForegroundBrush, dpi),
            Text(clock.DateText, 26, ForegroundBrush, dpi),
            Text(clock.WeekdayText, 22, AccentBrush, dpi)
        };
        const double gap = 12;
        var naturalWidth = lines.Max(line => line.WidthIncludingTrailingWhitespace);
        var naturalHeight = lines.Sum(line => line.Height) + gap * 2;
        var scale = Math.Min(area.Width * 0.82 / naturalWidth, area.Height * 0.76 / naturalHeight);
        var y = area.Top + (area.Height - naturalHeight * scale) / 2;
        foreach (var line in lines)
        {
            context.PushTransform(new MatrixTransform(scale, 0, 0, scale,
                area.Left + (area.Width - line.Width * scale) / 2, y));
            try { context.DrawText(line, new Point()); }
            finally { context.Pop(); }
            y += (line.Height + gap) * scale;
        }
    }

    private void DrawDashboardPerformance(DrawingContext context, PerformanceScene scene, Rect area, bool horizontal)
    {
        context.DrawRoundedRectangle(PanelBrush, null, area, Math.Max(8, area.Height * 0.08), Math.Max(8, area.Height * 0.08));
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var values = scene.Values;
        var rows = new[] { ("CPU", values.CpuLoad, values.CpuTemperature), ("GPU", values.GpuLoad, values.GpuTemperature), ("RAM", values.MemoryLoad, (double?)null) };
        for (var index = 0; index < rows.Length; index++)
        {
            var (name, load, temperature) = rows[index];
            var cell = horizontal
                ? new Rect(area.Left + area.Width * index / 3, area.Top, area.Width / 3, area.Height)
                : new Rect(area.Left, area.Top + area.Height * index / 3, area.Width, area.Height / 3);
            var label = Text(name, Math.Max(10, cell.Height * 0.18), AccentBrush, dpi);
            var value = Text(FormatSensor(load, "%"), Math.Max(14, cell.Height * 0.31), ForegroundBrush, dpi);
            var detail = Text(name == "RAM" ? "内存占用" : FormatSensor(temperature, " °C"), Math.Max(9, cell.Height * 0.14), ForegroundBrush, dpi);
            var valueX = horizontal ? cell.Left + (cell.Width - value.Width) / 2 : cell.Left + cell.Width * 0.12;
            context.DrawText(label, new Point(horizontal ? cell.Left + (cell.Width - label.Width) / 2 : valueX, cell.Top + cell.Height * 0.08));
            context.DrawText(value, new Point(valueX, cell.Top + cell.Height * 0.34));
            context.DrawText(detail, new Point(horizontal ? cell.Left + (cell.Width - detail.Width) / 2
                : cell.Right - detail.Width - cell.Width * 0.1, cell.Top + cell.Height * 0.68));
        }
    }

    internal static string FormatSensor(double? value, string suffix) =>
        value is { } number && double.IsFinite(number) ? number.ToString("0.0", CultureInfo.InvariantCulture) + suffix : "--";

    internal static Rect ImageDestination(ImageScene scene, Rect frame)
        => ImageDestination(scene, frame, scene.PixelWidth);

    internal static Rect ImageDestination(ImageScene scene, Rect frame, int scenePixelWidth)
    {
        var width = (double)scene.Image.OriginalWidth;
        var height = (double)scene.Image.OriginalHeight;
        if (scene.Mode == ImageDisplayMode.Stretch) return frame;
        var scale = scene.Mode switch
        {
            ImageDisplayMode.Fill => Math.Max(frame.Width / width, frame.Height / height),
            ImageDisplayMode.Uniform => Math.Min(frame.Width / width, frame.Height / height),
            // Center uses source pixels in scene coordinates, independent of preview DPI and decode size.
            ImageDisplayMode.Center => frame.Width / scenePixelWidth,
            _ => throw new InvalidOperationException("Unknown image display mode.")
        };
        width *= scale;
        height *= scale;
        return new Rect(frame.Left + (frame.Width - width) / 2, frame.Top + (frame.Height - height) / 2, width, height);
    }

    private static FormattedText Text(string text, double size, Brush brush, double pixelsPerDip) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, size, brush, pixelsPerDip);

    private static Brush FrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static Brush FrozenBrush(byte alpha, byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
        brush.Freeze();
        return brush;
    }
}
