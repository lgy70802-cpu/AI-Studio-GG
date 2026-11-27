using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MiraBox.Core;
using Size=MiraBox.Core.Size;
namespace MiraBox.App;
public partial class MainWindow
{
    sealed record ManagedDeviceOutput(string Id, ContentControl Preview, TextBlock Status, Button Toggle);
    ManagedDeviceOutput? managedDeviceOutput;

    string DeviceName(string id)=>state.Devices.TryGetValue(id,out var d)?d.Name:id;
    void ChangeDeviceConnection(Device device, bool online)
    {
        var result = state.SetOnline(device.Id, online);
        Render();
        Notice.Text = result?.Message ?? $"「{device.Name}」已断开，内容和设置已保留。";
    }

    void ToggleDeviceOutput(Device device)
    {
        if (device.Content == null) return;
        state.ToggleOutput(device.Id);
        Render();
        if (!device.Online) Notice.Text = device.Running ? "设备离线，重连后恢复显示。" : "设备离线，已保存停止状态。";
    }
    void Identify(Device d,FrameworkElement preview){if(!d.Online){Notice.Text="设备离线，无法识别。";return;}preview.BeginAnimation(OpacityProperty,new DoubleAnimation(1,.15,TimeSpan.FromMilliseconds(250)){AutoReverse=true,RepeatBehavior=new RepeatBehavior(4)});Notice.Text="正在闪烁「"+d.Name+"」的模拟预览，未发送真实设备命令。";}
    void RenderDevices()
    {
        managedDeviceOutput = null;
        if (!IsPlaylistDemo)
        {
            var title = new StackPanel();
            title.Children.Add(HomeText("设备管理", 26, bold: true));
            title.Children.Add(HomeText("每台设备独立命名、设置方向，互不影响。", 13, muted: true));
            var actions = new WrapPanel();
            actions.Children.Add(HomeButton("刷新", () => _ = RefreshManagedHardwareDevices(), "PillButton"));
            actions.Children.Add(HomeButton("＋ 连接新设备", ConnectWizard, "PrimaryButton"));
            var heading = HomeHeader(title, actions);
            heading.Margin = new Thickness(0, 0, 0, 20);
            Body.Children.Add(heading);
            Body.Children.Add(BuildManagedHardwareDevices());
            return;
        }
        Body.Children.Add(HomeText("设备管理", 26, bold: true));
        Body.Children.Add(HomeText("每台设备独立命名和设置方向。", 13, muted: true));
        var tools = new WrapPanel { Margin = new Thickness(0, 8, 0, 14) };
        tools.Children.Add(HomeButton("刷新", () => _ = RefreshManagedHardwareDevices(), "PillButton"));
        tools.Children.Add(HomeButton("＋ 连接新设备", ConnectWizard, "PrimaryButton"));
        if (IsPlaylistDemo) tools.Children.Add(HomeButton("添加模拟设备", ConnectSimulatedWizard, "LinkButton"));
        tools.Children.Add(HomeButton("USB 屏幕测试", ShowHardwarePilot, "LinkButton"));
        Body.Children.Add(tools);
        Body.Children.Add(BuildManagedHardwareDevices());
        if (state.Devices.Count == 0)
        {
            return;
        }
        Body.Children.Add(HomeText("模拟设备", 16, bold: true));
        var inspectedDevice = detailDeviceId != null && state.Devices.TryGetValue(detailDeviceId, out var inspected)
            ? inspected : state.Devices.Values.First();
        var details = new StackPanel();
        if (state.Devices.Count == 1) Body.Children.Add(details);
        else
        {
            var layout = new Grid();
            layout.ColumnDefinitions.Add(new());
            layout.ColumnDefinitions.Add(new());
            layout.ColumnDefinitions.Add(new());
            layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var devices = new StackPanel();
            var chooser = new ScrollViewer { Content = devices, Focusable = false, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            AutomationProperties.SetName(chooser, "设备列表");
            foreach (var device in state.Devices.Values)
            {
                var item = new StackPanel();
                var name = HomeText(device.Name, 15, bold: true);
                name.TextWrapping = TextWrapping.NoWrap;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                item.Children.Add(name);
                item.Children.Add(HomeText($"{device.Model} · {SpecText(device.Canvas)}", 12, muted: true));
                var status = DeviceConnectionBadge(device);
                status.Margin = new Thickness(0, 5, 0, 0);
                item.Children.Add(status);
                var button = HomeButton("", () => { detailDeviceId = device.Id; Render(); }, "SidebarCardChoice");
                button.Width = 206;
                button.Content = item;
                button.Tag = device.Id == inspectedDevice.Id ? "Selected" : null;
                button.Margin = new Thickness(2, 2, 10, 8);
                AutomationProperties.SetName(button, $"{device.Name}，{device.Model}，{SpecText(device.Canvas)}，{DeviceConnectionText(device)}");
                devices.Children.Add(button);
            }
            layout.Children.Add(chooser);
            layout.Children.Add(details);
            ArrangeDevices(ActualWidth > 0 ? ActualWidth - 268 : Width - 268);
            layout.SizeChanged += (_, change) =>
            {
                if (change.WidthChanged) ArrangeDevices(change.NewSize.Width);
            };
            Body.Children.Add(layout);

            void ArrangeDevices(double width)
            {
                var wide = width >= 680;
                layout.ColumnDefinitions[0].Width = wide ? new GridLength(218) : new GridLength(0);
                layout.ColumnDefinitions[1].Width = wide ? new GridLength(12) : new GridLength(0);
                devices.Orientation = wide ? Orientation.Vertical : Orientation.Horizontal;
                chooser.HorizontalScrollBarVisibility = wide ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
                Grid.SetColumnSpan(chooser, wide ? 1 : 3);
                Grid.SetColumn(details, 2);
                Grid.SetRow(details, wide ? 0 : 1);
                details.Margin = wide ? new Thickness(0) : new Thickness(0, 12, 0, 0);
            }
        }
        RenderDeviceDetails(details, inspectedDevice);
    }

    static string DeviceConnectionText(Device device) => device.Online ? "已连接" : "离线";

    Border DeviceConnectionBadge(Device device)
    {
        var badge = PlaylistBadge(DeviceConnectionText(device), device.Online ? "#E2F7EF" : "#EEF3F6",
            device.Online ? "#067647" : "#53677C");
        badge.HorizontalAlignment = HorizontalAlignment.Left;
        return badge;
    }

    void RenderDeviceDetails(Panel details, Device device)
    {
        var form = new StackPanel();
        form.Children.Add(HomeHeader(HomeText(device.Name, 22, bold: true), DeviceConnectionBadge(device)));
        form.Children.Add(HomeText($"{device.Model} · 设备身份 {device.Id}", 12, muted: true));
        var preview = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center };
        var frame = new Border { Child = preview, MinHeight = 155, Padding = new Thickness(12),
            Background = (Brush)FindResource("TintBrush"), CornerRadius = new CornerRadius(18), Margin = new Thickness(0, 12, 0, 12) };
        form.Children.Add(frame);
        var fields = new Grid();
        fields.ColumnDefinitions.Add(new());
        fields.ColumnDefinitions.Add(new());
        fields.RowDefinitions.Add(new() { Height = GridLength.Auto });
        fields.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var name = new TextBox { Text = device.Name, MaxLength = 30, Height = 36, Margin = new Thickness(4) };
        name.SetResourceReference(StyleProperty, "PlaylistSeconds");
        var rotations = new[] { "横屏 · 0°", "竖屏 · 90°", "横屏翻转 · 180°", "竖屏翻转 · 270°" };
        var direction = new ComboBox { ItemsSource = rotations, SelectedIndex = device.Rotation / 90, Margin = new Thickness(4) };
        AddField("设备名称", name, 0, 0);
        AddField("显示方向", direction, 0, 1);
        AddField("原始输出尺寸", HomeText(SpecText(device.NativeSize)), 1, 0);
        AddField("当前连接", HomeText("USB · 模拟"), 1, 1);
        form.Children.Add(fields);
        if (state.Devices.Values.Count(item => item.Model == device.Model) > 1)
            form.Children.Add(HomeText("同型号设备按稳定身份区分，调整名称或方向只影响当前设备。", 13, muted: true));
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        if (state.Devices.Count > 1)
        {
            var identify = HomeButton("识别这台设备", () => Identify(device, preview), "PillButton");
            identify.IsEnabled = device.Online;
            actions.Children.Add(identify);
        }
        actions.Children.Add(HomeButton(device.Online ? "模拟断开" : "模拟重连",
            () => ChangeDeviceConnection(device, !device.Online), "PillButton"));
        actions.Children.Add(HomeButton("保存设备设置", () =>
        {
            var rotation = direction.SelectedIndex * 90;
            var changed = rotation != device.Rotation;
            state.ConfigureDevice(device.Id, name.Text, rotation);
            RefreshTargets(target, keepContext: true);
            SaveWorkspace();
            Render();
            Notice.Text = changed ? "方向已更新，请重新预览并应用内容。" : "设备名称已保存。";
        }, "PrimaryButton"));
        form.Children.Add(actions);
        details.Children.Add(HomeCard(form));

        var playbackPanel = new StackPanel();
        playbackPanel.Children.Add(HomeText("独立播放", 17, bold: true));
        var outputStatus = HomeText("", 13, muted: true);
        outputStatus.Margin = new Thickness(0, 4, 0, 0);
        playbackPanel.Children.Add(outputStatus);
        var playback = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        var change = HomeButton("更换内容", () => BrowseHomeDevice(device), "PillButton");
        var playlists = HomeButton("设置轮播列表", () => OpenManagedDevicePlaylist(device), "PillButton");
        if (HardwarePlaybackSelected)
        {
            change.IsEnabled = playlists.IsEnabled = false;
            foreach (var button in new[] { change, playlists })
            {
                button.ToolTip = "切换到模拟输出后可修改此模拟设备。";
                ToolTipService.SetShowOnDisabled(button, true);
            }
        }
        playback.Children.Add(change);
        playback.Children.Add(playlists);
        var toggle = HomeButton("", () => ToggleDeviceOutput(device), "PillButton");
        playback.Children.Add(toggle);
        playbackPanel.Children.Add(playback);
        var playbackCard = HomeCard(playbackPanel);
        playbackCard.Margin = new Thickness(0, 14, 0, 0);
        details.Children.Add(playbackCard);
        managedDeviceOutput = new(device.Id, preview, outputStatus, toggle);
        UpdateManagedDeviceOutput();

        var advanced = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        advanced.Children.Add(HomeText("视频模式", 13, muted: true));
        advanced.Children.Add(Choose(["画质优先", "流畅优先"], "画质优先", _ => { }));
        advanced.Children.Add(HomeText("物理屏关联组", 13, muted: true));
        var group = new TextBox { Margin = new Thickness(4) };
        AutomationProperties.SetName(group, "物理屏关联组");
        advanced.Children.Add(group);
        advanced.Children.Add(HomeText("此处用于演示视频模式与物理屏关联设置。", 13, muted: true));
        advanced.Children.Add(HomeButton("确认演示设置", () => Notice.Text = "高级设置已在本次演示中确认。", "PillButton"));
        details.Children.Add(new Expander { Header = "高级设置 · 输出性能与物理屏关联", Content = advanced,
            Margin = new Thickness(12, 14, 12, 0) });

        void AddField(string label, FrameworkElement control, int row, int column)
        {
            var field = new StackPanel { Margin = new Thickness(0, 0, 10, 9) };
            field.Children.Add(HomeText(label, 12, muted: true));
            field.Children.Add(control);
            AutomationProperties.SetName(control, label);
            Grid.SetRow(field, row);
            Grid.SetColumn(field, column);
            fields.Children.Add(field);
        }
    }

