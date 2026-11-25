using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using MiraBox.Core;
using Size = MiraBox.Core.Size;

namespace MiraBox.App;

public partial class MainWindow
{
    const double PortraitPreviewHeight = 540;
    const double PortraitHomeWidth = 300;
    const double PortraitHomeGap = 22;
    const double HomeSectionGap = 24;
    sealed record HomeScreen(ContentControl Preview, Border Frame, Border StatusBadge, Ellipse StatusDot,
        TextBlock Status, TextBlock ContentName, TextBlock Details, Button Edit, Button Previous,
        Button Next, Button Output, WrapPanel Warning, TextBlock WarningText);

    readonly Dictionary<string, HomeScreen> homeScreens = [];
    TextBlock? homeOutputCount;
    StackPanel? homeRecent;
    StackPanel? homePopular;
    string? homeRecommendationContent;
    bool homeRecommendationsDrawn;

    void RenderHome()
    {
        homeScreens.Clear();
        hardwareHomeCard = null;
        homeOutputCount = null;
        homeRecent = null;
        homePopular = null;
        homeRecommendationsDrawn = false;
        hardwareOverview = null;
        var devices = state.Devices.Values.ToArray();
        var physical = HardwarePlaybackSelected;
        if (!physical && devices.Length != 1 && target == null)
        {
            RenderMultiDeviceHome(devices);
            UpdateHomePreview();
            return;
        }

        var device = devices.FirstOrDefault(item => item.Id == target) ?? devices.FirstOrDefault();
        var canvas = physical ? HardwareCanvas : device!.Canvas;
        var portrait = canvas.Width < canvas.Height;
        var screen = physical ? BuildHardwareHomeCard() : HomeCard(BuildHomeScreen(device!, false));
        if (portrait) screen.Padding = new Thickness(18, 16, 18, 16);
        homePopular = new StackPanel { Margin = new Thickness(0, HomeSectionGap, 0, 0) };
        homeRecent = new StackPanel();
        if (portrait)
        {
            var columns = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            columns.ColumnDefinitions.Add(new() { Width = new GridLength(PortraitHomeWidth) });
            columns.ColumnDefinitions.Add(new() { Width = new GridLength(PortraitHomeGap) });
            columns.ColumnDefinitions.Add(new());
            screen.VerticalAlignment = VerticalAlignment.Top;
            var right = new StackPanel();

            homeRecent.Margin = new Thickness(0, 10, 0, 0);
            right.Children.Add(homeRecent);
            right.Children.Add(homePopular);
            Grid.SetColumn(right, 2);
            columns.Children.Add(screen);
            columns.Children.Add(right);
            Body.Children.Add(columns);
        }
        else
        {
            Body.Children.Add(screen);
            homeRecent.Margin = new Thickness(0, HomeSectionGap, 0, 0);
            Body.Children.Add(homeRecent);
            Body.Children.Add(homePopular);
        }
        UpdateHomePreview();
    }

    void RenderMultiDeviceHome(Device[] devices)
    {
        Body.Children.Add(HomeText("首页", 26, bold: true));
        var shortcuts = new WrapPanel();
        shortcuts.Children.Add(Action("管理设备", () => Navigate("设备管理")));
        shortcuts.Children.Add(Action("选择内容", () => Navigate("资源库")));
        Body.Children.Add(shortcuts);
        if (hardwareApplySerial != null) Body.Children.Add(BuildHardwareHomeCard());

        var summary = new UniformGrid { Columns = 3 };
        summary.Children.Add(HomeCard(Text($"已连接设备　{devices.Count(d => d.Online)} / {devices.Length} 台", 17)));
        homeOutputCount = Text("", 17);
        summary.Children.Add(HomeCard(homeOutputCount));
        summary.Children.Add(HomeCard(Text($"我的内容　{library.Entries.Count(e => e.Resource.Local)} 项", 17)));
        Body.Children.Add(summary);
        Body.Children.Add(Text("所有设备", 20));

        if (devices.Length == 0)
        {
            var empty = new StackPanel();
            empty.Children.Add(Text("连接你的第一台 MiraBox 设备", 20));
            empty.Children.Add(Text("连接后确认设备身份与方向，再选择内容。"));
            var actions = new WrapPanel();
            actions.Children.Add(Action("连接新设备", ConnectWizard));
            actions.Children.Add(Action("先浏览资源", () => Navigate("在线资源")));
            empty.Children.Add(actions);
            Body.Children.Add(HomeCard(empty));
        }
        else
        {
            var screens = new UniformGrid { Columns = devices.Length >= 3 ? 3 : 2 };
            foreach (var device in devices)
            {
                var card = HomeCard(BuildHomeScreen(device, true));
                card.Margin = new Thickness(0, 0, 12, 12);
                screens.Children.Add(card);
            }
            Body.Children.Add(screens);
        }

        var common = new WrapPanel();
        common.Children.Add(Text("常用内容", 18));
        common.Children.Add(Action("查看全部", () => Navigate("资源库")));
        Body.Children.Add(common);
        var reference = target != null && state.Devices.TryGetValue(target, out var deviceTarget)
            ? deviceTarget.Canvas : BrowseCanvas();
        var cards = new WrapPanel();
        foreach (var entry in library.Entries.Where(entry => entry.Favorite))
            cards.Children.Add(ResourceCard(entry.Resource, reference));
        Body.Children.Add(cards);
    }

