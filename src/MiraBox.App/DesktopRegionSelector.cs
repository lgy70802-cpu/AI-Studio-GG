using System.Drawing;
using System.Windows.Forms;

namespace MiraBox.App;

internal sealed class DesktopRegionSelector : Form
{
    readonly Bitmap desktop;
    readonly double ratio;
    Point anchor;
    Rectangle selection;
    bool dragging;
    bool moving;
    Point moveOffset;
    bool freeRatio;
    readonly Button confirm = new();
    public bool IncludeApp { get; private set; }
    public event Action? IncludeAppChanged;
    public Rectangle Selection => selection;

    public DesktopRegionSelector(Bitmap desktop, Rectangle bounds, double ratio)
    {
        this.desktop = desktop;
        this.ratio = ratio;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = bounds;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;
        Cursor = Cursors.Cross;
        Text = "MiraBox 桌面选区";
        confirm.Text = "确认选区";
        confirm.Bounds = new Rectangle(Math.Max(12, ClientSize.Width - 256), 24, 112, 42);
        confirm.BackColor = Color.MediumSlateBlue;
        confirm.ForeColor = Color.White;
        confirm.FlatStyle = FlatStyle.Flat;
        confirm.Font = new Font("Microsoft YaHei UI", 11);
        confirm.Enabled = false;
        confirm.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        Controls.Add(confirm);
        var cancel = new Button { Text = "取消", Bounds = new Rectangle(Math.Max(130, ClientSize.Width - 132), 24, 108, 42), BackColor = Color.White, FlatStyle = FlatStyle.Flat, Font = confirm.Font };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        Controls.Add(cancel);
        MouseDown += (_, e) =>
        {
            moving = selection.Contains(e.Location);
            moveOffset = new Point(e.X - selection.X, e.Y - selection.Y);
            anchor = e.Location;
            dragging = true;
            Capture = true;
        };
        MouseMove += (_, e) =>
        {
            if (!dragging) return;
            if (moving)
            {
                selection.Location = new Point(Math.Clamp(e.X - moveOffset.X, 0, ClientSize.Width - selection.Width),
                    Math.Clamp(e.Y - moveOffset.Y, 0, ClientSize.Height - selection.Height));
                Invalidate();
                return;
            }
            var width = Math.Abs(e.X - anchor.X);
            var height = Math.Abs(e.Y - anchor.Y);
            if (!freeRatio && (ModifierKeys & Keys.Shift) == 0)
            {
                width = Math.Min(width, (int)Math.Round(height * ratio));
                height = (int)Math.Round(width / ratio);
            }
            selection = Rectangle.Intersect(ClientRectangle, new Rectangle(
                e.X < anchor.X ? anchor.X - width : anchor.X,
                e.Y < anchor.Y ? anchor.Y - height : anchor.Y, width, height));
            Invalidate();
        };
        MouseUp += (_, _) => { dragging = false; Capture = false; confirm.Enabled = selection.Width > 1 && selection.Height > 1; };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.P) { freeRatio = !freeRatio; Invalidate(); }
            if (e.KeyCode == Keys.I) { IncludeApp = !IncludeApp; IncludeAppChanged?.Invoke(); Invalidate(); }
            if (e.KeyCode == Keys.Tab) { DialogResult = DialogResult.Retry; Close(); }
            var step = e.Shift ? 10 : 1;
            var dx = e.KeyCode == Keys.Left ? -step : e.KeyCode == Keys.Right ? step : 0;
            var dy = e.KeyCode == Keys.Up ? -step : e.KeyCode == Keys.Down ? step : 0;
            if (e.Control)
            {
                selection.Width = Math.Clamp(selection.Width + dx, 2, Math.Max(2, ClientSize.Width - selection.X));
                selection.Height = Math.Clamp(selection.Height + dy, 2, Math.Max(2, ClientSize.Height - selection.Y));
            }
            else selection.Location = new Point(Math.Clamp(selection.X + dx, 0, ClientSize.Width - selection.Width),
                Math.Clamp(selection.Y + dy, 0, ClientSize.Height - selection.Height));
            Invalidate();
            confirm.Enabled = selection.Width > 1 && selection.Height > 1;
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            if (e.KeyCode == Keys.Enter && selection.Width > 1 && selection.Height > 1)
            { DialogResult = DialogResult.OK; Close(); }
        };
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.DrawImageUnscaled(desktop, 0, 0);
        using var shade = new SolidBrush(Color.FromArgb(110, Color.Black));
        e.Graphics.FillRectangle(shade, ClientRectangle);
        if (selection.Width > 0 && selection.Height > 0)
        {
            e.Graphics.DrawImage(desktop, selection, selection, GraphicsUnit.Pixel);
            using var pen = new Pen(Color.MediumSlateBlue, 2);
            e.Graphics.DrawRectangle(pen, selection);
        }
        using var font = new Font("Microsoft YaHei UI", 12);
        using var toolbar = new SolidBrush(Color.FromArgb(225, 42, 28, 75));
        e.Graphics.FillRectangle(toolbar, 12, 12, Math.Min(ClientSize.Width - 24, 1190), 76);
        e.Graphics.DrawString($"拖动框选/移动 · Enter 确认 · Esc 取消 · P {(freeRatio ? "自由选区" : "设备比例")} · I {(IncludeApp ? "包含本软件" : "排除本软件")} · Tab 切换显示器", font, Brushes.White, 24, 24);
        e.Graphics.DrawString($"{selection.Width} × {selection.Height} px · 方向键移动 · Ctrl+方向键调整尺寸 · Shift 加速", font, Brushes.White, 24, 52);
    }

    protected override bool ProcessDialogKey(Keys keyData)
    {
        if (keyData == Keys.Tab) { DialogResult = DialogResult.Retry; Close(); return true; }
        return base.ProcessDialogKey(keyData);
    }
}