    void OpenManagedDevicePlaylist(Device device)
    {
        var list = state.Playlists.Where(item => item.DeviceId == device.Id)
            .OrderByDescending(item => item.Id == selectedList).ThenByDescending(item => item.Playing)
            .ThenByDescending(item => item.LastUsed).FirstOrDefault();
        if (list == null)
        {
            CreateList(_ => OpenDevicePage(device, "轮播列表"), device.Id);
            return;
        }
        selectedList = list.Id;
        OpenDevicePage(device, "轮播列表");
    }

    void UpdateManagedDeviceOutput()
    {
        UpdateHardwareSettingsPreview();
        if (page != "设备管理" || managedDeviceOutput is not { } view ||
            !state.Devices.TryGetValue(view.Id, out var device)) return;
        var visible = state.VisibleContent(device.Id) ?? device.Content;
        var previewing = state.Preview?.DeviceId == device.Id;
        var running = device.Online && (device.Running || previewing);
        var fit = state.VisibleFit(device.Id);
        var mode = !device.Online ? "离线" : previewing ? "临时预览" : running ? "正在显示" : "已停止";
        view.Status.Text = visible == null ? "未选择内容" : $"{mode} · {ResourceName(visible)}";
        view.Toggle.Content = device.Running ? "停止输出" : "恢复显示";
        view.Toggle.IsEnabled = device.Content != null;
        var key = (visible, running, device.Canvas, fit);
        if (Equals(view.Preview.Tag, key)) return;
        view.Preview.Tag = key;
        if (visible != null && state.Resources.TryGetValue(visible, out var resource))
        {
            var image = Thumbnail(resource, device.Canvas, 550, 185, fit, liveClock: running);
            view.Preview.Content = new Viewbox { Child = image, StretchDirection = StretchDirection.DownOnly };
        }
        else view.Preview.Content = HomeText("未选择内容", 13, muted: true);
    }
    void ConnectSimulatedWizard()
    {
        int step=0,rotation=0;string model="D92",name="我的 D92",id="sim:"+Guid.NewGuid().ToString("N"),content="";
        var panel=new StackPanel{Margin=new Thickness(20)};var dialog=Dialog("添加模拟设备",panel,740,650);
        bool Same()=>state.Devices.Values.Any(d=>d.Model==model);
        Size Native()=>model=="D92"?new(1920,462):new(1280,480);
        Size Canvas()=>rotation==0?Native():new(Native().Height,Native().Width);
        void ResetName(){var count=state.Devices.Values.Count(d=>d.Model==model);name=count==0?"我的 "+model:model+" #"+(count+1);}
        void Draw()
        {
            panel.Children.Clear();string[] steps=Same()?["发现设备","识别","命名与方向","第一个内容"]:["发现设备","摆放方向","第一个内容"];step=Math.Clamp(step,0,steps.Length-1);var current=steps[step];panel.Children.Add(Text($"第 {step+1} 步，共 {steps.Length} 步 · {current}",22));panel.Children.Add(Text("此向导添加模拟设备，不执行硬件枚举。"));
            var progress = new WrapPanel();
            for (var index = 0; index < steps.Length; index++)
            {
                var label = Text($"{(index < step ? "✓" : (index + 1).ToString())} {steps[index]}", 13);
                if (index == step) { label.FontWeight = FontWeights.SemiBold; label.Background = ColorBrush("#E9F0FF"); }
                progress.Children.Add(label);
            }
            panel.Children.Add(progress);
            if (step == 0)
            {
                panel.Children.Add(Choose(["D92", "D69"], model, value =>
                {
                    if (model == value) return;
                    model = value;
                    content = "";
                    id = "sim:" + Guid.NewGuid().ToString("N");
                    ResetName();
                    Draw();
                }));
                panel.Children.Add(Text($"检测到 MiraBox {model}\n身份：{id}\n规格：{SpecText(Native())}\n连接方式：USB（模拟）"));
                var sameModel = state.Devices.Values.Count(device => device.Model == model);
                if (sameModel > 0)
                    panel.Children.Add(Text($"已有 {sameModel} 台 {model}。下一步可用闪烁确认新设备。"));
            }
            if(current=="识别"){var flash=new Border{Height=140,Background=ColorBrush("#253D5C"),Child=Text("模拟设备识别")};panel.Children.Add(flash);panel.Children.Add(Action("让这台设备闪烁",()=>Identify(new Device(id,Native()),flash)));panel.Children.Add(Text("识别用于区分同型号设备，也可跳过。"));}
            if(current is "命名与方向" or "摆放方向")
            {
                if(current=="命名与方向"){var input=new TextBox{Text=name,MaxLength=30,Margin=new Thickness(4)};input.TextChanged+=(_,_)=>name=input.Text;panel.Children.Add(input);var presets=new WrapPanel();foreach(var n in new[]{"左侧","右侧","桌面","副屏"})presets.Children.Add(Action(n,()=>{name=n+" "+model;Draw();}));panel.Children.Add(presets);}else panel.Children.Add(Text("默认名称："+name));
                var native = Native();
                var horizontal = $"横屏 · {SpecText(native)}";
                var vertical = $"竖屏 · {SpecText(new(native.Height, native.Width))}";
                panel.Children.Add(Choose([horizontal, vertical], rotation == 0 ? horizontal : vertical, value =>
                {
                    var selectedRotation = value == horizontal ? 0 : 90;
                    if (selectedRotation == rotation) return;
                    rotation = selectedRotation;
                    if (content != "" && (!state.Resources.TryGetValue(content, out var chosen) ||
                        !(Workspace.Difference(chosen, Canvas()) <= .05))) content = "";
                    Draw();
                }));
                var previewCanvas = Canvas();
                var preview = state.Resources.GetValueOrDefault(content) ?? new Resource("", "", [previewCanvas]);
                var previewLabel = Text($"摆放方向预览 · {SpecText(previewCanvas)}");
                panel.Children.Add(previewLabel);
                panel.Children.Add(Thumbnail(preview, previewCanvas, 320, rotation == 0 ? 90 : 170));
            }
            if (current == "第一个内容")
            {
                var picks = new WrapPanel();
                var compatible = library.Query(new(Sort: "热门")).Select(entry => entry.Resource)
                    .Where(resource => Workspace.Difference(resource, Canvas()) <= .05).Take(4).ToList();
                if (content != "" && compatible.All(resource => resource.Id != content) &&
                    state.Resources.TryGetValue(content, out var chosen) && chosen.Local &&
                    Workspace.Difference(chosen, Canvas()) <= .05)
                {
                    if (compatible.Count == 4) compatible.RemoveAt(3);
                    compatible.Insert(0, chosen);
                }
                foreach (var resource in compatible)
                {
                    var card = new StackPanel();
                    card.Children.Add(Thumbnail(resource, Canvas(), 150, 170));
                    card.Children.Add(Action((content == resource.Id ? "✓ " : "") + resource.Name,
                        () => { content = resource.Id; Draw(); }));
                    picks.Children.Add(card);
                }
                if (picks.Children.Count == 0)
                    panel.Children.Add(Text("暂时没有适合这个规格的内容，可以稍后去在线资源下载。"));
                panel.Children.Add(picks);
                panel.Children.Add(Action(content == "" ? "✓ 稍后再选" : "稍后再选",
                    () => { content = ""; Draw(); }));
            }
            var buttons=new WrapPanel();var back=Action("上一步",()=>{step--;Draw();});back.IsEnabled=step>0;buttons.Children.Add(back);buttons.Children.Add(Action("取消",()=>dialog.Close()));if(current=="识别")buttons.Children.Add(Action("跳过识别",()=>{step++;Draw();}));
            buttons.Children.Add(Action(step==steps.Length-1?"完成并连接":current=="识别"?"我看到了，就是这台":"下一步", () =>
            {
                if (step < steps.Length - 1) { step++; Draw(); return; }
                if (!LeaveEditor()) return;
                var before = SessionStore.Capture(state, library, activeScenario);
                var device = new Device(id, Native()) { Rotation = rotation };
                device.Rename(string.IsNullOrWhiteSpace(name) ? model + " 新设备" : name);
                try
                {
                    state.Devices.Add(id, device);
                    if (content != "") { state.ApplyDirect(id, content); state.NoteUse(content); }
                    SaveWorkspace();
                }
                catch { state = SessionStore.Restore(before, library); throw; }
                dialog.Close();
                RefreshTargets(id);
                ChangePage("首页");
                PageScroll.ScrollToTop();
                Notice.Text = $"已连接「{device.Name}」。";
            }));panel.Children.Add(buttons);
        }
        ResetName();Draw();dialog.ShowDialog();
    }
}