    Border HomeCard(UIElement content)
    {
        var panel = new Border { Child = content, Margin = new Thickness(0), Padding = new Thickness(20) };
        panel.SetResourceReference(StyleProperty, "Panel");
        return panel;
    }

    TextBlock HomeText(string value, int size = 14, bool bold = false, bool muted = false)
    {
        var text = new TextBlock
        {
            Text = MaterialDisplayName(value), FontSize = size, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
        };
        if (muted) text.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        return text;
    }

    Button HomeButton(string title, Action action, string style)
    {
        var button = Action(title, action);
        button.SetResourceReference(StyleProperty, style);
        return button;
    }

    static DockPanel HomeHeader(FrameworkElement title, FrameworkElement actions)
    {
        var row = new DockPanel();
        DockPanel.SetDock(actions, Dock.Right);
        row.Children.Add(actions);
        row.Children.Add(title);
        return row;
    }

    StackPanel BuildHomeScreen(Device device, bool multi)
    {
        var panel = new StackPanel();
        var portrait = device.Canvas.Width < device.Canvas.Height;
        var status = HomeText("", 12, bold: true);
        var dot = new Ellipse { Width = 6, Height = 6, Margin = new Thickness(0, 0, 6, 0) };
        var badgeContent = new StackPanel { Orientation = Orientation.Horizontal };
        badgeContent.Children.Add(dot);
        badgeContent.Children.Add(status);
        var statusBadge = new Border
        {
            Child = badgeContent, CornerRadius = new CornerRadius(13), Padding = new Thickness(9, 4, 9, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        if (multi) panel.Children.Add(HomeText(device.Name, 16, bold: true));

        var previewHeading = HomeText("", 13);
        previewHeading.Inlines.Add(new Run(portrait ? "竖屏预览" : "横屏预览") { FontWeight = FontWeights.Bold });
        previewHeading.Inlines.Add(new Run($" · {device.Model} {SpecText(device.Canvas)}") { Foreground = (Brush)FindResource("MutedBrush") });
        var controls = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        if (!multi) controls.Children.Add(BuildHomeOrientation(portrait, rotation =>
        {
            RotateDevice(device, rotation);
            SaveWorkspace();
            return Task.CompletedTask;
        }));
        controls.Children.Add(statusBadge);
        if (portrait && !multi)
        {
            panel.Children.Add(previewHeading);
            controls.Margin = new Thickness(0, 8, 0, 0);
            panel.Children.Add(controls);
        }
        else panel.Children.Add(HomeHeader(previewHeading, controls));

        var preview = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center
        };
        var frame = new Border { Child = preview, CornerRadius = new CornerRadius(16), Margin = new Thickness(0, 12, 0, 12) };
        frame.SetResourceReference(Border.BackgroundProperty, "HomePreviewBrush");
        if (portrait)
        {
            var limit = multi ? 250 : PortraitPreviewHeight;
            frame.Width = PortraitCardWidth(device.Canvas, limit);
            frame.HorizontalAlignment = multi ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        }
        frame.SizeChanged += (_, change) => { if (change.WidthChanged && frame.IsLoaded) UpdateHomePreview(); };
        frame.Loaded += (_, _) => UpdateHomePreview();
        panel.Children.Add(portrait && !multi ? WrapPortraitHomePreview(frame) : frame);

        var contentName = HomeText("", 16, bold: true);
        var details = HomeText("", 13, muted: true);
        var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(contentName);
        labels.Children.Add(details);
        var actions = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        if (multi)
        {
            actions.Children.Add(HomeButton("设置", () => OpenDevicePage(device, "设备管理"), "SmallButton"));
            actions.Children.Add(HomeButton("更换内容", () => BrowseHomeDevice(device), "SmallButton"));
        }

        var edit = HomeButton("编辑", () =>
        {
            var id = state.VisibleContent(device.Id) ?? device.Content;
            if (id != null && state.Resources.TryGetValue(id, out var resource))
            {
                Target.SelectedValue = device.Id;
                OpenEditor(resource);
            }
        }, "SmallButton");
        var previous = HomeButton("上一项", () => StepHomePlaylist(device, -1), "SmallButton");
        var next = HomeButton("下一项", () => StepHomePlaylist(device, 1), "SmallButton");
        if (!multi)
        {
            actions.Children.Add(edit);
            actions.Children.Add(previous);
            actions.Children.Add(next);
        }
        var output = HomeButton("", () => ToggleDeviceOutput(device), "SmallButton");
        actions.Children.Add(output);
        if (portrait || multi)
        {
            panel.Children.Add(labels);
            panel.Children.Add(actions);
        }
        else panel.Children.Add(HomeHeader(labels, actions));
        if (!multi && !device.Online)
        {
            panel.Children.Add(HomeText("设备已断开，内容和设置仍然保留。", 13, muted: true));
            panel.Children.Add(HomeButton("模拟重新连接", () => ChangeDeviceConnection(device, true), "SmallButton"));
        }

        var warning = new WrapPanel { Background = ColorBrush("#FFF4D8") };
        var warningText = Text("");
        warning.Children.Add(warningText);
        warning.Children.Add(HomeButton("去资源库", () => BrowseHomeDevice(device), "SmallButton"));
        if (!multi) panel.Children.Add(warning);
        homeScreens.Add(device.Id, new(preview, frame, statusBadge, dot, status, contentName, details, edit, previous, next,
            output, warning, warningText));
        return panel;
    }

    Border BuildHomeOrientation(bool portrait, Func<int, Task> changeDirection, string? portraitUnavailable = null)
    {
        var landscapeOption = CreateOption("横屏", !portrait);
        var portraitOption = CreateOption("竖屏", portrait);
        if (portraitUnavailable != null && !portrait)
        {
            portraitOption.IsEnabled = false;
            portraitOption.ToolTip = portraitUnavailable;
            ToolTipService.SetShowOnDisabled(portraitOption, true);
        }
        var segments = new StackPanel { Orientation = Orientation.Horizontal };
        segments.Children.Add(landscapeOption);
        segments.Children.Add(portraitOption);
        var group = new Border
        {
            Child = segments, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1),
            Padding = new Thickness(1), Margin = new Thickness(0, 0, 8, 0), Background = Brushes.White
        };
        group.SetResourceReference(Border.BorderBrushProperty, "SegmentLineBrush");
        group.ToolTip = "切换当前设备的输出方向";
        landscapeOption.Click += async (_, _) => await SelectDirection(0);
        portraitOption.Click += async (_, _) => await SelectDirection(90);
        return group;

        RadioButton CreateOption(string title, bool selected)
        {
            var option = new RadioButton { Content = title, GroupName = "HomeOrientation", IsChecked = selected, FontSize = 13 };
            option.SetResourceReference(StyleProperty, "OrientationSegment");
            return option;
        }

        async Task SelectDirection(int rotation)
        {
            try
            {
                group.IsEnabled = false;
                await changeDirection(rotation);
            }
            catch (Exception ex) { Notice.Text = ex.Message; }
            finally
            {
                var selectedPortrait = HardwarePlaybackSelected
                    ? HardwareCanvas.Width < HardwareCanvas.Height
                    : target != null && state.Devices.TryGetValue(target, out var selectedDevice)
                        ? selectedDevice.Canvas.Width < selectedDevice.Canvas.Height : portrait;
                landscapeOption.IsChecked = !selectedPortrait;
                portraitOption.IsChecked = selectedPortrait;
                group.IsEnabled = true;
            }
        }
    }

