using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using MiraBox.App;

internal static class Program
{
    [STAThread] static int Main(string[] args)
    {
        if (args.Length != 4) { Console.Error.WriteLine("Usage: SceneRenderChecks <built Web directory> <workspace> <theme ID> <result JSON>"); return 2; }
        var sourceWeb = Path.GetFullPath(args[0]);
        var web = Path.Combine(AppContext.BaseDirectory, "Web");
        foreach (var file in Directory.EnumerateFiles(sourceWeb, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(web, Path.GetRelativePath(sourceWeb, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target, true);
        }
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var frame = new DispatcherFrame(); Exception? failure = null;
        Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
        {
            try
            {
                var workspace = Path.GetFullPath(args[1]);
                var snapshot = JsonNode.Parse(File.ReadAllText(Path.Combine(workspace, "library-workspace.json")))!;
                var theme = snapshot["themes"]!.AsArray().Single(t => t!["id"]!.GetValue<string>() == args[2])!;
                var profile = Path.Combine(Path.GetTempPath(), "MiraBoxSceneChecks-" + Guid.NewGuid().ToString("N"));
                // No remote debugging argument or external debugger connection.
                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
                var results = new List<object>();
                foreach (var model in new[] { "D69", "D92" })
                {
                    var geometry = D92StaticImage.Geometry(90, model);
                    using var renderer = new StudioThemeRenderer(geometry.Width, geometry.Height);
                    int? layoutIndex = null;
                    if (theme["editorProject"]?["layouts"] is JsonArray layouts)
                        for (var i = 0; i < layouts.Count; i++)
                            if (layouts[i]?["size"]?[0]?.GetValue<int>() == geometry.Width && layouts[i]?["size"]?[1]?.GetValue<int>() == geometry.Height) { layoutIndex = i; break; }
                    await renderer.Initialize(environment, Path.Combine(workspace, "Library"), theme, false, layoutIndex);
                    var timer = Stopwatch.StartNew(); var frames = 0; var changes = 0; string? prior = null;
                    while (timer.Elapsed.TotalSeconds < 8)
                    {
                        var bitmap = await renderer.Capture();
                        var jpeg = await renderer.Encode(bitmap, 90, model);
                        D92StaticImage.RequireTransportRaster(jpeg, model);
                        var hash = Convert.ToHexString(SHA256.HashData(jpeg));
                        if (hash != prior) changes++;
                        prior = hash; frames++;
                        await Task.Delay(100);
                    }
                    var newFrameFps = changes / timer.Elapsed.TotalSeconds;
                    if (newFrameFps < 2) throw new Exception(model + " background scene stalled or throttled without an external debugger: " + newFrameFps);
                    results.Add(new { model, seconds = timer.Elapsed.TotalSeconds, captures = frames, changedFrames = changes, newFrameFps, transportRasterVerified = true });
                    Console.WriteLine($"PASS {model} no-debugger offscreen composite: {newFrameFps:F2} new frames/s");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[3]))!);
                File.WriteAllText(args[3], JsonSerializer.Serialize(new { externalDebugger = false, usbAccess = false, results }, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception e) { failure = e; }
            finally { frame.Continue = false; }
        });
        Dispatcher.PushFrame(frame); application.Shutdown();
        if (failure == null) return 0;
        Console.Error.WriteLine(failure); return 1;
    }
}
