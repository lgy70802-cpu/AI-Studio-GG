using System.Diagnostics;
using System.IO;
using System.Text.Json;
using DeskDisplay.Core.Models;
using DeskDisplay.Display.Services;
using DeskDisplay.Display.Usb;
using Serilog;

namespace DeskDisplay.VideoPrototype;

internal static partial class Program
{
    private sealed record Plan(string VideoPath, int Width, int Height, int Rotation, int SecondsPerStage, int[] RequestedFps, string? SerialNumber = null, string[]? BaselineJpegs = null, string Backend = "TimestampSampling", bool ExperimentalHighFrameRate = false, bool CaptureDiagnostics = false, bool ComposeClock = false);
    private sealed record Frame(double AtSeconds, VideoSampleTiming Sample, D92FrameTiming? Transport);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static async Task<int> Main(string[] args)
    {
        try
        {
            if(args is ["--make-hid-fixtures",var fixturePlan,var fixtureDirectory])
                return await MakeFixturesAsync(JsonSerializer.Deserialize<Plan>(await File.ReadAllTextAsync(fixturePlan),Json)??throw new InvalidDataException("Empty plan"),fixtureDirectory);
            if(args is ["--analyze-hid",var measurements,var analysisDirectory])
                return await AnalyzeHidAsync(measurements,analysisDirectory);
            if (args is ["--list"])
            { Console.WriteLine(JsonSerializer.Serialize(D92HidDevice.Enumerate().Select(d => new { d.SerialNumber, d.ReportLength }), Json)); return 0; }
            if (args.Length != 3 || args[0] is not ("--offline" or "--run-real-device"))
            { Console.Error.WriteLine("Usage: --list | --offline <plan.json> <new-evidence-dir> | --run-real-device <plan.json> <new-evidence-dir>"); return 2; }
            var real = args[0] == "--run-real-device";
            var plan = JsonSerializer.Deserialize<Plan>(await File.ReadAllTextAsync(args[1]), Json) ?? throw new InvalidDataException("Empty plan");
            using var timerResolution = new VideoTimerResolution(plan.ExperimentalHighFrameRate);
            var maximumFps = plan.ExperimentalHighFrameRate ? 30 : 10;
            if (plan.SecondsPerStage is < 3 or > 60 || plan.RequestedFps is not { Length: > 0 and <= 4 } || plan.RequestedFps.Any(f => f < 1 || f > maximumFps)) throw new InvalidDataException("Bounded stages require 3–60 seconds, 1–10 FPS normally, or explicit ExperimentalHighFrameRate for up to 30 FPS.");
            if (plan.Width is < 1 or > 8192 || plan.Height is < 1 or > 8192 || (long)plan.Width * plan.Height > 16_777_216 || plan.Rotation is not (0 or 90 or 180 or 270)) throw new InvalidDataException("Invalid target geometry.");
            var directory = Path.GetFullPath(args[2]); if (Directory.Exists(directory)) throw new IOException("Evidence directory already exists; use a new path.");
            // Prepare and decode before opening a device or taking over any output.
            if (!Enum.TryParse<VideoFrameBackend>(plan.Backend, true, out var backend) || !Enum.IsDefined(backend)) throw new InvalidDataException("Unknown video backend.");
            using var fixtureScene=plan.ComposeClock&&plan.BaselineJpegs is null?CreateClockFixture(plan):null;
            var composition=fixtureScene is null?null:new ThemeVideoComposition(fixtureScene,_=>Task.FromResult<IReadOnlyDictionary<Guid,Scene>>(new Dictionary<Guid,Scene>()));
            await using var source = plan.BaselineJpegs is null ? await VideoJpegFrameSource.OpenAsync(plan.VideoPath, plan.Width, plan.Height, plan.Rotation, backend: backend, composition:composition, captureDiagnostics:plan.CaptureDiagnostics) : null;
            if (source is not null) await source.RenderAsync(default);
            await using var framePipeline = source is null ? null : new VideoFramePipeline(source, plan.ExperimentalHighFrameRate);
            var baseline = new List<byte[]>();
            if (plan.BaselineJpegs is { } paths)
            {
                if (paths.Length is < 2 or > 4) throw new InvalidDataException("Transport baseline needs 2–4 pre-encoded JPEGs.");
                foreach (var path in paths)
                {
                    if (new FileInfo(path).Length is < 4 or > 16 * 1024 * 1024) throw new InvalidDataException("Baseline JPEG is outside frame limits.");
                    var bytes = await File.ReadAllBytesAsync(path);
                    using var stream = new MemoryStream(bytes);
                    var decoded = System.Windows.Media.Imaging.BitmapDecoder.Create(stream, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
                    if (decoded is not System.Windows.Media.Imaging.JpegBitmapDecoder || decoded.Frames[0].PixelWidth != (plan.Rotation is 90 or 270 ? plan.Height : plan.Width) || decoded.Frames[0].PixelHeight != (plan.Rotation is 90 or 270 ? plan.Width : plan.Height)) throw new InvalidDataException("Baseline must be a JPEG with the exact rotated output dimensions.");
                    baseline.Add(bytes);
                }
            }
            VideoSampleTiming lastSample = source?.LastTiming ?? new(TimeSpan.Zero, 0, 0, 0);
            var baselineIndex = 0;
            async Task<byte[]> Render(CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                if (baseline.Count == 0)
                {
                    var prepared = await framePipeline!.ReadAsync(token);
                    lastSample = prepared.Timing;
                    return prepared.Bytes;
                }
                var frame = baseline[baselineIndex++ % baseline.Count];
                lastSample = new(TimeSpan.Zero, 0, 0, frame.Length); return frame;
            }
            if (real)
            {
                if (string.IsNullOrWhiteSpace(plan.SerialNumber) || D92HidDevice.Enumerate().Count(d => d.SerialNumber == plan.SerialNumber) != 1) throw new IOException("An explicit unique connected D92 serial is required.");
                foreach (var name in new[] { "MiraBox Craft", "DeskDisplay.App", "DeskDisplay.UsbDemo", "DeskDisplay.HardwareAcceptance" })
                {
                    var processes = Process.GetProcessesByName(name);
                    try { if (processes.Length != 0) throw new IOException("Close competing application first: " + name); }
                    finally { foreach (var process in processes) process.Dispose(); }
                }
            }
            Directory.CreateDirectory(directory);
            int? reportLength=real?D92HidDevice.Enumerate().Single(d=>d.SerialNumber==plan.SerialNumber).ReportLength:null;
            await File.WriteAllTextAsync(Path.Combine(directory, "plan.json"), JsonSerializer.Serialize(plan, Json));
            using var logger = new LoggerConfiguration().CreateLogger();
            using var cancelled = new CancellationTokenSource();
            ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; cancelled.Cancel(); }; Console.CancelKeyPress += cancel;
            var stages = new List<object>();
            using var measurementProcess = Process.GetCurrentProcess();
            try
            {
                foreach (var fps in plan.RequestedFps)
                {
                    var frames = new List<Frame>(); var states = new List<D92OutputState>();
                    var watch = Stopwatch.StartNew();
                    var cpuBefore = measurementProcess.TotalProcessorTime;
                    var allocatedBefore=GC.GetTotalAllocatedBytes(false);var gen2Before=GC.CollectionCount(2);
                    var gcPauseBefore=GC.GetTotalPauseDuration();
                    string? stageError = null;
                    using var stage = CancellationTokenSource.CreateLinkedTokenSource(cancelled.Token); stage.CancelAfter(TimeSpan.FromSeconds(plan.SecondsPerStage));
                    try
                    {
                    if (real)
                    {
                        var target = new D92HidRenderTarget(logger, experimentalHighFrameRate: plan.ExperimentalHighFrameRate);
                        target.StateChanged += state => states.Add(state);
                        target.FrameCompleted += timing => frames.Add(new(watch.Elapsed.TotalSeconds, lastSample, timing));
                        await target.RunAsync(plan.SerialNumber!, Render, TimeSpan.FromSeconds(1d / fps), stage.Token);
                    }
                    else
                    {
                        try
                        {
                            while (!stage.IsCancellationRequested)
                            {
                                var before = watch.Elapsed;
                                await Render(stage.Token); frames.Add(new(watch.Elapsed.TotalSeconds, lastSample, null));
                                var wait = TimeSpan.FromSeconds(1d / fps) - (watch.Elapsed - before);
                                if (wait > TimeSpan.Zero) await Task.Delay(wait, stage.Token);
                            }
                        }
                        catch (OperationCanceledException) when (stage.IsCancellationRequested) { }
                    }
                    }
                    catch (Exception error) { stageError = error.ToString(); }
                    watch.Stop();
                    measurementProcess.Refresh();
                    var summary = new
                    {
                        RequestedFps = fps, Seconds = watch.Elapsed.TotalSeconds, CompletedFrames = frames.Count,
                        CompletionFps = frames.Count / watch.Elapsed.TotalSeconds,
                        CompletionMeaning = real ? "Host completed full JPEG HID writes; physical refresh unconfirmed" : "Native sampling and JPEG only; no HID",
                        MeanJpegBytes = frames.Count == 0 ? 0 : frames.Average(f => f.Sample.JpegBytes),
                        DecodeP95 = Percentile(frames.Select(f => f.Sample.DecodeMilliseconds)),
                        EncodeP95 = Percentile(frames.Select(f => f.Sample.EncodeMilliseconds)),
                        WriteP95 = Percentile(frames.Where(f => f.Transport is not null).Select(f => f.Transport!.WriteMilliseconds)),
                        WorkingSetBytes = measurementProcess.WorkingSet64, PeakWorkingSetBytes = measurementProcess.PeakWorkingSet64,
                        CpuSeconds = (measurementProcess.TotalProcessorTime - cpuBefore).TotalSeconds,
                        ProcessAllocatedBytes=GC.GetTotalAllocatedBytes(false)-allocatedBefore,
                        ProcessAllocatedBytesPerSecond=(GC.GetTotalAllocatedBytes(false)-allocatedBefore)/watch.Elapsed.TotalSeconds,
                        ProcessGen2Collections=GC.CollectionCount(2)-gen2Before,
                        ProcessGcPauseMs=(GC.GetTotalPauseDuration()-gcPauseBefore).TotalMilliseconds,
                        AllocationScope="Whole process, including measurement records; not per-thread or isolated decoder allocations",
                        IntervalMs=VideoMeasurementStatistics.Summarize(frames.Zip(frames.Skip(1),(a,b)=>(b.AtSeconds-a.AtSeconds)*1000)),
                        DetailedStages=SummarizeStages(frames),
                        GenerationMs=VideoMeasurementStatistics.Summarize(frames.Select(f=>f.Sample.DecodeMilliseconds+f.Sample.EncodeMilliseconds)),
                        States = states, Error = stageError,
                        Failed = stageError is not null || frames.Count == 0 || states.Any(s => s.Status is "Failed" or "BlockedByCraft"),
                        Frames = frames
                    };
                    stages.Add(summary);
                    await File.WriteAllTextAsync(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { RealDevice = real, ReportLength=reportLength, Decoder = baseline.Count == 0 ? source!.Backend.ToString() : "Pre-encoded JPEG transport baseline; not video playback", PhysicalDisplayConfirmed = false, HighFrameRateValidated = false, Stages = stages }, Json));
                    Console.WriteLine($"{fps} requested FPS: {frames.Count} complete frames / {watch.Elapsed.TotalSeconds:F2}s; physical refresh unconfirmed");
                    if (summary.Failed) throw new IOException("Stage failed; inspect preserved result.json.");
                    if (cancelled.IsCancellationRequested) break;
                }
            }
            finally { Console.CancelKeyPress -= cancel; }
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
    private static double? Percentile(IEnumerable<double> values)
    { var sorted = values.Order().ToArray(); return sorted.Length == 0 ? null : sorted[(int)Math.Ceiling(sorted.Length * .95) - 1]; }

}
