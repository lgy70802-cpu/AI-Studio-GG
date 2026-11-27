using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using Forms = System.Windows.Forms;

namespace MiraBox.App;

public sealed partial class StudioWindow
{
    bool selectingDesktop;
    async Task<object> CaptureDesktop(JsonElement message)
    {
        if (selectingDesktop) throw new InvalidOperationException("请先完成当前桌面选区。");
        var width = message.GetProperty("width").GetInt32();
        var height = message.GetProperty("height").GetInt32();
        if (width <= 0 || height <= 0) throw new InvalidOperationException("画布尺寸无效。");
        selectingDesktop = true;
        var wasVisible = IsVisible;
        try
        {
            var screens = Forms.Screen.AllScreens;
            var monitorIndex = Array.IndexOf(screens, Forms.Screen.FromPoint(Forms.Cursor.Position));
            while (true)
            {
                var monitor = screens[monitorIndex % screens.Length];
                Hide();
                await Task.Delay(200);
                using var bitmap = new Bitmap(monitor.Bounds.Width, monitor.Bounds.Height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(monitor.Bounds.Location, Point.Empty, monitor.Bounds.Size);
                using var selector = new DesktopRegionSelector(bitmap, monitor.Bounds, (double)width / height);
                selector.IncludeAppChanged += async () =>
                {
                    selector.Enabled = false;
                    selector.Opacity = 0;
                    try
                    {
                        if (selector.IncludeApp) Show(); else Hide();
                        await Task.Delay(200);
                        if (selector.IsDisposed) return;
                        using var graphics = Graphics.FromImage(bitmap);
                        graphics.CopyFromScreen(monitor.Bounds.Location, Point.Empty, monitor.Bounds.Size);
                    }
                    catch (Exception ex) { Log(new { subsystem = "capture", error = ex.Message }); }
                    finally
                    {
                        if (!selector.IsDisposed) { selector.Opacity = 1; selector.Enabled = true; selector.Activate(); }
                    }
                };
                var result = selector.ShowDialog();
                if (result == Forms.DialogResult.Retry)
                {
                    monitorIndex++;
                    continue;
                }
                if (result != Forms.DialogResult.OK) return new { cancelled = true };
                var region = selector.Selection;
                var recording = message.GetProperty("command").GetString() == "capture-record";
                if (recording)
                {
                    region.Width -= region.Width % 2;
                    region.Height -= region.Height % 2;
                    var stem = "capture-" + Guid.NewGuid().ToString("N");
                    var videoPath = Path.Combine(library.MediaRoot, stem + ".mp4");
                    var posterPath = Path.Combine(library.MediaRoot, stem + ".png");
                    var absolute = new Rectangle(region.X + monitor.Bounds.X, region.Y + monitor.Bounds.Y, region.Width, region.Height);
                    try
                    {
                        using var recordingWindow = new DesktopRecording(absolute, monitor.Bounds, monitor.DeviceName, videoPath, posterPath);
                        recordingWindow.Show();
                        var duration = await recordingWindow.Completion.WaitAsync(shutdown.Token);
                        recordingWindow.Close();
                        if (duration <= 0) { File.Delete(videoPath); return new { cancelled = true }; }
                        if (!File.Exists(posterPath)) throw new InvalidOperationException("录屏没有产生有效画面，请重试。");
                        return new
                        {
                            cancelled = false,
                            src = "https://media.studio.local/" + stem + ".mp4",
                            poster = "https://media.studio.local/" + stem + ".png",
                            width = region.Width,
                            height = region.Height,
                            dur = duration,
                            name = "录屏 " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss")
                        };
                    }
                    catch { File.Delete(videoPath); File.Delete(posterPath); throw; }
                }
                using var cropped = bitmap.Clone(region, PixelFormat.Format32bppArgb);
                var filename = "capture-" + Guid.NewGuid().ToString("N") + ".png";
                cropped.Save(Path.Combine(library.MediaRoot, filename), ImageFormat.Png);
                return new
                {
                    cancelled = false,
                    src = "https://media.studio.local/" + filename,
                    width = region.Width,
                    height = region.Height,
                    name = "截图 " + DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss")
                };
            }
        }
        finally
        {
            selectingDesktop = false;
            if (wasVisible && !closing) { Show(); Activate(); }
        }
    }
}