    void OpenDevicePage(Device? device, string name)
    {
        if (device != null) Target.SelectedValue = device.Id;
        Navigate(name);
    }

    void BrowseHomeDevice(Device? device)
    {
        OpenDevicePage(device, "资源库");
        var rotation = HardwarePlaybackSelected ? HardwareBrowseRotation : device?.Rotation ?? DefaultBrowseRotation();
        if (browseRotation % 180 == rotation % 180 && !showAll) return;
        browseRotation = rotation;
        showAll = false;
        Render();
    }

    Playlist? HomePlaying(Device device) => state.Playlists.FirstOrDefault(p =>
        p.DeviceId == device.Id && p.Playing && p.Items.Count > 0);

    void StepHomePlaylist(Device device, int direction)
    {
        if (HomePlaying(device) is not { } playlist) return;
        state.Step(playlist, direction);
        Render();
    }

    FrameworkElement BuildHomePlaylists(Device? device, string direction)
    {
        var useHardware = HardwarePlaybackSelected;
        var lists = state.Playlists.Where(p => p.DeviceId == (useHardware ?
            HardwareSchedulePrefix + hardwareApplySerial : device?.Id ?? "") ||
            useHardware && p.DeviceId == (device?.Id ?? "")).ToArray();
        if (lists.Length == 0)
        {
            var create = HomeButton("新建轮播", () =>
            {
                OpenDevicePage(device, "轮播列表");
                CreateList(AddContents);
            }, "SmallPrimaryButton");
            create.Margin = new Thickness(0);
            var label = HomeText("轮播列表", 18, bold: true);
            var message = HomeText("还没有轮播列表", 13, muted: true);
            message.HorizontalAlignment = HorizontalAlignment.Center;
            message.TextTrimming = TextTrimming.CharacterEllipsis;
            message.TextWrapping = TextWrapping.NoWrap;
            message.Margin = new Thickness(8, 0, 8, 0);
            var empty = new DockPanel { Height = 38 };
            DockPanel.SetDock(label, Dock.Left);
            DockPanel.SetDock(create, Dock.Right);
            empty.Children.Add(label);
            empty.Children.Add(create);
            empty.Children.Add(message);
            return empty;
        }

        var manage = HomeButton("管理 →", () =>
        {
            selectedList = (useHardware ? lists.FirstOrDefault(IsHardwarePlaylist) :
                device != null ? HomePlaying(device) : null)?.Id ?? lists[0].Id;
            OpenDevicePage(device, "轮播列表");
        }, "LinkButton");
        var row = new WrapPanel();
        System.Windows.Automation.AutomationProperties.SetName(row, "轮播列表");
        if (direction == "竖屏")
        {
            var label = HomeText("轮播列表", 12, muted: true);
            label.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(label);
        }
        foreach (var list in lists)
        {
            var compatible = useHardware ? HardwarePlaylistItems(list).Length :
                list.Items.Count(item => state.Resources.TryGetValue(item.ResourceId, out var resource) &&
                    resource.Local && Workspace.Difference(resource, device!.Canvas) <= .05);
            var playing = useHardware ? IsHardwarePlaylist(list) : list.Playing;
            var online = useHardware || device is { Online: true };
            var mood = MoodIcon(list.Mood);
            var button = HomeButton($"{mood}  {list.Name}  ·  {compatible} 项{(playing ? " · 轮播中" : "")}", () =>
            {
                selectedList = list.Id;
                if (useHardware) ToggleHardwarePlaylist(list);
                else if (list.Playing) list.Pause();
                else state.Start(list);
                Render();
            }, "SmallButton");
            button.IsEnabled = playing || online && compatible > 0;
            ToolTipService.SetShowOnDisabled(button, true);
            button.ToolTip = playing ? "暂停轮播，保留当前内容" :
                !online ? "设备未连接" : compatible == 0 ? $"没有适合{direction}的内容" : list.Mood;
            if (playing)
            {
                button.SetResourceReference(Control.BorderBrushProperty, "SearchFocusBrush");
                button.SetResourceReference(Control.BackgroundProperty, "HoverBrush");
                button.FontWeight = FontWeights.Bold;
            }
            row.Children.Add(button);
        }
        row.Children.Add(manage);
        return row;
    }

