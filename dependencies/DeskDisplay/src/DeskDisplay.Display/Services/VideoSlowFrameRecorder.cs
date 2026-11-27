using DeskDisplay.Display.Usb;

namespace DeskDisplay.Display.Services;

public sealed record VideoSlowEvent(double AtMs,double IntervalMs,double GcPauseMs,int Gen2Delta,
    VideoSampleTiming Sample,VideoSampleTiming? PreviousSample,D92FrameTiming Transport)
{public VideoSampleTiming? PreparedLookAhead {get;init;}public bool LookAheadPending {get;init;}}
public sealed record VideoFrameDiagnostic(string Serial,Guid ThemeId,VideoSampleTiming Sample,VideoSampleTiming? PreparedLookAhead,bool LookAheadPending,D92FrameTiming Transport);
public sealed record VideoSlowSummary(long Intervals,long SlowFrames,long WithRedraw,long WithReadback,long WithGcPause,
    int Retained,int DroppedDetails,VideoSlowEvent[] Events)
{ public double SlowThresholdMs {get;init;}=50;public double ReadbackThresholdMs=>8;public double GcPauseThresholdMs=>.5;public bool IncludesPreviousGeneration=>true;public long WithPendingLookAhead {get;init;} }

/// <summary>Correlation only. Producer look-ahead means both the current and preceding generation are relevant.</summary>
public sealed class VideoSlowFrameRecorder(int capacity=300,double slowThresholdMs=50)
{
    private readonly int _capacity=capacity>=0?capacity:throw new ArgumentOutOfRangeException(nameof(capacity));
    private readonly double _threshold=double.IsFinite(slowThresholdMs)&&slowThresholdMs>0?slowThresholdMs:throw new ArgumentOutOfRangeException(nameof(slowThresholdMs));
    private readonly Queue<VideoSlowEvent> _events=new();
    private double? _previousAt;
    private double _previousPause;
    private int _previousGen2;
    private VideoSampleTiming? _previousSample;
    private long _intervals,_slow,_redraw,_readback,_gc,_pending;

    public void Observe(double atMs,VideoSampleTiming sample,D92FrameTiming transport,double totalGcPauseMs,int totalGen2,VideoSampleTiming? preparedLookAhead=null,bool lookAheadPending=false)
    {
        if(_previousAt is {} previous)
        {
            _intervals++;var interval=atMs-previous;
            if(interval>_threshold)
            {
                _slow++;var pause=Math.Max(0,totalGcPauseMs-_previousPause);
                if(sample.Stages?.Composition?.RedrawCause>OverlayRedrawCause.None||_previousSample?.Stages?.Composition?.RedrawCause>OverlayRedrawCause.None||preparedLookAhead?.Stages?.Composition?.RedrawCause>OverlayRedrawCause.None)_redraw++;
                if(sample.Stages?.Native?.ReadbackMs>8||_previousSample?.Stages?.Native?.ReadbackMs>8||preparedLookAhead?.Stages?.Native?.ReadbackMs>8)_readback++;
                if(pause>.5)_gc++;
                if(lookAheadPending)_pending++;
                if(_capacity>0){if(_events.Count==_capacity)_events.Dequeue();_events.Enqueue(new(atMs,interval,pause,Math.Max(0,totalGen2-_previousGen2),sample,_previousSample,transport){PreparedLookAhead=preparedLookAhead,LookAheadPending=lookAheadPending});}
            }
        }
        _previousAt=atMs;_previousPause=totalGcPauseMs;_previousGen2=totalGen2;_previousSample=sample;
    }
    public VideoSlowSummary Snapshot()=>new(_intervals,_slow,_redraw,_readback,_gc,_events.Count,(int)Math.Min(int.MaxValue,_slow-_events.Count),_events.ToArray()){SlowThresholdMs=_threshold,WithPendingLookAhead=_pending};
}
