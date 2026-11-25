using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    FrameworkElement BuildLocalVideoPreview(LibraryEntry entry, FrameworkElement poster, Window dialog, bool startImmediately)
    {
        var preview = new StackPanel { Width = Math.Max(210, poster.Width) };
        var surface = new Grid { Width = poster.Width, Height = poster.Height, Background = Brushes.Black, ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Center };
        surface.Children.Add(poster);
        preview.Children.Add(surface);
        var path = library.MediaPath(entry);
        var status = HomeText(path != null && File.Exists(path) ? "" : "本地视频文件不可用", 12, muted: true);
        status.HorizontalAlignment = HorizontalAlignment.Center;
        if (path == null || !File.Exists(path))
        {
            preview.Children.Add(status);
            return preview;
        }

        var player = new MediaElement
        {
            LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close,
            Stretch = Stretch.Uniform, Volume = 0, Visibility = Visibility.Hidden
        };
        surface.Children.Add(player);
        var bar = new WrapPanel { Margin = new Thickness(0, 7, 0, 4), HorizontalAlignment = HorizontalAlignment.Center };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        bool opened = false, playing = false, ended = false, closed = false;
        var cursor = new ProgressBar { Height = 4, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 0, 0, 5) };
        cursor.SetResourceReference(StyleProperty, "PlaylistProgress");
        AutomationProperties.SetName(cursor, "本机视频播放进度");
        var duration = entry.Duration;
        var timeline = HomeText(Timestamp(TimeSpan.Zero), 12, muted: true);
        var toggle = new Button { Content = "播放本机预览" };
        toggle.SetResourceReference(StyleProperty, "SmallButton");
        toggle.Click += (_, _) => Toggle();
        bar.Children.Add(toggle);
        bar.Children.Add(timeline);
        preview.Children.Add(bar);
        preview.Children.Add(cursor);
        preview.Children.Add(status);
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);

        timer.Tick += (_, _) => UpdateProgress();
        player.MediaOpened += (_, _) =>
        {
            if (closed) return;
            opened = true;
            if (player.NaturalDuration.HasTimeSpan) duration = player.NaturalDuration.TimeSpan;
            poster.Visibility = Visibility.Hidden;
            status.Text = "";
            UpdateProgress();
            if (playing) timer.Start();
        };
        player.MediaEnded += (_, _) =>
        {
            if (closed) return;
            playing = false;
            ended = true;
            timer.Stop();
            timeline.Text = Timestamp(duration ?? player.Position);
            cursor.Value = 1;
            toggle.Content = "重播本机预览";
        };
        player.MediaFailed += (_, _) =>
        {
            if (closed) return;
            ShowFailure();
        };
        dialog.Closed += (_, _) => { closed = true; Release(); };
        if (startImmediately)
            dialog.Loaded += (_, _) => _ = dialog.Dispatcher.InvokeAsync(Toggle, DispatcherPriority.Loaded);
        return preview;

        string Timestamp(TimeSpan position) => PlaybackTime(position) +
            (duration is { } length ? " / " + PlaybackTime(length) : "");

        void UpdateProgress()
        {
            var position = player.Position;
            timeline.Text = Timestamp(position);
            if (duration is { TotalSeconds: > 0 } length)
                cursor.Value = Math.Clamp(position.TotalSeconds / length.TotalSeconds, 0, 1);
        }

        void Toggle()
        {
            if (closed) return;
            if (playing)
            {
                player.Pause();
                playing = false;
                timer.Stop();
                if (opened) UpdateProgress();
                toggle.Content = "继续本机预览";
                return;
            }
            if (!File.Exists(path))
            {
                status.Text = "本地视频文件不可用";
                return;
            }
            if (player.Source == null)
            {
                status.Text = "正在打开本机预览…";
                player.Source = new Uri(path);
            }
            if (ended) player.Position = TimeSpan.Zero;
            ended = false;
            player.Visibility = Visibility.Visible;
            player.Play();
            playing = true;
            toggle.Content = "暂停本机预览";
            if (opened) timer.Start();
        }

        void Release()
        {
            timer.Stop();
            opened = playing = ended = false;
            player.Close();
            player.Source = null;
            player.Visibility = Visibility.Hidden;
            poster.Visibility = Visibility.Visible;
            cursor.Value = 0;
            timeline.Text = Timestamp(TimeSpan.Zero);
        }

        void ShowFailure()
        {
            Release();
            status.Text = "本机无法解码此视频";
            toggle.Content = "重试本机预览";
        }
    }
}
