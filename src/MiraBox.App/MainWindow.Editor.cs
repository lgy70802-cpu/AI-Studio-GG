using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MiraBox.Core;
using Size=MiraBox.Core.Size;
namespace MiraBox.App;
public partial class MainWindow
{
    TextBlock? draftStatus;
    bool draftTemplateShown;
    ThemeProject? draft;string draftName="",draftType="主题";int draftLayout;bool draftDirty;
    static readonly Size[] Specs=[new(1920,462),new(462,1920),new(1280,480),new(480,1280)];
    static string SpecText(Size s)=>$"{s.Width} × {s.Height}";
    static string EditorSpecLabel(Size size) =>
        $"{(size.Width == 1280 || size.Height == 1280 ? "D69" : "D92")} {(size.Width < size.Height ? "竖屏" : "横屏")} · {SpecText(size)}";
    void MarkDraftDirty(){draftDirty=true;if(draftStatus!=null)draftStatus.Text="未保存";}
    void RenderCreator()
    {
        Body.Children.Add(HomeHeader(HomeText("为同一个主题创建适合不同设备和方向的布局。", 13, muted: true),
            HomeButton("＋ 新建主题", () => NewTheme(), "PrimaryButton")));
        var intro = new StackPanel();
        intro.Children.Add(HomeText("从灵感到屏幕", 22, bold: true));
        intro.Children.Add(HomeText("可以为同一个主题补充不同设备与方向的布局。", 14, muted: true));
        intro.Children.Add(HomeButton("从空白画布开始", () => NewTheme(), "PillButton"));
        var hero = HomeCard(HomeHeader(intro, HomeText("Aa", 64, bold: true)));
        hero.SetResourceReference(Border.BackgroundProperty, "HomePreviewBrush");
        hero.Margin = new Thickness(0, 12, 0, 16);
        Body.Children.Add(hero);

        var projects = library.Entries.Where(entry => entry.Resource.Local &&
            (entry.Type is "主题" or "时钟" || entry.Created)).ToArray();
        Body.Children.Add(Text($"我的主题 · {projects.Length} 个", 18));
        if (projects.Length == 0)
        {
            Body.Children.Add(Text("还没有主题。从空白画布开始创建。"));
            Body.Children.Add(HomeButton("新建主题", () => NewTheme(), "PrimaryButton"));
            return;
        }
        var cards = new WrapPanel();
        foreach (var entry in projects) cards.Children.Add(ResourceCard(entry.Resource));
        Body.Children.Add(cards);
    }
    void NewTheme(Resource? background = null, FlipClockSettings? flipClock = null)
    {
        if (background != null)
        {
            if (!LeaveEditor()) return;
            string? copied = null;
            var source = Entry(background.Id).Type == "视频" ? library.ThumbnailPath(Entry(background.Id)) :
                images.GetValueOrDefault(background.Id);
            if (source != null)
            {
                copied = Path.Combine(DataRoot, "Library", "theme-bg-" + Guid.NewGuid().ToString("N") + Path.GetExtension(source));
                File.Copy(source, copied);
            }
            var style = DemoAppearance(background.Id, BrowseCanvas()).Style;
            BeginThemeDraft(background.Name + " · 主题", BrowseCanvas(), copied, background.Name, style);
            ChangePage("主题编辑器");
            return;
        }
        if(!LeaveEditor())return;var panel=new StackPanel{Margin=new Thickness(20)};var dialog=Dialog("新建主题",panel,500,350);var name=new TextBox{Text=flipClock == null ? "我的新主题" : "复古相机翻页时钟",MaxLength=40,Margin=new Thickness(4)};panel.Children.Add(Text("主题名称"));panel.Children.Add(name);var size=Choose(Specs.Select(EditorSpecLabel),EditorSpecLabel(BrowseCanvas()),_=>{});if(size.SelectedIndex<0)size.SelectedIndex=0;panel.Children.Add(Text("初始布局"));panel.Children.Add(size);
        panel.Children.Add(Action("创建并编辑", () =>
        {
            if (string.IsNullOrWhiteSpace(name.Text))
            {
                name.ToolTip = "请输入主题名称";
                name.Focus();
                Notice.Text = "请输入主题名称。";
                return;
            }
            BeginThemeDraft(name.Text.Trim(), Specs[Math.Max(0, size.SelectedIndex)]);
            if (flipClock != null && draft is { } clockDraft)
            {
                clockDraft.Layouts[0] = clockDraft.Layouts[0] with { FlipClock = flipClock };
                draft = clockDraft with { EditorData = null };
                draftTemplateShown = true;
            }
            dialog.Close();
            ChangePage("主题编辑器");
        }));
        dialog.ShowDialog();
    }
    void BeginThemeDraft(string name, Size size, string? backgroundFile = null,
        string? backgroundName = null, string style = "paper")
    {
        draft = new(Guid.NewGuid().ToString("N"), [new(size, Color: style == "paper" ? "#4A463C" : "#FFFFFF", Style: style)], backgroundFile, backgroundName);
        if (Environment.GetEnvironmentVariable("MIRABOX_LEGACY_EDITOR_CHECKS") != "1" && backgroundFile == null)
            draft = draft with { EditorData = System.Text.Json.JsonSerializer.SerializeToElement(new
            { bg = new { kind = "style", style, dim = 0, blur = 0, fit = "cover" }, master = Array.Empty<object>(),
                layouts = new[] { new { follow = false } }, locks = new Dictionary<string, bool>() }) };
        draftTemplateShown = false;
        draftName = name;
        draftType = "主题";
        draftLayout = 0;
        MarkDraftDirty();
    }
    void OpenEditor(Resource r){if(state.Themes.GetValueOrDefault(r.Id)?.CraftPlugin!=null){Navigate("插件中心");Notice.Text="Woodland Clock 由原插件绘制，请在插件中心预览并选择时间制式。";return;}if(!LeaveEditor())return;draft=state.Themes.TryGetValue(r.Id,out var saved)?saved with{Layouts=saved.Layouts.ToList()}:new(r.Id,r.Layouts.Select(s=>DemoAppearance(r.Id,s)).ToList());draftName=r.Name;draftType=Entry(r.Id).Type;draftLayout=0;draftDirty=false;ChangePage("主题编辑器");}
    bool LeaveEditor()
    {
        if(page!="主题编辑器"||!draftDirty)return true;var answer=MessageBox.Show(this,"当前有未保存修改。\n是：保存并离开；否：放弃修改；取消：继续编辑。","离开编辑器？",MessageBoxButton.YesNoCancel);if(answer==MessageBoxResult.Cancel)return false;if(answer==MessageBoxResult.Yes){try{SaveDraft();}catch(Exception ex){Notice.Text=ex.Message;return false;}}draftDirty=false;return true;
    }
    void SaveDraft(){if(draft==null)return;if(string.IsNullOrWhiteSpace(draftName))throw new InvalidOperationException("请输入主题名称。");var before=SessionStore.Capture(state,library,Scenario.SelectedIndex);try{state.SaveTheme(draft,draftName,draftType,library);SaveWorkspace();}catch{state=SessionStore.Restore(before,library);throw;}draftDirty=false;SyncLibrary();}
    void AddLayout(Size? requested=null)
    {
        if(draft==null)return;void Add(Size size){if(draft.Layouts.Any(x=>x.Size==size)){Notice.Text="该尺寸布局已存在。";return;}draft.Layouts.Add(draft.Layouts[draftLayout] with{Size=size});draftLayout=draft.Layouts.Count-1;MarkDraftDirty();Notice.Text=$"已复制当前布局到 {SpecText(size)}，请检查文字位置与显示效果。";Render();}
        if (requested is { } exact) { Add(exact); return; }
        var panel = new StackPanel { Margin = new Thickness(20) };
        var dialog = Dialog("新增显示布局", panel, 540, 410);
        panel.Children.Add(Text($"从当前布局 {SpecText(draft.Layouts[draftLayout].Size)} 复制。"));
        var bySize = state.Devices.Values.GroupBy(device => device.Canvas)
            .ToDictionary(group => group.Key, group => string.Join("、", group.Select(device => device.Name)));
        var existing = draft.Layouts.Select(layout => layout.Size).ToHashSet();
        var sizes = bySize.Keys.Concat(Specs).Concat(existing).Distinct()
            .OrderBy(size => existing.Contains(size) ? 2 : bySize.ContainsKey(size) ? 0 : 1);
        foreach (var size in sizes)
        {
            var already = existing.Contains(size);
            var devices = bySize.TryGetValue(size, out var names) ? $" · {names}" : "";
            var status = already ? " · 已有布局" : devices.Length == 0 ? "" : " · 缺少布局";
            var choice = Action(SpecText(size) + devices + status, () => { dialog.Close(); Add(size); });
            choice.IsEnabled = !already;
            panel.Children.Add(choice);
        }
        panel.Children.Add(Action("取消", dialog.Close));
        dialog.ShowDialog();
    }
    void RenderLegacyEditor()
    {
        if (draft is not { } project) return;
        var tools = new WrapPanel();
        tools.Children.Add(HomeButton("返回项目", () => Navigate("创作中心"), "PillButton"));
        tools.Children.Add(HomeButton("保存", () => SaveEditorDraft(false), "PillButton"));
        tools.Children.Add(HomeButton("完成并使用", () =>
        {
            SaveEditorDraft(true);
        }, "PrimaryButton"));
        Body.Children.Add(tools);
        var meta = new WrapPanel();
        var name = new TextBox { Text = draftName, Width = 210, MaxLength = 40, Margin = new Thickness(4) };
        name.SetResourceReference(StyleProperty, "PlaylistSeconds");
        AutomationProperties.SetName(name, "主题名称");
        name.TextChanged += (_, _) => { draftName = name.Text; MarkDraftDirty(); };
        meta.Children.Add(name);
        var type = Choose(["主题", "时钟"], draftType, value => { draftType = value; MarkDraftDirty(); });
        AutomationProperties.SetName(type, "分类");
        meta.Children.Add(type);
        draftStatus = HomeText(draftDirty ? "未保存" : "已保存", 13, muted: true);
        draftStatus.Margin = new Thickness(8, 0, 12, 0);
        AutomationProperties.SetLiveSetting(draftStatus, AutomationLiveSetting.Polite);
        meta.Children.Add(draftStatus);
        var layoutTools = new StackPanel { Orientation = Orientation.Horizontal };
        layoutTools.Children.Add(HomeText("当前布局", 13, muted: true));
        var layoutChoice = Choose(draft.Layouts.Select(layout => EditorSpecLabel(layout.Size)), EditorSpecLabel(draft.Layouts[draftLayout].Size),
            value => { draftLayout = draft.Layouts.FindIndex(layout => EditorSpecLabel(layout.Size) == value); Render(); });
        AutomationProperties.SetName(layoutChoice, "当前布局");
        layoutTools.Children.Add(layoutChoice);
        layoutTools.Children.Add(HomeButton("＋ 布局", () => AddLayout(), "PillButton"));
        meta.Children.Add(layoutTools);
        var metaCard = HomeCard(meta);
        metaCard.Margin = new Thickness(0, 6, 0, 14);
        Body.Children.Add(metaCard);
        var grid = new Grid();
        for (var column = 0; column < 3; column++) grid.ColumnDefinitions.Add(new());
        for (var row = 0; row < 2; row++) grid.RowDefinitions.Add(new() { Height = GridLength.Auto });
        Body.Children.Add(grid);
        var editingLayout = draftLayout;
        var current = project.Layouts[editingLayout];
        var display = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
        var previewHost = new Viewbox { StretchDirection = StretchDirection.DownOnly, Stretch = Stretch.Uniform };
        var caption = HomeText("", 13, muted: true);
        caption.HorizontalAlignment = HorizontalAlignment.Center;
        caption.Margin = new Thickness(0, 12, 0, 0);
        display.Children.Add(previewHost);
        display.Children.Add(caption);
        var layers = new StackPanel();
        var layoutDevices = state.Devices.Values.Select(device => (device.Name, Size: device.Canvas))
            .Concat(KnownHardwareSerials().Select(serial =>
                (Name: HardwareDeviceName(serial), Size: InHardwareSession(serial, () => HardwareCanvas))))
            .ToArray();
        layers.Children.Add(HomeText("内容与图层", 16, bold: true));
        var layerActions = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        layerActions.Children.Add(HomeButton("＋ 文字", () => Change(layout => layout with { Text = "你好，世界", FlipClock = null }), "SmallButton"));
        layerActions.Children.Add(HomeButton("＋ 时钟", () => Change(layout => layout with { FlipClock = layout.FlipClock ?? new() }), "SmallButton"));
        layers.Children.Add(layerActions);
        layers.Children.Add(Text(current.FlipClock == null ? "文字图层\n背景" : "复古相机翻页时钟\n背景"));
        layers.Children.Add(Text("设备专属布局", 16));
        if (layoutDevices.Length == 0) layers.Children.Add(Text("连接设备后可查看缺失的专属布局。"));
        foreach (var group in layoutDevices.GroupBy(device => device.Size))
        {
            var size = group.Key;
            var available = draft.Layouts.Any(layout => layout.Size == size);
            layers.Children.Add(Text($"{string.Join("、", group.Select(device => device.Name))}\n{SpecText(size)} · {(available ? "已有" : "缺少布局")}"));
            if (!available) layers.Children.Add(Action("＋ " + SpecText(size), () => AddLayout(size)));
        }
        var layerCard = HomeCard(layers);
        grid.Children.Add(layerCard);
        var canvas = HomeCard(display);
        canvas.Background = (Brush)FindResource("TintBrush");
        grid.Children.Add(canvas);
        void Draw()
        {
            var layout = project.Layouts[editingLayout];
            var resource = new Resource(project.ResourceId, draftName, [layout.Size]);
            previewHost.Child = Thumbnail(resource, layout.Size, 700, 370, previewProject: project with { Layouts = [layout] },
                liveClock: true);
            var devices = layoutDevices.Where(device => device.Size == layout.Size).Select(device => device.Name).ToArray();
            var heading = devices.Length == 0 ? "当前编辑布局" : string.Join("、", devices);
            caption.Text = $"{heading} · {SpecText(layout.Size)}";
        }
        var properties = new StackPanel();
        var propertiesCard = HomeCard(properties);
        grid.Children.Add(propertiesCard);
        properties.Children.Add(HomeText("元素属性", 16, bold: true));
        if (current.FlipClock is { } clock)
        {
            properties.Children.Add(Text("时间制式"));
            var format = Choose(["24 小时制", "12 小时制"], clock.TwelveHour ? "12 小时制" : "24 小时制",
                value => ChangeProperty(layout => layout with { FlipClock = new(value == "12 小时制") }));
            AutomationProperties.SetName(format, "时间制式");
            properties.Children.Add(format);
        }
        else
        {
            properties.Children.Add(Text("文字 / 时间"));
            var text = new TextBox { Text = current.Text, MaxLength = 20, Margin = new Thickness(4) };
            text.TextChanged += (_, _) => ChangeProperty(layout => layout with { Text = text.Text });
            AutomationProperties.SetName(text, "文字或时间");
            properties.Children.Add(text);
        }
        properties.Children.Add(Text("颜色（HEX）"));
        var color = new TextBox { Text = current.Color, Margin = new Thickness(4) };
        AutomationProperties.SetName(color, "颜色（HEX）");
        color.TextChanged += (_, _) =>
        {
            var value = color.Text.Trim();
            if (value.Length != 7 || value[0] != '#' || !value.Skip(1).All(Uri.IsHexDigit)) return;
            if (project.Layouts[editingLayout].Color == value) return;
            ChangeProperty(layout => layout with { Color = value });
        };
        color.LostFocus += (_, _) =>
        {
            if (color.Text.Trim() == project.Layouts[editingLayout].Color) return;
            color.Text = project.Layouts[editingLayout].Color;
            Notice.Text = "请输入有效颜色，例如 #FFFFFF。";
        };
        properties.Children.Add(color);
        properties.Children.Add(Text("背景风格"));
        var styles = new Dictionary<string, string>
        {
            ["paper"] = "暖纸", ["ink"] = "深蓝", ["aurora"] = "极光",
            ["pink"] = "柔粉", ["mountain"] = "山谷", ["sun"] = "日落"
        };
        if (!styles.ContainsKey(current.Style)) styles[current.Style] = current.Style;
        var backgroundStyle = Choose(styles.Values, styles[current.Style],
            label => ChangeProperty(layout => layout with { Style = styles.First(pair => pair.Value == label).Key }));
        AutomationProperties.SetName(backgroundStyle, "背景风格");
        properties.Children.Add(backgroundStyle);
        if (project.BackgroundName != null) properties.Children.Add(Text("已复制背景：" + project.BackgroundName));
        Draw();
        ResizeEditor(ActualWidth > 0 ? ActualWidth - 268 : Width - 268);
        grid.SizeChanged += (_, change) =>
        {
            if (change.WidthChanged) ResizeEditor(change.NewSize.Width);
        };
        void ResizeEditor(double width)
        {
            var wide = width >= 760;
            previewHost.MaxHeight = wide ? 370 : 230;
            LayoutEditor(grid, layerCard, canvas, propertiesCard, wide);
        }
        void Change(Func<ThemeLayout, ThemeLayout> change)
        {
            ChangeProperty(change);
            Render();
        }

        void ChangeProperty(Func<ThemeLayout, ThemeLayout> change)
        {
            project.Layouts[editingLayout] = change(project.Layouts[editingLayout]);
            MarkDraftDirty();
            Draw();
        }
    }

    static void LayoutEditor(Grid grid, Border layers, Border canvas, Border properties, bool wide)
    {
        grid.ColumnDefinitions[0].Width = wide ? new GridLength(155) : new GridLength(1, GridUnitType.Star);
        grid.ColumnDefinitions[2].Width = wide ? new GridLength(200) : new GridLength(0);
        Grid.SetRow(layers, wide ? 0 : 1);
        Grid.SetRow(canvas, 0);
        Grid.SetRow(properties, wide ? 0 : 1);
        Grid.SetColumn(canvas, wide ? 1 : 0);
        Grid.SetColumn(properties, wide ? 2 : 1);
        Grid.SetColumnSpan(canvas, wide ? 1 : 2);
        canvas.MinHeight = wide ? 420 : 260;
        canvas.Margin = wide ? new Thickness(12, 0, 12, 12) : new Thickness(0, 0, 0, 12);
        layers.Margin = wide ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 6, 12);
        properties.Margin = wide ? new Thickness(0, 0, 0, 12) : new Thickness(6, 0, 0, 12);
    }
}
