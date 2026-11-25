using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskDisplay.Display.Services;

namespace MiraBox.App;

public partial class MainWindow
{
    string HardwareMediaFit => hardwareApplySerial is { } serial && hardwareOrientations.GetOptions(serial).MediaFit == "cover" ? "cover" : "contain";

    ComboBox HardwareMediaFitControl(bool applyCurrent = false)
    {
        var control = new ComboBox { ItemsSource = new[] { "完整显示", "裁切铺满" },
            SelectedIndex = HardwareMediaFit == "cover" ? 1 : 0, MinWidth = 125, Margin = new Thickness(0, 6, 8, 6) };
        System.Windows.Automation.AutomationProperties.SetName(control, "素材显示方式");
        control.ToolTip = "图片与独立视频的显示方式；主题使用编辑器中的布局。";
        control.SelectionChanged += (_, _) => {
            try {
            if (hardwareApplySerial is not { } serial) return;
            var options = hardwareOrientations.GetOptions(serial);
            hardwareOrientations.SaveOptions(serial, options with { MediaFit = control.SelectedIndex == 1 ? "cover" : "contain" });
            stoppedHardwareStill = null;
            if (!applyCurrent || hardwarePreviewEnds != null || hardwareOutput == null) return;
            var id = hardwareVideoResourceId ?? hardwareStill?.ResourceId;
            if (id == null || !state.Resources.TryGetValue(id, out var resource) || IsHardwareTheme(resource)) return;
            var paused = HardwareVideoPaused;
            if (IsHardwareVideo(resource)) { PlayHardwareVideo(resource, recordUse: false);
                if (paused && hardwareOutput is { } output) _ = RestoreMediaFitPause(output, output.RequestedSubmissionId); }
            else UseHardwareStill(resource, recordUse: false);
            SaveWorkspace();
            } catch (Exception error) { Notice.Text = "显示方式更新失败：" + error.Message; }
        };
        return control;
    }

    async Task RestoreMediaFitPause(D92StaticOutput output, Guid submission)
    {
        var wait = System.Diagnostics.Stopwatch.StartNew();
        while (!output.IsStopping && output.RequestedSubmissionId == submission && !output.VideoHasWritten && wait.Elapsed.TotalSeconds < 10)
            await Task.Delay(30);
        if (!output.IsStopping && output.RequestedSubmissionId == submission) output.SetVideoPaused(true);
    }

    Func<IVideoFrameComposition>? CreateMediaFitComposition(string? resourceId)
    {
        if (resourceId == null || !state.Resources.TryGetValue(resourceId, out var resource) ||
            IsHardwareTheme(resource) || HardwareMediaFit != "cover") return null;
        var canvas = HardwareCanvas;
        var data = JsonSerializer.SerializeToElement(new { fit = "cover" });
        return () => new EnhancedVideoComposition(data, true, canvas.Width, canvas.Height,
            () => DateTimeOffset.UnixEpoch, _ => {
                var empty = BitmapSource.Create(canvas.Width, canvas.Height, 96, 96, PixelFormats.Pbgra32,
                    null, new byte[canvas.Width * canvas.Height * 4], canvas.Width * 4);
                empty.Freeze(); return (empty, empty);
            }, false) { ReuseEncoderBitmap = true };
    }
}
