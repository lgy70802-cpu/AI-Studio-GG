using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Win32;

namespace MiraBox.App;

public sealed partial class StudioWindow
{
    readonly SemaphoreSlim updateOperation = new(1);
    StudioUpdateService? updater;
    CancellationTokenSource? updateCancellation;
    string UpdateSourceFile => Path.Combine(dataRoot, "update-source.json");

    string ReadUpdateSource()
    {
        if (!File.Exists(UpdateSourceFile)) return "";
        return JsonNode.Parse(File.ReadAllText(UpdateSourceFile))?["url"]?.GetValue<string>() ?? "";
    }

    (string Mode, string Directory) InstalledLocation()
    {
        const string key = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{5DC279A8-9DAC-4660-B9B3-FF889E1088BB}_is1";
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        {
            using var root = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var install = root.OpenSubKey(key);
            if (install?.GetValue("InstallLocation") is not string directory) continue;
            if (Path.GetFullPath(directory).TrimEnd('\\').Equals(AppContext.BaseDirectory.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                return (hive == RegistryHive.CurrentUser ? "/CURRENTUSER" : "/ALLUSERS", directory);
        }
        throw new InvalidOperationException("请先使用安装包安装本程序，再使用覆盖更新功能。");
    }

    async Task<object> UpdateCommand(string command, JsonElement message)
    {
        if (command == "updates-status") return new { version = StudioUpdateService.CurrentVersion.ToString(3), source = ReadUpdateSource() };
        if (command == "updates-cancel") { updateCancellation?.Cancel(); return true; }
        if (!await updateOperation.WaitAsync(0)) throw new InvalidOperationException("更新操作正在进行，请稍候。");
        try
        {
            updateCancellation = CancellationTokenSource.CreateLinkedTokenSource(shutdown.Token);
            var token = updateCancellation.Token;
            var loopback = Environment.GetEnvironmentVariable("MIRABOX_DEVTOOLS_PORT") != null;
            updater ??= new(new HttpClient { Timeout = TimeSpan.FromMinutes(15) }, Path.Combine(dataRoot, "Updates", "Downloads"), loopback);
            var id = message.GetProperty("id").GetString();
            void Progress(string phase, int percent = -1) => Reply(new { id, updateProgress = new { phase, percent } });
            if (command == "updates-check")
            {
                var source = message.GetProperty("source").GetString()?.Trim() ?? "";
                if (source.Length == 0) throw new InvalidOperationException("请先设置发行方提供的 HTTPS 更新清单地址。");
                StudioUpdateService.ValidateUrl(source, loopback);
                StudioAtomicFile.Write(UpdateSourceFile, new JsonObject { ["url"] = source }.ToJsonString());
                Progress("正在检查更新…");
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                var manifest = await updater.Check(source, timeout.Token);
                return new { current = StudioUpdateService.CurrentVersion.ToString(3), available = manifest };
            }
            if (command != "updates-install") throw new InvalidOperationException("未知更新操作。");
            var install = InstalledLocation();
            var lastPercent = -1;
            var installer = await updater.Download((done, total) =>
            {
                var percent = (int)(done * 100 / total);
                if (percent != lastPercent) { lastPercent = percent; Progress("正在下载安装包…", percent); }
            }, token);
            Progress("正在备份配置和旧版本…");
            string backup;
            await commands.WaitAsync(token);
            try
            {
                if (librarySaveFailed || videoUploads.Count > 0) throw new InvalidOperationException("请先完成资源库保存和素材导入，再更新软件。");
                Save(); SaveSchedules();
                backup = Path.Combine(dataRoot, "Updates", "Backups", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(backup);
                foreach (var name in new[] { "library-workspace.json", "desktop-state.json", "schedule-state.json" })
                    if (File.Exists(Path.Combine(dataRoot, name))) File.Copy(Path.Combine(dataRoot, name), Path.Combine(backup, name));
                File.WriteAllText(Path.Combine(backup, "preferences.json"), message.GetProperty("preferences").GetRawText());
                await Task.Run(() =>
                {
                    var application = AppContext.BaseDirectory;
                    using var archive = ZipFile.Open(Path.Combine(backup, "previous-program.zip"), ZipArchiveMode.Create);
                    foreach (var file in Directory.EnumerateFiles(application, "*", SearchOption.AllDirectories))
                    {
                        token.ThrowIfCancellationRequested();
                        if (Path.GetFullPath(file).StartsWith(Path.GetFullPath(dataRoot).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) continue;
                        archive.CreateEntryFromFile(file, Path.GetRelativePath(application, file), CompressionLevel.Fastest);
                    }
                }, token);
                File.WriteAllText(Path.Combine(backup, "recovery.txt"), $"原程序目录 {install.Directory}\n原版本 {StudioUpdateService.CurrentVersion}\n安装失败时可退出程序后用 previous-program.zip 恢复程序文件。配置快照与 preferences.json 保留在此目录，素材保留在工作区。\n");
                File.WriteAllText(Path.Combine(backup, "backup.complete"), "complete");
            }
            finally { commands.Release(); }
            Progress("备份完成，正在启动安装程序…");
            token.ThrowIfCancellationRequested();
            var start = new ProcessStartInfo(installer) { UseShellExecute = true };
            foreach (var argument in new[] { install.Mode, "/SILENT", "/NORESTART", "/UPDATE=1",
                "/DIR=" + install.Directory, "/WORKSPACE=" + dataRoot, "/LOG=" + Path.Combine(backup, "install.log") })
                start.ArgumentList.Add(argument);
            var process = Process.Start(start);
            if (process == null) throw new IOException("无法启动安装程序。旧版本和备份已保留。");
            exitRequested = true;
            _ = Dispatcher.BeginInvoke(new Action(Close));
            return new { backup };
        }
        finally { updateCancellation?.Dispose(); updateCancellation = null; updateOperation.Release(); }
    }
}
