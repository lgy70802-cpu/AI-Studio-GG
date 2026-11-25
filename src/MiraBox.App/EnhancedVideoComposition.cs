using System.Text.Json;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Display.Services;

namespace MiraBox.App;

// Runs on the existing video STA before the transport JPEG is encoded.
internal sealed class EnhancedVideoComposition(
    JsonElement videoLayer, bool background, int width, int height,
    Func<DateTimeOffset> now,
    Func<DateTimeOffset, (BitmapSource Below, BitmapSource Above)> renderStatic,
    bool refreshStaticEverySecond = true) : IVideoFrameComposition
{
    BitmapSource? below;
    BitmapSource? above;
    long renderedSecond = long.MinValue;
    byte[]? sourcePixels, composedPixels, overlayPixels, belowPixels;
    (int Low, int High, int Weight)[]? horizontal, vertical;
    (int Width, int Height, string Fit) sourceGeometry;
    Int32Rect overlayBounds;
    Rect elementGeometry;
    internal bool UsePixelCompositor { get; set; } = true;
    internal bool RefreshStaticEachSecond { get; set; } = refreshStaticEverySecond;
    internal bool ReuseEncoderBitmap { get; set; }
    internal bool UseSimd { get; set; } = true;
    int[]? elementLow, elementHigh;
    uint[]? elementWeight;
    int elementFirst, elementEnd;
    WriteableBitmap? encoderBitmap;
    RenderTargetBitmap? drawingBitmap;
    internal event Action<double, double, double, long>? PixelMeasured;

    static double Number(JsonElement value, string name, double fallback = 0) =>
        value.TryGetProperty(name, out var item) && item.TryGetDouble(out var number) ? number : fallback;
    static string Text(JsonElement value, string name, string fallback = "") =>
        value.TryGetProperty(name, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() ?? fallback : fallback;

    public Task<BitmapSource> ComposeAsync(BitmapSource video, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var time = now();
        if (below == null || RefreshStaticEachSecond && renderedSecond != time.ToUnixTimeSeconds())
        {
            (below, above) = renderStatic(time);
            renderedSecond = time.ToUnixTimeSeconds();
            CacheOverlay();
        }
        if (UsePixelCompositor && background && Number(videoLayer, "rot") == 0 && Number(videoLayer, "sc", 1) == 1 &&
            Number(videoLayer, "tx") == 0 && Number(videoLayer, "ty") == 0)
            return Task.FromResult(ComposeBackgroundPixels(video));
        if (UsePixelCompositor && !background && Number(videoLayer, "rot") % 360 == 0 &&
            Number(videoLayer, "sx", 1) > 0 && Number(videoLayer, "sy", 1) > 0)
            return Task.FromResult(ComposeElementPixels(video));
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
            drawing.DrawImage(below, new Rect(0, 0, width, height));
            var bounds = background ? new Rect(0, 0, width, height) : ElementBounds();
            var matrix = Matrix.Identity;
            matrix.ScaleAt(background ? Number(videoLayer, "sc", 1) : Number(videoLayer, "sx", 1),
                background ? Number(videoLayer, "sc", 1) : Number(videoLayer, "sy", 1),
                bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            matrix.RotateAt(Number(videoLayer, "rot"), bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            if (background) matrix.Translate(Number(videoLayer, "tx") * width / 100, Number(videoLayer, "ty") * height / 100);
            drawing.PushTransform(new MatrixTransform(matrix));
            drawing.PushOpacity(background ? 1 : Number(videoLayer, "alpha", 1));
            drawing.PushClip(new RectangleGeometry(bounds));
            var destination = bounds;
            if (background && Text(videoLayer, "fit", "cover") != "stretch")
            {
                var contain = Text(videoLayer, "fit") == "contain";
                var scale = contain ? Math.Min(bounds.Width / video.PixelWidth, bounds.Height / video.PixelHeight) :
                    Math.Max(bounds.Width / video.PixelWidth, bounds.Height / video.PixelHeight);
                destination = new Rect(bounds.X + (bounds.Width - video.PixelWidth * scale) / 2,
                    bounds.Y + (bounds.Height - video.PixelHeight * scale) / 2,
                    video.PixelWidth * scale, video.PixelHeight * scale);
            }
            drawing.DrawImage(video, destination);
            drawing.Pop(); drawing.Pop(); drawing.Pop();
            if (background && Number(videoLayer, "dim") > 0)
                drawing.DrawRectangle(new SolidColorBrush(Color.FromArgb((byte)(Number(videoLayer, "dim") * 255 / 100), 0, 0, 0)),
                    null, new Rect(0, 0, width, height));
            drawing.DrawImage(above, new Rect(0, 0, width, height));
            drawing.Pop();
        }
        var frame = ReuseEncoderBitmap ? drawingBitmap ??= new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32) :
            new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        if (ReuseEncoderBitmap) frame.Clear();
        frame.Render(visual);
        if (!ReuseEncoderBitmap) frame.Freeze();
        return Task.FromResult<BitmapSource>(frame);
    }

    void CacheOverlay()
    {
        belowPixels ??= new byte[width * height * 4];
        var under = below!.Format == PixelFormats.Pbgra32 ? below : new FormatConvertedBitmap(below, PixelFormats.Pbgra32, null, 0);
        under.CopyPixels(belowPixels, width * 4, 0);
        overlayPixels ??= new byte[width * height * 4];
        above!.CopyPixels(overlayPixels, width * 4, 0);
        var left = width; var top = height; var right = -1; var bottom = -1;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (overlayPixels[(y * width + x) * 4 + 3] == 0) continue;
            left = Math.Min(left, x); right = Math.Max(right, x);
            top = Math.Min(top, y); bottom = y;
        }
        overlayBounds = right < left ? Int32Rect.Empty : new(left, top, right - left + 1, bottom - top + 1);
    }

    BitmapSource ComposeElementPixels(BitmapSource input)
    {
        var started = Stopwatch.GetTimestamp();
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var source = input.Format == PixelFormats.Bgra32 ? input : new FormatConvertedBitmap(input, PixelFormats.Bgra32, null, 0);
        var bounds = ElementBounds();
        var sx = Number(videoLayer, "sx", 1); var sy = Number(videoLayer, "sy", 1);
        bounds = new(bounds.X + bounds.Width * (1 - sx) / 2, bounds.Y + bounds.Height * (1 - sy) / 2,
            bounds.Width * sx, bounds.Height * sy);
        if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) || !double.IsFinite(bounds.Width) ||
            !double.IsFinite(bounds.Height) || bounds.Width <= 0 || bounds.Height <= 0)
            throw new InvalidDataException("视频元素尺寸或位置无效。");
        var geometry = (source.PixelWidth, source.PixelHeight, "element");
        if (horizontal == null || sourceGeometry != geometry || elementGeometry != bounds)
        {
            horizontal = Axis(source.PixelWidth, width, bounds.Width / source.PixelWidth, bounds.X);
            vertical = Axis(source.PixelHeight, height, bounds.Height / source.PixelHeight, bounds.Y);
            sourceGeometry = geometry; elementGeometry = bounds;
            elementLow = horizontal.Select(column => Math.Max(0, column.Low)).ToArray();
            elementHigh = horizontal.Select(column => Math.Max(0, column.High)).ToArray();
            elementWeight = horizontal.Select(column => (uint)column.Weight).ToArray();
            elementFirst = Array.FindIndex(horizontal, column => column.Low >= 0);
            elementEnd = Array.FindLastIndex(horizontal, column => column.Low >= 0) + 1;
        }
        var length = source.PixelWidth * source.PixelHeight * 4;
        if (sourcePixels == null || sourcePixels.Length != length) sourcePixels = new byte[length];
        composedPixels ??= new byte[width * height * 4];
        source.CopyPixels(sourcePixels, source.PixelWidth * 4, 0);
        Buffer.BlockCopy(belowPixels!, 0, composedPixels, 0, composedPixels.Length);
        var copyMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        started = Stopwatch.GetTimestamp();
        var opacity = (int)Math.Round(Math.Clamp(Number(videoLayer, "alpha", 1), 0, 1) * 256);
        var inputPixels = MemoryMarshal.Cast<byte, uint>(sourcePixels.AsSpan());
        var outputPixels = MemoryMarshal.Cast<byte, uint>(composedPixels.AsSpan());
        for (var y = 0; y < height; y++)
        {
            var row = vertical![y];
            if (row.Low < 0) continue;
            var first = UseSimd && Avx2.IsSupported ? BlendRowSimd(y, row, opacity) : 0;
            for (var x = first; x < width; x++)
            {
                var column = horizontal[x];
                if (column.Low < 0) continue;
                var target = y * width + x;
                var tl = row.Low * source.PixelWidth + column.Low;
                var tr = row.Low * source.PixelWidth + column.High;
                var bl = row.High * source.PixelWidth + column.Low;
                var br = row.High * source.PixelWidth + column.High;
                var pixel = MixPixel(MixPixel(inputPixels[tl], inputPixels[tr], column.Weight),
                    MixPixel(inputPixels[bl], inputPixels[br], column.Weight), row.Weight);
                var alpha = (int)(((pixel >> 24) * opacity + 128) >> 8);
                if (alpha == 0) continue;
                outputPixels[target] = alpha == 255 ? pixel :
                    MixPixel(outputPixels[target], pixel | 0xff000000u, (alpha * 256 + 127) / 255);
            }
        }
        BlendOverlay();
        var blendMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        started = Stopwatch.GetTimestamp();
        var result = OutputBitmap(PixelFormats.Pbgra32);
        PixelMeasured?.Invoke(copyMs, blendMs, Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - allocated);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static uint MixPixel(uint a, uint b, int weight)
    {
        var w = (uint)weight; var inverse = 256 - w;
        const uint mask = 0x00ff00ff;
        var rb = (((a & mask) * inverse + (b & mask) * w + 0x00800080u) >> 8) & mask;
        var ag = (((((a >> 8) & mask) * inverse + ((b >> 8) & mask) * w + 0x00800080u) >> 8) & mask) << 8;
        return rb | ag;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static Vector256<uint> MixPixels(Vector256<uint> a, Vector256<uint> b, Vector256<uint> weight)
    {
        var mask = Vector256.Create(0x00ff00ffu); var rounding = Vector256.Create(0x00800080u);
        var inverse = Vector256.Create(256u) - weight;
        var rb = Avx2.ShiftRightLogical(Avx2.MultiplyLow(a & mask, inverse) + Avx2.MultiplyLow(b & mask, weight) + rounding, 8) & mask;
        var ag = Avx2.ShiftRightLogical(Avx2.MultiplyLow(Avx2.ShiftRightLogical(a, 8) & mask, inverse) +
            Avx2.MultiplyLow(Avx2.ShiftRightLogical(b, 8) & mask, weight) + rounding, 8) & mask;
        return rb | Avx2.ShiftLeftLogical(ag, 8);
    }

    unsafe int BlendRowSimd(int y, (int Low, int High, int Weight) row, int opacity)
    {
        if (elementFirst < 0) return width;
        var x = elementFirst;
        fixed (byte* input = sourcePixels, output = composedPixels)
        fixed (int* lows = elementLow, highs = elementHigh)
        fixed (uint* weights = elementWeight)
        {
            var top = (uint*)input + row.Low * sourceGeometry.Width;
            var bottom = (uint*)input + row.High * sourceGeometry.Width;
            var destination = (uint*)output + y * width;
            var verticalWeight = Vector256.Create((uint)row.Weight);
            var opaque = Vector256.Create(0xff000000u);
            for (; x + 8 <= elementEnd; x += 8)
            {
                var left = Vector256.Load(lows + x); var right = Vector256.Load(highs + x);
                var horizontalWeight = Vector256.Load(weights + x);
                var upper = MixPixels(Avx2.GatherVector256(top, left, 4), Avx2.GatherVector256(top, right, 4), horizontalWeight);
                var lower = MixPixels(Avx2.GatherVector256(bottom, left, 4), Avx2.GatherVector256(bottom, right, 4), horizontalWeight);
                var pixel = MixPixels(upper, lower, verticalWeight);
                if (opacity == 256 && Vector256.EqualsAll(pixel & opaque, opaque)) Vector256.Store(pixel, destination + x);
                else
                {
                    var alpha = Avx2.ShiftRightLogical(Avx2.MultiplyLow(Avx2.ShiftRightLogical(pixel, 24), Vector256.Create((uint)opacity)) + Vector256.Create(128u), 8);
                    var weight = alpha + Avx2.ShiftRightLogical(alpha, 7);
                    Vector256.Store(MixPixels(Vector256.Load(destination + x), pixel | opaque, weight), destination + x);
                }
            }
        }
        return x;
    }

    void BlendOverlay()
    {
        for (var y = overlayBounds.Y; y < overlayBounds.Y + overlayBounds.Height; y++)
        for (var x = overlayBounds.X; x < overlayBounds.X + overlayBounds.Width; x++)
        {
            var index = (y * width + x) * 4;
            var alpha = overlayPixels![index + 3];
            if (alpha == 0) continue;
            for (var channel = 0; channel < 3; channel++)
                composedPixels![index + channel] = (byte)Math.Min(255, overlayPixels[index + channel] + (composedPixels[index + channel] * (255 - alpha) + 127) / 255);
            composedPixels![index + 3] = (byte)Math.Min(255, alpha + (composedPixels[index + 3] * (255 - alpha) + 127) / 255);
        }
    }

    BitmapSource OutputBitmap(PixelFormat format)
    {
        if (ReuseEncoderBitmap)
        {
            encoderBitmap ??= new WriteableBitmap(width, height, 96, 96, format, null);
            encoderBitmap.WritePixels(new Int32Rect(0, 0, width, height), composedPixels!, width * 4, 0);
            return encoderBitmap;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, format, null, composedPixels!, width * 4);
        bitmap.Freeze(); return bitmap;
    }

    BitmapSource ComposeBackgroundPixels(BitmapSource input)
    {
        var source = input.Format == PixelFormats.Bgra32 ? input : new FormatConvertedBitmap(input, PixelFormats.Bgra32, null, 0);
        var geometry = (source.PixelWidth, source.PixelHeight, Text(videoLayer, "fit", "cover"));
        var sourceLength = source.PixelWidth * source.PixelHeight * 4;
        if (sourcePixels == null || sourcePixels.Length != sourceLength) sourcePixels = new byte[sourceLength];
        composedPixels ??= new byte[width * height * 4];
        if (horizontal == null || sourceGeometry != geometry)
        {
            var sx = (double)width / source.PixelWidth;
            var sy = (double)height / source.PixelHeight;
            if (geometry.Item3 != "stretch") sx = sy = geometry.Item3 == "contain" ? Math.Min(sx, sy) : Math.Max(sx, sy);
            horizontal = Axis(source.PixelWidth, width, sx);
            vertical = Axis(source.PixelHeight, height, sy);
            sourceGeometry = geometry;
        }
        source.CopyPixels(sourcePixels, source.PixelWidth * 4, 0);
        var light = (int)Math.Round(256 * (1 - Number(videoLayer, "dim") / 100));
        var canCopyRows = source.PixelWidth == width && light == 256 && horizontal[0].Low == 0 &&
            horizontal[0].Weight == 0 && horizontal[^1].Low == width - 1 && horizontal[^1].Weight == 0;
        for (var y = 0; y < height; y++)
        {
            var row = vertical![y];
            if (canCopyRows && row.Low >= 0 && row.Weight == 0)
            {
                Buffer.BlockCopy(sourcePixels, row.Low * width * 4, composedPixels, y * width * 4, width * 4);
                for (var x = 0; x < width; x++) composedPixels[(y * width + x) * 4 + 3] = 255;
                continue;
            }
            for (var x = 0; x < width; x++)
            {
                var column = horizontal[x];
                var output = (y * width + x) * 4;
                composedPixels[output + 3] = 255;
                if (row.Low < 0 || column.Low < 0)
                { composedPixels[output] = composedPixels[output + 1] = composedPixels[output + 2] = 0; continue; }
                var tl = (row.Low * source.PixelWidth + column.Low) * 4;
                var tr = (row.Low * source.PixelWidth + column.High) * 4;
                var bl = (row.High * source.PixelWidth + column.Low) * 4;
                var br = (row.High * source.PixelWidth + column.High) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    var upper = sourcePixels[tl + channel] * (256 - column.Weight) + sourcePixels[tr + channel] * column.Weight;
                    var lower = sourcePixels[bl + channel] * (256 - column.Weight) + sourcePixels[br + channel] * column.Weight;
                    var value = (upper * (256 - row.Weight) + lower * row.Weight + 32768) >> 16;
                    composedPixels[output + channel] = (byte)((value * light + 128) >> 8);
                }
            }
        }
        for (var y = overlayBounds.Y; y < overlayBounds.Y + overlayBounds.Height; y++)
        for (var x = overlayBounds.X; x < overlayBounds.X + overlayBounds.Width; x++)
        {
            var index = (y * width + x) * 4;
            var alpha = overlayPixels![index + 3];
            if (alpha == 0) continue;
            for (var channel = 0; channel < 3; channel++)
                composedPixels[index + channel] = (byte)Math.Min(255, overlayPixels[index + channel] + (composedPixels[index + channel] * (255 - alpha) + 127) / 255);
        }
        return OutputBitmap(PixelFormats.Bgra32);
    }

    static (int Low, int High, int Weight)[] Axis(int source, int destination, double scale, double? position = null)
    {
        var result = new (int Low, int High, int Weight)[destination];
        var offset = position ?? (destination - source * scale) / 2;
        for (var index = 0; index < destination; index++)
        {
            var sample = (index + .5 - offset) / scale - .5;
            if (sample < -.5 || sample > source - .5) { result[index] = (-1, -1, 0); continue; }
            sample = Math.Clamp(sample, 0, source - 1);
            var low = (int)sample;
            result[index] = (low, Math.Min(source - 1, low + 1), (int)Math.Round((sample - low) * 256));
        }
        return result;
    }

    Rect ElementBounds()
    {
        var elementHeight = Number(videoLayer, "size", 30) * Math.Min(width, height) / 100;
        var elementWidth = elementHeight * Number(videoLayer, "ar", 1);
        var anchor = Text(videoLayer, "a", "mc");
        var x = anchor[1] == 'l' ? 0 : anchor[1] == 'r' ? 1 : .5;
        var y = anchor[0] == 't' ? 0 : anchor[0] == 'b' ? 1 : .5;
        return new Rect(x * width + Number(videoLayer, "x") * width / 100 - x * elementWidth,
            y * height + Number(videoLayer, "y") * height / 100 - y * elementHeight, elementWidth, elementHeight);
    }

    public void Dispose() { below = above = null; sourcePixels = composedPixels = overlayPixels = belowPixels = null; encoderBitmap = null; drawingBitmap = null; elementLow = elementHigh = null; elementWeight = null; }
}