    static double PortraitCardWidth(Size canvas, double height = PortraitPreviewHeight) =>
        height * canvas.Width / canvas.Height;

    Border WrapPortraitHomePreview(Border frame)
    {
        var stage = new Border
        {
            Child = frame, CornerRadius = new CornerRadius(16),
            Margin = frame.Margin, Padding = new Thickness(0, 12, 0, 12)
        };
        frame.Margin = new Thickness(0);
        stage.SetResourceReference(Border.BackgroundProperty, "HomePreviewBrush");
        return stage;
    }

    void UpdateHomePreview()
    {
        if (backgroundHardwareContext) return;
        if (page != "首页") return;
        UpdateHardwareHomeCard();
        if (homeOutputCount != null)
            homeOutputCount.Text = $"正在显示　{state.Devices.Values.Count(d => d.Online && (d.Running || state.Preview?.DeviceId == d.Id))} 台";
        foreach (var (id, screen) in homeScreens)
        {
            var device = state.Devices[id];
            var visibleId = state.VisibleContent(id) ?? device.Content;
            var fit = state.VisibleFit(id);
            var previewing = state.Preview?.DeviceId == id;
            var listing = HomePlaying(device);
            var entry = library.Entries.FirstOrDefault(e => e.Resource.Id == visibleId);
            var direction = device.Canvas.Width < device.Canvas.Height ? "竖屏" : "横屏";
            var status = !device.Online ? "设备未连接" : previewing ? "临时预览" :
                entry == null ? "未选择内容" : device.Running ? "正在显示" : "输出已停止";
            if (screen.Status.Text != status)
            {
                screen.Status.Text = status;
                screen.StatusBadge.SetResourceReference(Border.BackgroundProperty, status == "正在显示" ? "RunningBackgroundBrush" : "TintBrush");
                var ink = status == "正在显示" ? "RunningInkBrush" : "MutedBrush";
                screen.Status.SetResourceReference(TextBlock.ForegroundProperty, ink);
                screen.StatusDot.SetResourceReference(Shape.FillProperty, ink);
            }
            screen.ContentName.Text = MaterialDisplayName(entry?.Resource.Name ?? "未选择内容");
            var mode = fit == "cover" ? "裁切铺满" : "完整显示";
            var details = entry == null ? "" : $"{entry.Type} · {mode}";
            if (listing != null && !previewing)
                details = $"来自轮播列表「{listing.Name}」· {listing.Index + 1}/{listing.Items.Count} · 剩余 {Math.Ceiling(listing.Remaining.TotalSeconds)} 秒";
            if (!device.Online) details += entry == null ? "重新连接后可选择内容" : " · 正在查看上次内容";
            else if (entry != null && !device.Running && !previewing)
                details += " · 已停止输出，预览保留上次内容";
            screen.Details.Text = details;
            screen.Edit.Visibility = entry is { Resource.Local: true, Type: "主题" or "时钟" }
                ? Visibility.Visible : Visibility.Collapsed;
            screen.Previous.Visibility = screen.Next.Visibility = listing == null ? Visibility.Collapsed : Visibility.Visible;
            screen.Previous.IsEnabled = screen.Next.IsEnabled = device.Online;
            screen.Output.Content = device.Running ? "停止输出" : "恢复显示";
            screen.Output.IsEnabled = device.Content != null;
            var loss = entry == null ? null : Workspace.Difference(entry.Resource, device.Canvas);
            var mismatch = device.Online && visibleId != null && (loss == null || loss > .05);
            screen.Warning.Visibility = mismatch ? Visibility.Visible : Visibility.Collapsed;
            screen.WarningText.Text = mismatch ? $"当前内容「{ResourceName(visibleId)}」不适合{direction}。" : "";

            var width = screen.Frame.ActualWidth;
            if (width <= 0) continue;
            var height = width * device.Canvas.Height / device.Canvas.Width;
            var liveClock = device.Online && (device.Running || previewing);
            var previewKey = (visibleId, fit, device.Canvas, width, liveClock);
            if (Equals(screen.Preview.Tag, previewKey)) continue;
            screen.Preview.Tag = previewKey;
            screen.Frame.Height = height;
            screen.Preview.Content = entry == null ? HomeText("未选择内容", 14, muted: true) :
                Thumbnail(entry.Resource, device.Canvas, width, height, fit, liveClock: liveClock);
            screen.Preview.Clip = new RectangleGeometry(new Rect(0, 0, width, height), 16, 16);
        }
        if (target != null && state.Devices.TryGetValue(target, out var active))
            UpdateHomeRecommendations(active);
        else if (HardwarePlaybackSelected)
            UpdateHomeRecommendations(null);
    }

