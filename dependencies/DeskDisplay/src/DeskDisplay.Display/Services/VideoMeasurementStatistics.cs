namespace DeskDisplay.Display.Services;

public sealed record TimingDistribution(int Count, double? Mean, double? P50, double? P95, double? P99);
public sealed record WriteRegression(int Count, double? InterceptMs, double? MsPerReport, double? RSquared);

public static class VideoMeasurementStatistics
{
    public static TimingDistribution Summarize(IEnumerable<double> samples)
    {
        var values=samples.Where(double.IsFinite).Order().ToArray();
        double? Percentile(double p)=>values.Length==0?null:values[(int)Math.Ceiling(values.Length*p)-1];
        return new(values.Length,values.Length==0?null:values.Average(),Percentile(.5),Percentile(.95),Percentile(.99));
    }

    public static WriteRegression FitReports(IEnumerable<(int Reports,double WriteMs)> samples)
    {
        var values=samples.Where(x=>x.Reports>0&&double.IsFinite(x.WriteMs)&&x.WriteMs>=0).ToArray();
        if(values.Length<2)return new(values.Length,null,null,null);
        var x=values.Average(v=>v.Reports);var y=values.Average(v=>v.WriteMs);
        var xx=values.Sum(v=>(v.Reports-x)*(v.Reports-x));
        if(xx==0)return new(values.Length,null,null,null);
        var slope=values.Sum(v=>(v.Reports-x)*(v.WriteMs-y))/xx;var intercept=y-slope*x;
        var yy=values.Sum(v=>(v.WriteMs-y)*(v.WriteMs-y));
        var error=values.Sum(v=>Math.Pow(v.WriteMs-(intercept+slope*v.Reports),2));
        return new(values.Length,intercept,slope,yy==0?null:1-error/yy);
    }
}
