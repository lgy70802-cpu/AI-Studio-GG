namespace DeskDisplay.App.ViewModels;

public sealed record OutputRecoveryAdvice(string Hint,string Label,string Destination);
public static class OutputTroubleshooting
{
    public static string DescribeFailure(string operation,string? cause)
    {var detail=cause?.Replace('\r',' ').Replace('\n',' ').Trim();return operation+"失败："+(string.IsNullOrWhiteSpace(detail)?"未取得具体原因，请查看诊断日志后重试。":detail.Length>400?detail[..400]+"…":detail);}
    public static OutputRecoveryAdvice Explain(string message)
    {
        if(Contains(message,"CRAFT","Craft","占用","设备锁"))return new("请先退出CRAFT或停止占用该设备的输出，再点击重试；不会自动强退其他程序。","查看设备","Devices");
        if(Contains(message,"丢失","缺失","不存在","not found"))return new("请在资源库定位、重新关联或导入缺失素材，再重试。","检查资源","Library");
        if(Contains(message,"解码","损坏","codec","MP4"))return new("请检查视频能否正常播放，或换用可解码的MP4；原素材不会被修改。","检查视频","Library");
        if(Contains(message,"未连接","断开","未选择设备","连接已变化","不唯一","唯一匹配","DeviceUnavailable"))return new("请检查数据线、目标序列号和设备确认状态，连接原设备后重试。","查看设备","Devices");
        if(Contains(message,"超时","timed out","timeout"))return new("请检查数据线与目标设备；若是视频，请先确认素材能正常解码，再重试。","查看设备","Devices");
        if(Contains(message,"Access is denied","Access denied","访问被拒绝","共享冲突"))return new("设备可能被其他程序占用或访问受限，请先退出占用程序，确认目标设备后重试。","查看设备","Devices");
        if(Contains(message,"保存失败","写入失败","磁盘","disk"))return new("请检查数据目录是否可写及磁盘剩余空间；原内容先保留，修复后重试。","查看设置","Settings");
        return new("请查看设置中的诊断日志，按具体原因处理后重试。","查看设置","Settings");
    }
    private static bool Contains(string value,params string[] words)=>words.Any(w=>value.Contains(w,StringComparison.OrdinalIgnoreCase));
}
