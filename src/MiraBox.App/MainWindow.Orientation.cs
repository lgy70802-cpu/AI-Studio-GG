using System.Windows;
using System.Windows.Controls;
using MiraBox.Core;
using Size = MiraBox.Core.Size;

namespace MiraBox.App;

public partial class MainWindow
{
    void RotateDevice(Device device, int rotation)
    {
        if (device.Rotation % 180 == rotation % 180) return;
        if (state.Preview is { } preview && preview.DeviceId == device.Id)
        {
            state.EndPreview(preview.Token);
            RenderOutputs();
            UpdateHomePreview();
        }

        void Finish()
        {
            if (target == device.Id)
            {
                browseRotation = rotation;
                showAll = false;
            }
            Render();
        }

        if (state.DirectionCandidate(device.Id, rotation) != null ||
            device.Content == null || !device.Running)
        {
            state.Rotate(device.Id, rotation);
            Finish();
            return;
        }

        var canvas = rotation == 0 ? device.NativeSize :
            new Size(device.NativeSize.Height, device.NativeSize.Width);
        var list = state.Playlists.FirstOrDefault(p => p.DeviceId == device.Id && p.Playing);
        var replacement = list?.Items.Select(item => state.Resources.GetValueOrDefault(item.ResourceId))
            .FirstOrDefault(resource => resource is { Local: true } && Workspace.Difference(resource, canvas) <= .05);
        var reason = list == null ? "" : $"来自正在轮播的「{list.Name}」";
        if (replacement == null)
        {
            list = null;
            var eligible = library.Query(new(Sort: "热门"))
                .Where(entry => Workspace.Difference(entry.Resource, canvas) <= .05)
                .ToArray();
            replacement = eligible.FirstOrDefault(entry => entry.Type == Entry(device.Content).Type)?.Resource;
            reason = replacement == null ? "适合新方向的热门内容" : "同类型的热门内容";
            replacement ??= eligible.FirstOrDefault()?.Resource;
        }

        var direction = rotation == 0 ? "横屏" : "竖屏";
        var panel = new StackPanel { Margin = new Thickness(20) };
        var dialog = Dialog($"切换为{direction}？", panel, 650, 500);
        panel.Children.Add(Text($"当前内容「{ResourceName(device.Content)}」不适合{direction}，直接切换会停止输出。"));
        var actions = new WrapPanel();
        actions.Children.Add(Action(rotation == 0 ? "保留竖屏" : "保留横屏", dialog.Close));
        actions.Children.Add(Action("仅切换，停止输出", () =>
        {
            state.Rotate(device.Id, rotation);
            dialog.Close();
            Finish();
        }));
        if (replacement is { } alternative)
        {
            panel.Children.Add(Thumbnail(alternative, canvas, 270, 190));
            panel.Children.Add(Text($"建议换成「{alternative.Name}」\n{reason}"));
            actions.Children.Add(Action($"切换并换成「{alternative.Name}」", () =>
            {
                state.RotateAndUse(device.Id, rotation, alternative.Id, list);
                dialog.Close();
                Finish();
            }));
        }
        else
        {
            panel.Children.Add(Text($"没有找到适合{direction}的本地内容，可以先去在线资源下载。"));
        }
        panel.Children.Add(actions);
        dialog.ShowDialog();
    }

    void UsePreview(PreviewSession preview)
    {
        if (state.Preview?.Token != preview.Token) return;
        var resource = state.Resources[preview.ResourceId];
        state.ApplyConfirmed(preview.DeviceId, resource.Id);
        state.NoteUse(resource.Id);
        Notice.Text = $"已将「{resource.Name}」应用到「{DeviceName(preview.DeviceId)}」。";
        Render();
    }
}
