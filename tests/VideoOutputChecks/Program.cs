using System.Collections.Concurrent;
using System.Diagnostics;
using System.Windows.Threading;
using DeskDisplay.Display.Usb;
using DeskDisplay.Display.Services;
using MiraBox.App;

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        Exception? failure = null;
        var loop = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
        {
            try
            {
                var cycles = args.Length > 1 ? int.Parse(args[1]) : 1;
                if (cycles is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(cycles));
                if (args.Length > 2 && args[2] == "--image-lifetime")
                {
                    ReportResources("image baseline");
                    for (var cycle = 0; cycle < cycles; cycle++)
                    {
                        foreach (var model in new[] { "D69", "D92" }) D92StaticImage.Encode(90, (_, _) => { }, model);
                        await Task.Delay(30);
                        if ((cycle + 1) % 10 == 0) ReportResources("image cycle " + (cycle + 1));
                    }
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    await Task.Delay(1000); ReportResources("image after diagnostic GC");
                    return;
                }
                if (args.Length > 2 && args[2] == "--native-lifetime")
                {
                    for (var cycle = 0; cycle < cycles; cycle++)
                    {
                        await using (var decoder = await WindowsLiveVideoFrameSource.OpenAsync(args[0], 480, 1280, CancellationToken.None))
                            await decoder.ReadLatestAsync(CancellationToken.None);
                        ReportResources("shared STA native cycle " + (cycle + 1));
                    }
                    return;
                }
                int? warmupHandles = null;
                for (var cycle = 0; cycle < cycles; cycle++)
                {
                    if (cycles > 1) await Task.WhenAll(new[] { "D69", "D92" }.Select(model => Verify(model, args[0])));
                    else foreach (var model in new[] { "D69", "D92" }) await Verify(model, args[0]);
                    if (cycle == 2) { using var process = Process.GetCurrentProcess(); warmupHandles = process.HandleCount; }
                    if (cycles > 1) ReportResources("cycle " + (cycle + 1));
                }
                if (cycles >= 10)
                {
                    using var process = Process.GetCurrentProcess();
                    Check(process.HandleCount <= warmupHandles + 200, "repeated concurrent decoder sessions keep handle growth bounded after warmup");
                }
                if (cycles > 1)
                {
                    // Diagnostic only: distinguish deferred managed/COM cleanup from a
                    // permanently retained decoder. The application never forces GC.
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    await Task.Delay(2000);
                    ReportResources("after diagnostic GC");
                    for (var wait = 1; wait <= 6; wait++)
                    {
                        await Task.Delay(10000);
                        ReportResources("idle " + (wait * 10) + "s");
                    }
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    await Task.Delay(2000);
                    ReportResources("after idle diagnostic GC");
                }
            }
            catch (Exception ex) { failure = ex; }
            finally { loop.Continue = false; }
        });
        Dispatcher.PushFrame(loop);
        if (failure == null) return 0;
        Console.Error.WriteLine(failure);
        return 1;
    }
    static void ReportResources(string label)
    {
        using var process = Process.GetCurrentProcess();
        Console.WriteLine($"RESOURCE {label}: handles={process.HandleCount}, threads={process.Threads.Count}, privateMiB={process.PrivateMemorySize64 / 1048576d:F1}");
    }
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
    static async Task Until(Func<bool> predicate)
    {
        var clock = Stopwatch.StartNew();
        while (!predicate()) { if (clock.Elapsed.TotalSeconds > 15) throw new TimeoutException(); await Task.Delay(30); }
    }
    static async Task Verify(string model, string video)
    {
        var link = new Link();
        var statuses = new ConcurrentQueue<string>();
        var still = D92StaticImage.Encode(90, (_, _) => { }, model);
        var invalid = 0;
        using var output = new D92StaticOutput("SIMULATED-" + model, 90, still, false,
            (_, _) => new Sender(link, bytes => { try { D92StaticImage.RequireTransportRaster(bytes, model); } catch { Interlocked.Increment(ref invalid); } }),
            null, model, model == "D69");
        output.SubmitVideo(video, "fixture", profile: new(10, false, 80));
        var running = output.RunAsync(state => statuses.Enqueue(state.Status));
        try
        {
            await Until(() => output.VideoHasWritten && output.VideoProgress != null && output.Frames > 3);
            Check(invalid == 0, model + " real decoder preserves transport raster");
            output.SetVideoPaused(true);
            await Task.Delay(300);
            var paused = output.VideoProgress!.Position;
            await Task.Delay(400);
            Check(output.VideoProgress.Position == paused, model + " pause freezes decoder position");
            output.SetVideoPaused(false);
            await Until(() => output.VideoProgress!.Position != paused);
            link.Online = false;
            await Until(() => statuses.Contains("WaitingForDevice"));
            await Task.Delay(300);
            var offlineFrames = output.Frames;
            var offlinePosition = output.VideoProgress!.Position;
            var submission = output.SubmissionId;
            await Task.Delay(600);
            Check(output.Frames == offlineFrames && output.VideoProgress.Position == offlinePosition, model + " simulated disconnect freezes delivery and decoder");
            link.Online = true;
            await Until(() => output.Connections == 2 && output.Frames > offlineFrames + 2);
            Check(output.SubmissionId == submission && output.VideoPath == video, model + " simulated reconnect retains same submission");
            Check(invalid == 0, model + " reconnect frames retain transport geometry");
        }
        finally { output.Stop(); await running.WaitAsync(TimeSpan.FromSeconds(10)); }
        Check(statuses.Last() == "Stopped", model + " stops pipeline cleanly");
    }
    sealed class Link { public volatile bool Online = true; }
    sealed class Sender(Link link, Action<byte[]> observe) : ID92PlaybackTransport
    {
        public event Action<D92OutputState>? StateChanged;
        public event Action<D92FrameTiming>? FrameCompleted;
        public async Task RunAsync(string serial, Func<CancellationToken, Task<byte[]>> render, TimeSpan interval, CancellationToken token)
        {
            long frames = 0;
            int connections = 0;
            bool connected = false, waiting = false;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!link.Online)
                    {
                        if (!waiting) StateChanged?.Invoke(new("WaitingForDevice", frames, connections));
                        connected = false; waiting = true;
                        await Task.Delay(10, token); continue;
                    }
                    if (!connected) { StateChanged?.Invoke(new("Connected", frames, ++connections)); connected = true; waiting = false; }
                    var bytes = await render(token);
                    observe(bytes);
                    FrameCompleted?.Invoke(new(++frames, bytes.Length, 0, 0));
                    StateChanged?.Invoke(new("Streaming", frames, connections));
                    await Task.Delay(interval, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            finally { StateChanged?.Invoke(new("Stopped", frames, connections)); }
        }
    }
}
