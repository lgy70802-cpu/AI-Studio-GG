using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MiraBox.App;
internal static class Program
{
 [STAThread] static void Main()
 {
  var root=Path.Combine(Path.GetTempPath(),"ScheduleHostChecks-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(Path.Combine(root,"Library"));
  File.WriteAllBytes(Path.Combine(root,"Library","fixture.png"),Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9WQAAAAASUVORK5CYII="));
  File.WriteAllText(Path.Combine(root,"desktop-state.json"),"""{"Selected":"QA-OFFLINE","Restore":false,"Devices":[{"Serial":"QA-OFFLINE","Model":"D69","Playing":false}]}""");
  Environment.SetEnvironmentVariable("MIRABOX_DATA_DIR",root);
var window=new StudioWindow();
  var flags=BindingFlags.NonPublic|BindingFlags.Instance;
  typeof(StudioWindow).GetField("librarySnapshot",flags)!.SetValue(window,JsonNode.Parse("""{"themes":[{"id":"image","displayName":"fixture","category":"壁纸","inLibrary":true,"imageUrl":"https://media.studio.local/fixture.png"}],"playlists":[],"schedules":[]}""")!.AsObject());
  var method=typeof(StudioWindow).GetMethod("ExecuteScheduled",flags)!;
  var rule=new StudioScheduleDefinition("offline","Offline","QA-OFFLINE",false,"12:00","daily",[],"theme","image",null,true);
  ((Task)method.Invoke(window,[rule,true,null,null])!).GetAwaiter().GetResult();
  var state=(StudioScheduleState)typeof(StudioWindow).GetField("scheduleState",flags)!.GetValue(window)!;
  if(state.History[0].Status!="跳过"||!state.History[0].Reason.Contains("离线"))throw new Exception("offline behavior");
  Console.WriteLine("PASS actual host skips offline device and persists reason");
  var missing=rule with{Id="missing",ThemeId="missing"};
  ((Task)method.Invoke(window,[missing,true,null,null])!).GetAwaiter().GetResult();
  if(!state.History[0].Reason.Contains("重新绑定"))throw new Exception("missing target repair");
  Console.WriteLine("PASS missing material recorded with repair guidance");
  var lostList=rule with{Id="list",Action="playlist",PlaylistId="missing"};
  ((Task)method.Invoke(window,[lostList,true,null,null])!).GetAwaiter().GetResult();
  if(!state.History[0].Reason.Contains("列表已删除"))throw new Exception("missing list");
  Console.WriteLine("PASS deleted list retains rule with repair reason");
  var persisted=JsonSerializer.Deserialize<StudioScheduleState>(File.ReadAllText(Path.Combine(root,"schedule-state.json")))!;
  if(persisted.History.Count!=3||File.Exists(Path.Combine(root,"desktop-output.jsonl")))throw new Exception("unexpected device operation");
  Console.WriteLine("PASS skip results survive restart without USB side effects");

  persisted.Begin(rule,DateTime.Now,false);
  File.WriteAllText(Path.Combine(root,"schedule-state.json"),JsonSerializer.Serialize(persisted));
  typeof(StudioWindow).GetMethod("LoadSchedules",flags)!.Invoke(window,null);
  var recovered=(StudioScheduleState)typeof(StudioWindow).GetField("scheduleState",flags)!.GetValue(window)!;
  if(recovered.History[0].Status!="失败"||!recovered.History[0].Reason.Contains("中断"))throw new Exception("interrupted receipt recovery");
  Console.WriteLine("PASS interrupted execution becomes a durable failure instead of replay");
  // This fixture tests host scheduling methods without starting a WebView event loop.
  // End the isolated test process after assertions; GUI shutdown is covered by the running-app tests.
  Console.Out.Flush();
  Environment.Exit(0);
 }
}
