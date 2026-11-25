namespace DeskDisplay.Display.Usb;

/// <summary>Bounded aggregate counters; never retains frames or user content.</summary>
public static class RenderPerformance
{
    private static readonly object Gate=new();
    private static long _frames,_bytes;
    private static double _total,_max;
    private static long _cacheHits;
    public static long CacheHits {get{lock(Gate)return _cacheHits;}}
    internal static void RecordCacheHit(){lock(Gate)_cacheHits++;}
    public static (long Frames,double AverageMilliseconds,double MaxMilliseconds,long EncodedBytes) Snapshot
    {get{lock(Gate)return(_frames,_frames==0?0:_total/_frames,_max,_bytes);}}
    internal static void Record(double milliseconds,int bytes)
    {lock(Gate){_frames++;_bytes+=bytes;_total+=milliseconds;_max=Math.Max(_max,milliseconds);}}
}
