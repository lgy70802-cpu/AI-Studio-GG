namespace DeskDisplay.Core.Models;

public enum VideoPlaybackMode { Custom, Quality, Smooth }
public sealed record UsbVideoPreferences(int FramesPerSecond = 30, bool AutoReduceFrameRate = false, int JpegQuality = 90)
{
    public VideoPlaybackMode Mode { get; init; }
    public static bool IsValidRate(int fps) => fps is 5 or 10 or 20 or 24 or 30;
    public UsbVideoPreferences Normalize() => this with { FramesPerSecond = IsValidRate(FramesPerSecond) ? FramesPerSecond : 30, JpegQuality = JpegQuality is >= 30 and <= 95 ? JpegQuality : 90, Mode=Enum.IsDefined(Mode)?Mode:VideoPlaybackMode.Custom };
    public UsbVideoPreferences Resolve()
    {
        var value=Normalize();return value.Mode switch {
        VideoPlaybackMode.Quality=>value with {FramesPerSecond=24,AutoReduceFrameRate=false,JpegQuality=90},
        VideoPlaybackMode.Smooth=>value with {FramesPerSecond=30,AutoReduceFrameRate=true,JpegQuality=80},
        _=>value
        };
    }
}
public sealed record VideoThroughput(double ActualFps, int RequestedFps, int EffectiveFps, bool Reduced)
{public int JpegQuality {get;init;}public int JpegBytes {get;init;}public string? QualityReason {get;init;}}

/// <summary>Bounded quality adaptation; a byte target is not a guaranteed transport or FPS limit.</summary>
public sealed class VideoQualityController
{
    private readonly bool _adaptive;
    private int _samples,_small;
    public const int TargetBytes=100_000,RetryBytes=110_000;
    public bool Adaptive=>_adaptive;
    public int Quality {get;private set;}
    public string Reason {get;private set;}="固定质量";
    public VideoQualityController(UsbVideoPreferences preferences)
    {var value=preferences.Resolve();_adaptive=value.Mode==VideoPlaybackMode.Smooth;Quality=value.JpegQuality;}
    public int RetryQuality(int bytes)=>_adaptive&&bytes>RetryBytes&&Quality>60?Math.Max(60,Quality-4):Quality;
    public void Observe(int bytes,int encodedQuality,bool paused=false)
    {
        if(!_adaptive||paused)return;
        Quality=encodedQuality;_samples++;
        _small=bytes<TargetBytes*.75?_small+1:0;
        if(bytes>RetryBytes){Quality=Math.Max(60,Quality-2);Reason=encodedQuality==60?"已到最低质量，字节预算仍可能超出":"画面较复杂，降低JPEG质量";}
        else if(_samples%4==0&&bytes>TargetBytes){Quality=Math.Max(60,Quality-2);Reason="按字节预算降低JPEG质量";}
        else if(_small>=24){Quality=Math.Min(90,Quality+1);_small=0;Reason="字节预算充足，缓慢恢复画质";}
        else Reason="按字节预算控制画质";
    }
}
/// <summary>Host complete-frame throughput, not panel refresh. Stores no image frames.</summary>
public sealed class VideoRateController(int requestedFps, bool automaticReduction)
{
    private double? _start;
    private int _frames, _slowWindows, _stableWindows, _workSamples;
    private double? _started;
    private double _lastChange = double.NegativeInfinity, _workTotal;
    private static readonly int[] Steps = [5, 10, 15, 20, 24, 30];
    private readonly object _sync = new();
    public int EffectiveFps { get; private set; } = UsbVideoPreferences.IsValidRate(requestedFps) ? requestedFps : 30;
    public void Reset() { lock (_sync) { _start = _started = null; _frames = _slowWindows = _stableWindows = _workSamples = 0; _workTotal = 0; _lastChange = double.NegativeInfinity; } }
    public VideoThroughput? Observe(double seconds, bool paused = false, double? workMilliseconds = null)
    { lock (_sync) return ObserveCore(seconds, paused, workMilliseconds); }
    private VideoThroughput? ObserveCore(double seconds, bool paused, double? workMilliseconds)
    {
        if (paused) { Reset(); return null; }
        if (_start is null) { _start = _started = seconds; return null; }
        _frames++;
        if (workMilliseconds is >= 0 && double.IsFinite(workMilliseconds.Value)) { _workTotal += workMilliseconds.Value; _workSamples++; }
        var elapsed = seconds - _start.Value;
        if (elapsed < 1) return null;
        var fps = _frames / elapsed;
        var warmed = seconds - _started!.Value >= 3;
        _slowWindows = warmed && fps + 1e-6 < EffectiveFps * .8 ? _slowWindows + 1 : 0;
        var index = Array.IndexOf(Steps, EffectiveFps);
        var next = index < Steps.Length - 1 ? Math.Min(requestedFps, Steps[index + 1]) : EffectiveFps;
        var headroom = _workSamples > 0 && _workTotal / _workSamples < 1000d / next * .8;
        _stableWindows = warmed && fps >= EffectiveFps * .9 && headroom ? _stableWindows + 1 : 0;
        var reduced = automaticReduction && _slowWindows >= 5 && index > 0;
        if (reduced) { EffectiveFps = Steps[index - 1]; _slowWindows = _stableWindows = 0; _lastChange = seconds; }
        else if (automaticReduction && next > EffectiveFps && _stableWindows >= 8 && seconds - _lastChange >= 15)
        { EffectiveFps = next; _slowWindows = _stableWindows = 0; _lastChange = seconds; }
        _start = seconds; _frames = _workSamples = 0; _workTotal = 0;
        return new(fps, requestedFps, EffectiveFps, reduced);
    }
}
