using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Drawing.Imaging;
using System.Windows.Forms;
using ScreenRecorderLib;

namespace MiraBox.App;

internal sealed class DesktopRecording : Form
{
    [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr window, uint affinity);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window, int id);
    readonly TaskCompletionSource<double> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly System.Windows.Forms.Timer timer = new() { Interval = 200 };
    readonly Label status = new() { AutoSize = true, ForeColor = Color.White, Location = new Point(12, 14) };
    readonly Stopwatch clock = new();
    readonly Form border;
    Recorder? recorder;
    bool stopping;
    bool started;
    bool hotkey;
    readonly Button start = new();
    readonly Button stop = new();
    public Task<double> Completion => completion.Task;

    public DesktopRecording(Rectangle region, Rectangle monitor, string device, string path, string posterPath)
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = new Rectangle(Math.Clamp(region.X, monitor.Left, monitor.Right - 540),
            region.Top >= monitor.Top + 100 ? region.Top - 95 : Math.Min(region.Bottom + 5, monitor.Bottom - 90), 540, 84);
        BackColor = Color.FromArgb(42, 28, 75);
        AutoScaleMode = AutoScaleMode.None;
        Text = "MiraBox 区域录屏";
        TopMost = true;
        Font = new Font("Microsoft YaHei UI", 10);
        status.Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold);
        status.Location = new Point(16, 12);
        status.Text = "选区已就绪";
        var hint = new Label { Text = "开始后倒计时 3 秒", AutoSize = true, ForeColor = Color.Lavender, Location = new Point(16, 49) };
        Controls.Add(status);
        Controls.Add(hint);
        start.Text = "开始录制";
        start.Bounds = new Rectangle(304, 18, 108, 44);
        start.BackColor = Color.FromArgb(104, 65, 255);
        start.ForeColor = Color.White;
        start.FlatStyle = FlatStyle.Flat;
        stop.Text = "取消";
        stop.Bounds = new Rectangle(420, 18, 104, 44);
        stop.BackColor = Color.White;
        stop.FlatStyle = FlatStyle.Flat;
        Controls.Add(start);
        Controls.Add(stop);
        stop.Click += (_, _) => Stop();
        border = new RecordingBorder(region);
        Shown += (_, _) =>
        {
            hotkey = RegisterHotKey(Handle, 1, 0x4006, (uint)Keys.F10);
            border.Show();
            BringToFront();
            Activate();
        };
        start.Click += async (_, _) =>
        {
            if (started || stopping) return;
            started = true;
            start.Enabled = false;
            try
            {
                for (var n = 3; n > 0; n--)
                {
                    status.Text = $"倒计时 {n} 秒";
                    start.Text = n.ToString();
                    await Task.Delay(1000);
                    if (stopping) return;
                }
                status.Text = "正在开始录制…";
                hint.Text = hotkey ? "Ctrl+Shift+F10 结束录制" : "点击右侧按钮结束录制";
                stop.Text = "结束录制";
                start.Text = "录制中";
                var options = RecorderOptions.Default;
                options.AudioOptions.IsAudioEnabled = false;
                options.VideoEncoderOptions.Framerate = 30;
                options.VideoEncoderOptions.IsHardwareEncodingEnabled = true;
                options.VideoEncoderOptions.IsMp4FastStartEnabled = true;
                options.VideoEncoderOptions.Encoder = new H264VideoEncoder();
                options.MouseOptions.IsMousePointerEnabled = true;
                options.OutputOptions.IsVideoFramePreviewEnabled = true;
                options.OutputOptions.VideoFramePreviewSize = new ScreenSize(region.Width, region.Height);
                options.SourceOptions.RecordingSources = new List<RecordingSourceBase> {
                    new DisplayRecordingSource(device) { SourceRect = new ScreenRect(region.X - monitor.X, region.Y - monitor.Y, region.Width, region.Height) }
                };
                recorder = Recorder.CreateRecorder(options);
                var posterSaved = 0;
                recorder.OnFrameRecorded += (_, e) =>
                {
                    if (e.BitmapData == null || Interlocked.Exchange(ref posterSaved, 1) != 0) return;
                    try
                    {
                        var data = e.BitmapData;
                        using var image = new Bitmap(data.Width, data.Height, data.Stride, PixelFormat.Format32bppArgb, data.Data);
                        image.Save(posterPath, System.Drawing.Imaging.ImageFormat.Png);
                    }
                    catch (Exception ex) { completion.TrySetException(new InvalidOperationException("录屏封面保存失败。", ex)); }
                };
                recorder.OnRecordingComplete += (_, _) => completion.TrySetResult(clock.Elapsed.TotalSeconds);
                recorder.OnRecordingFailed += (_, e) => completion.TrySetException(new InvalidOperationException("录屏失败：" + e.Error));
                recorder.Record(path);
                clock.Start();
                timer.Tick += (_, _) => status.Text = $"录制中 {clock.Elapsed:mm\\:ss}";
                timer.Start();
            }
            catch (Exception ex) { completion.TrySetException(ex); }
        };
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0312) Stop();
        base.WndProc(ref m);
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!completion.Task.IsCompleted) { e.Cancel = true; Stop(); }
        base.OnFormClosing(e);
    }
    void Stop()
    {
        if (stopping) return;
        stopping = true;
        timer.Stop();
        status.Text = "正在保存…";
        if (recorder == null) completion.TrySetResult(0);
        else
        {
            recorder.Stop();
            _ = EnforceStopTimeout();
        }
    }
    async Task EnforceStopTimeout()
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        completion.TrySetException(new TimeoutException("录屏保存超时，请重试。"));
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            stopping = true;
            timer.Dispose();
            if (IsHandleCreated) UnregisterHotKey(Handle, 1);
            border.Dispose();
            recorder?.Dispose();
            recorder = null;
        }
        base.Dispose(disposing);
    }
    sealed class RecordingBorder : Form
    {
        public RecordingBorder(Rectangle bounds)
        {
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            bounds.Inflate(3, 3);
            Bounds = bounds;
            TopMost = true;
            BackColor = Color.Red;
            Opacity = 0.9;
            var shape = new Region(ClientRectangle);
            shape.Exclude(new Rectangle(3, 3, ClientSize.Width - 6, ClientSize.Height - 6));
            Region = shape;
            Shown += (_, _) => SetWindowDisplayAffinity(Handle, 0x11);
        }
        protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08080020; return p; } }
    }
}