    void UpdateHomeRecommendations(Device? device)
    {
        if (homeRecent == null || homePopular == null) return;
        var visibleId = HardwarePlaybackSelected
            ? hardwareVideoResourceId ?? hardwareStill?.ResourceId ?? pendingHardwareRestore?.VideoResourceId ??
                pendingHardwareRestore?.BaseResourceId
            : state.VisibleContent(device!.Id) ?? device.Content;
        if (homeRecommendationsDrawn && homeRecommendationContent == visibleId) return;
        homeRecommendationsDrawn = true;
        homeRecommendationContent = visibleId;
        homeRecent.Children.Clear();
        homePopular.Children.Clear();

        var canvas = HardwarePlaybackSelected ? HardwareCanvas : device!.Canvas;
        var portrait = canvas.Width < canvas.Height;
        var direction = portrait ? "竖屏" : "横屏";
        bool Fits(Resource resource) => resource.Id != visibleId && resource.Local &&
            Workspace.Difference(resource, canvas) <= .05;
        var recentIds = library.Entries.Where(entry => entry.Type is "壁纸" or "视频" or "主题" or "时钟")
            .Select(entry => entry.Resource.Id).ToHashSet();
        var recentCandidates = state.Recent.Where(use => recentIds.Contains(use.ResourceId))
            .Select(use => state.Resources.GetValueOrDefault(use.ResourceId))
            .OfType<Resource>().Where(Fits);
        var popularCandidates = library.Query(new(Sort: "热门")).Select(entry => entry.Resource).Where(Fits).ToArray();
        var recent = (portrait ? recentCandidates : recentCandidates.Take(6)).ToArray();
        var excludedCount = -1;
        var recentCards = new HomeCardGrid(recent.Length, portrait ? PortraitCardWidth(canvas) : null,
            (index, width) => ResourceCard(recent[index], canvas, homeWidth: width),
            width => HomeRecentPlaceholder(width, width * canvas.Height / canvas.Width));

        AddHeading(homeRecent, "最近使用", "查看更多记录 →", ShowHistory);
        homeRecent.Children.Add(recentCards);
        if (portrait)
            recentCards.SizeChanged += (_, _) => UpdatePopular(recentCards.VisibleResourceCount);
        UpdatePopular(portrait ? recentCards.VisibleResourceCount : recent.Length);

        void UpdatePopular(int visibleRecentCount)
        {
            if (visibleRecentCount == excludedCount || !ReferenceEquals(recentCards.Parent, homeRecent)) return;
            excludedCount = visibleRecentCount;
            var recentKeys = recent.Take(visibleRecentCount).Select(resource => resource.Id).ToHashSet();
            var candidates = popularCandidates.Where(resource => !recentKeys.Contains(resource.Id));
            var popular = (portrait ? candidates : candidates.Take(12)).ToArray();

            homePopular.Children.Clear();
            AddHeading(homePopular, $"适合{direction}的热门内容", "查看资源库 →", () => OpenDevicePage(device, "资源库"));
            if (popular.Length > 0)
            {
                homePopular.Children.Add(new HomeCardGrid(popular.Length, portrait ? PortraitCardWidth(canvas) : null,
                    (index, width) => ResourceCard(popular[index], canvas, homeWidth: width)));
                return;
            }

            homePopular.Children.Add(HomeText("暂时没有适合的内容。", 13, muted: true));
            var alternatives = new WrapPanel();
            alternatives.Children.Add(Action("去在线资源", () => OpenDevicePage(device, "在线资源")));
            alternatives.Children.Add(Action("去创作中心", () => Navigate("创作中心")));
            homePopular.Children.Add(alternatives);
        }

        void AddHeading(Panel section, string title, string label, Action action)
        {
            var heading = HomeHeader(HomeText(title, 18, bold: true), HomeButton(label, action, "LinkButton"));
            heading.Margin = new Thickness(0, 0, 0, 12);
            section.Children.Add(heading);
        }
    }
}
