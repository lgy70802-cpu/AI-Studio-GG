using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MiraBox.Core;

namespace MiraBox.App;

public partial class MainWindow
{
    ContextMenu BuildResourceMenu(Grid card, Resource resource, bool home)
    {
        var menu = new ContextMenu { PlacementTarget = card };
        menu.SetResourceReference(StyleProperty, "ResourceContextMenu");

        PopulateMenu();
        menu.Opened += (_, _) => PopulateMenu();
        return menu;

        void PopulateMenu()
        {
            menu.Items.Clear();
            var device = target == null ? null : state.Devices.GetValueOrDefault(target);
            var offline = device is { Online: false };
            var unavailable = offline ? "设备离线" : device == null ? "未选择设备" : null;
            if (resource.Local)
            {
                var targetLabel = device == null ? "当前设备" : $"「{device.Name}」";
                var loss = device == null ? null : Workspace.Difference(resource, device.Canvas);
                var fit = device != null && (loss == null || loss > .05) ? "需适配" : null;
                var useHardware = UsesHardwareApply;
                var applyTarget = useHardware ? $"「{HardwareTargetLabel}」" : targetLabel;
                var hardwareIssue = useHardware ? HardwareApplyIssue(resource) : null;
                var canApply = useHardware ? hardwareIssue == null : unavailable == null;
                var applyNote = useHardware ? hardwareIssue : unavailable ?? fit;
                Add($"应用到{applyTarget}", "M2,3 H16 V13 H2 Z M6,16 H12 M9,13 V16 M6,8 L8,10 L12,6",
                    () => Apply(resource), applyNote, canApply, primary: true);
                const string previewIcon = "M1,9 Q9,-1 17,9 Q9,19 1,9 Z M12,9 A3,3 0 1 1 6,9 A3,3 0 1 1 12,9";
                if (Entry(resource.Id).Type == "视频")
                    Add("在本机预览", previewIcon, () => ResourceDetail(resource, startVideoPreview: true));
                else
                    Add($"在{applyTarget}上预览 15 秒", previewIcon, () => Preview(resource),
                        useHardware ? (CanPreviewHardware(resource) ? null : hardwareIssue ?? "需先在实机应用壁纸或主题") : unavailable,
                        useHardware ? CanPreviewHardware(resource) : unavailable == null);
                Add("加入轮播列表", "M2,4 H15 M2,9 H10 M2,14 H10 M14,10 V16 M11,13 H17",
                    () => PickPlaylist([resource.Id]));
            }
            else
                Add(DownloadLabel(resource), "M9,2 V12 M5,8 L9,12 L13,8 M2,13 V16 H16 V13", () => Download(resource));

            Add(favorites.Contains(resource.Id) ? "取消收藏" : "收藏",
                "M9,15 L2,8 C-1,3 5,0 9,5 C13,0 19,3 16,8 Z",
                () => { library.Update(resource.Id, entry => entry with { Favorite = !entry.Favorite }); SyncLibrary(); Render(); });
            Add("查看详情", "M16,9 A7,7 0 1 1 2,9 A7,7 0 1 1 16,9 M9,8 V13 M9,5 V6",
                () => ResourceDetail(resource));
            if (!resource.Local) return;
            Add("用作主题背景", "M2,3 H16 V15 H2 Z M2,12 L7,7 L11,11 L13,9 L16,12 M11,6 H13",
                () => NewTheme(resource));
            if (!home && state.Devices.Count > 1)
                Add("其他设备…", "M1,2 H15 V12 H1 Z M10,6 H17 V16 H10 Z", () => ApplyPanel(resource));
            if (!home && Entry(resource.Id).Type is "主题" or "时钟")
                Add("编辑主题", "M3,11 L12,2 L16,6 L7,15 L2,16 Z M10,4 L14,8", () => OpenEditor(resource));
            if (page != "在线资源")
            {
                var separator = new Separator();
                separator.SetResourceReference(StyleProperty, "ResourceMenuSeparator");
                menu.Items.Add(separator);
                Add("移入回收站", "M2,5 H16 M6,5 V2 H12 V5 M4,5 L5,16 H13 L14,5 M7,8 V13 M11,8 V13",
                    () => DeleteResources([resource.Id]), destructive: true);
            }
        }

        void Add(string label, string icon, Action action, string? note = null, bool enabled = true,
            bool primary = false, bool destructive = false)
        {
            var item = new MenuItem { IsEnabled = enabled, FontWeight = primary ? FontWeights.Bold : FontWeights.Normal };
            item.SetResourceReference(StyleProperty, "ResourceMenuItem");
            if (destructive) item.Tag = "Destructive";
            AutomationProperties.SetName(item, note == null ? label : $"{label}，{note}");
            var header = new DockPanel { LastChildFill = true };
            var glyph = new Path
            {
                Data = Geometry.Parse(icon), Width = 18, Height = 18, StrokeThickness = 1.5,
                StrokeLineJoin = PenLineJoin.Round, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
            };
            glyph.SetBinding(Shape.StrokeProperty, new Binding(nameof(Foreground)) { Source = item });
            DockPanel.SetDock(glyph, Dock.Left);
            header.Children.Add(glyph);
            if (note != null)
            {
                var annotation = HomeText(note, 12, muted: true);
                var badge = new Border { Child = annotation, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                if (enabled)
                {
                    badge.Background = (Brush)FindResource("TintBrush");
                    badge.CornerRadius = new CornerRadius(9);
                    badge.Padding = new Thickness(6, 1, 6, 1);
                }
                DockPanel.SetDock(badge, Dock.Right);
                header.Children.Add(badge);
            }
            var caption = HomeText(label, bold: primary);
            caption.SetBinding(TextBlock.ForegroundProperty, new Binding(nameof(Foreground)) { Source = item });
            header.Children.Add(caption);
            item.Header = header;
            item.Click += (_, _) =>
            {
                try { action(); SaveWorkspace(); }
                catch (Exception ex) { Notice.Text = ex.Message; }
            };
            menu.Items.Add(item);
        }
    }

    void AddResourceCardActions(Grid card, Resource resource, bool home)
    {
        var menu = BuildResourceMenu(card, resource, home);
        card.ContextMenu = menu;
        card.ContextMenuOpening += (_, e) => menu.Placement = e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint;
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 7, 5),
            Visibility = Visibility.Hidden
        };
        if (home)
        {
            actions.HorizontalAlignment = HorizontalAlignment.Center;
            actions.Margin = new Thickness(0, 0, 0, 4);
            if (card.Width < 180) actions.Orientation = Orientation.Vertical;
            if (Entry(resource.Id).Type is "主题" or "时钟")
                AddHomeAction("编辑", () => OpenEditor(resource));
            AddHomeAction("+ 轮播", () => PickPlaylist([resource.Id]));
            AddHomeAction(favorites.Contains(resource.Id) ? "★ 已收藏" : "☆ 收藏", () =>
            {
                library.Update(resource.Id, item => item with { Favorite = !item.Favorite });
                SyncLibrary();
                Render();
            });
            card.Children.Add(actions);
        }
        else
        {
            var title = resource.Local ? "使用" : DownloadLabel(resource);
            var apply = HomeButton(title, () => { if (resource.Local) Apply(resource); else Download(resource); }, "SmallPrimaryButton");
            if (resource.Local && UsesHardwareApply)
            {
                apply.ToolTip = $"应用到「{HardwareTargetLabel}」，其他设备继续输出";
                AutomationProperties.SetName(apply, "应用到「" + HardwareTargetLabel + "」");
            }
            apply.Margin = new Thickness(0, 0, 5, 0);
            apply.Padding = new Thickness(10, 0, 10, 0);
            if (card.Width >= 105) actions.Children.Add(apply);
            var more = HomeButton("⋯", () => OpenMenu(PlacementMode.Bottom), "CardMoreButton");
            AutomationProperties.SetName(more, "更多操作");
            if (page == "资源库")
            {
                actions.HorizontalAlignment = HorizontalAlignment.Center;
                var delete = HomeButton("删除", () => DeleteResources([resource.Id]), "SmallButton");
                actions.Children.Insert(0, delete);
                actions.Children.Insert(1, HomeButton(favorites.Contains(resource.Id) ? "★ 已收藏" : "☆ 收藏", () =>
                {
                    library.Update(resource.Id, item => item with { Favorite = !item.Favorite });
                    SyncLibrary(); Render();
                }, "SmallButton"));
                if (Entry(resource.Id).Type is "主题" or "时钟")
                    actions.Children.Insert(2, HomeButton("编辑", () => OpenEditor(resource), "SmallButton"));
                actions.Children.Insert(actions.Children.Count - 1,
                    HomeButton("+ 轮播", () => PickPlaylist([resource.Id]), "SmallButton"));
                if (card.Width < 300) actions.Orientation = Orientation.Vertical;
            }
            else actions.Children.Add(more);
            card.Children.Add(actions);

        }

        void AddHomeAction(string label, Action action)
        {
            var button = HomeButton(label, action, "SmallButton");
            button.Margin = new Thickness(2, 1, 2, 1);
            button.Padding = new Thickness(6, 0, 6, 0);
            actions.Children.Add(button);
        }

        card.MouseEnter += (_, _) => RefreshActions(true);
        card.MouseLeave += (_, _) => RefreshActions(false);
        card.IsKeyboardFocusWithinChanged += (_, _) => RefreshActions(card.IsMouseOver);
        menu.Closed += (_, _) => RefreshActions(card.IsMouseOver);
        card.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Apps && (e.Key != Key.F10 || !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))) return;
            e.Handled = true;
            OpenMenu(PlacementMode.Bottom);
        };

        void RefreshActions(bool hovered)
        {
            actions.Visibility = hovered || card.IsKeyboardFocusWithin || menu.IsOpen
                ? Visibility.Visible : Visibility.Hidden;
        }

        void OpenMenu(PlacementMode placement)
        {
            menu.Placement = placement;
            menu.IsOpen = true;
        }
    }
}
