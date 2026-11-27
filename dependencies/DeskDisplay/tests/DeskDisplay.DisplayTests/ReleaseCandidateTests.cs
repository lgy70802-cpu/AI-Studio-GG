using System.IO;
using DeskDisplay.App.ViewModels;
using DeskDisplay.Core.Storage;
namespace DeskDisplay.DisplayTests;

internal static class ReleaseCandidateTests
{
    public static void Run()
    {
        Check(StudioDataPaths.ResolveRoot(null)==Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"DeskDisplay"),"Default data root retains the existing user directory exactly");
        var root=Path.Combine(Path.GetTempPath(),"D92 独立 RC");Check(StudioDataPaths.ResolveRoot(root)==Path.GetFullPath(root),"Explicit isolated data root accepts absolute Unicode paths");
        foreach(var path in new[]{"relative-profile",Path.GetPathRoot(root)!})
        {try{StudioDataPaths.ResolveRoot(path);throw new InvalidOperationException("Unsafe root accepted");}catch(ArgumentException){Check(true,"Relative and disk-root profiles are rejected before storage is created");}}
        var output=new OutputCoordinator();var reason=OutputTroubleshooting.DescribeFailure("USB 输出","HID write timed out");output.Report(reason,true);
        Check(output.Message.Contains("HID write timed out")&&output.Details.Contains(reason)&&output.Details.Contains(output.RecoveryAdvice.Hint),"Unified feedback preserves the real failure and its recovery advice together");
        Check(output.RecoveryAdvice.Destination=="Devices"&&OutputTroubleshooting.Explain("Access is denied").Destination=="Devices","Transport timeout and access errors lead to device recovery");
        Check(OutputTroubleshooting.Explain("视频文件已丢失").Destination=="Library"&&OutputTroubleshooting.Explain("设置保存失败，disk full").Destination=="Settings","Missing media and persistence failures lead to different repair destinations");
        Check(!OutputTroubleshooting.DescribeFailure("输出",new string('x',1000)+"\nnext").Contains('\n')&&OutputTroubleshooting.DescribeFailure("输出",new string('x',1000)).Length<420,"Driver failure text is bounded and does not break the compact status layout");
        Check(OutputTroubleshooting.DescribeFailure("输出",null).Contains("未取得具体原因"),"Unknown output failures remain explicit instead of inventing a cause");
    }
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);Console.WriteLine("PASS: "+message);}
}
