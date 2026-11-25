using DeskDisplay.Core.Models;
using DeskDisplay.Display.Views;
using DeskDisplay.Sensors;
using Serilog;

namespace DeskDisplay.DisplayTests;

internal static class SensorTests
{
    public static async Task RunAsync(ILogger logger)
    {
        var reader = new FakeReader();
        var creations = 0;
        await using (var service = new SensorService(logger, () => { creations++; return reader; }, TimeSpan.FromMilliseconds(40)))
        {
            await Task.WhenAll(service.StartAsync(), service.StartAsync());
            await WaitUntil(() => reader.Reads > 1);
            Check(creations == 1 && service.Current.CpuLoad == 42 && service.Current.CpuTemperature is null && service.Current.MemoryLoad == 55,
                "One sampling worker publishes supported values and preserves missing temperature");
            reader.Fail = true;
            await WaitUntil(() => service.Current.CpuLoad is null);
            reader.Fail = false;
            await WaitUntil(() => service.Current.CpuLoad == 42);
            Check(true, "Sampling exception publishes nulls and recovers on the next sample");
            await service.StopAsync();
            var reads = reader.Reads;
            await Task.Delay(100);
            Check(reader.Closed && reads == reader.Reads && service.Current.MemoryLoad is null, "Stop awaits reader release and stops sampling");
            await service.StartAsync();
            await WaitUntil(() => reader.Reads > reads);
            Check(creations == 2, "Stopped sensor service can start again");
        }
        var scene = new PerformanceScene();
        scene.Update(new SensorSnapshot { CpuLoad = 0, CpuTemperature = 40 }, TimeSpan.FromSeconds(1));
        scene.Update(new SensorSnapshot { CpuLoad = 100, CpuTemperature = null }, TimeSpan.FromMilliseconds(33));
        Check(scene.Values.CpuLoad is > 0 and < 20 && scene.Values.CpuTemperature is null, "Smoothing avoids load jumps but missing data becomes null immediately");
        for (var i = 0; i < 100; i++) scene.Update(new SensorSnapshot { CpuLoad = 100 }, TimeSpan.FromMilliseconds(33));
        Check(scene.Values.CpuLoad > 99 && SceneView.FormatSensor(null, "%") == "--", "Smoothed load converges and missing UI values use --");
        scene.Update(new SensorSnapshot { CpuLoad = double.NaN }, TimeSpan.FromSeconds(1));
        Check(scene.Values.CpuLoad is null, "Non-finite sensor values never reach the gauge");
        scene.SetCanvasSize(2400, 700);
        scene.Update(new SensorSnapshot { CpuLoad = 38.5, GpuLoad = 62, GpuTemperature = 54, MemoryLoad = 67.2 }, TimeSpan.FromSeconds(10));
        var view = new SceneView { Scene = scene };
        view.Measure(new System.Windows.Size(960, 280));
        view.Arrange(new System.Windows.Rect(0, 0, 960, 280));
        var image = new System.Windows.Media.Imaging.RenderTargetBitmap(960, 280, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        image.Render(view);
        var png = new System.Windows.Media.Imaging.PngBitmapEncoder();
        png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using (var file = System.IO.File.Create(System.IO.Path.Combine(AppContext.BaseDirectory, "performance-scene.png"))) png.Save(file);
        await using var live = new SensorService(logger);
        await live.StartAsync();
        await Task.Delay(2500);
        var snapshot = live.Current;
        Console.WriteLine($"LIVE SENSORS: CPU={snapshot.CpuLoad}, CPU°C={snapshot.CpuTemperature}, GPU={snapshot.GpuLoad}, GPU°C={snapshot.GpuTemperature}, RAM={snapshot.MemoryLoad}");
        Check(snapshot.CpuLoad is null or >= 0 and <= 100 && snapshot.MemoryLoad is null or >= 0 and <= 100,
            "Live hardware sampling survives unavailable sensors without invalid loads");
        await live.StopAsync();
        Console.WriteLine("All sensor tests passed.");
    }
    private static async Task WaitUntil(Func<bool> condition)
    {
        var timeout = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < timeout) await Task.Delay(20);
        Check(condition(), "Sensor worker completed expected state transition");
    }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS: " + message);
    }
    private sealed class FakeReader : ISensorReader
    {
        public volatile int Reads;
        public volatile bool Fail, Closed;
        public SensorSnapshot Read()
        {
            Interlocked.Increment(ref Reads);
            return Fail ? throw new InvalidOperationException("Simulated sensor failure") : new SensorSnapshot { CpuLoad = 42, MemoryLoad = 55 };
        }
        public void Dispose() => Closed = true;
    }
}
