using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MiraBox.Core;
using Size=MiraBox.Core.Size;
namespace MiraBox.App;
public partial class MainWindow
{
    static readonly Dictionary<string,Dictionary<string,string[]>> Categories=new(){["壁纸"]=new(){["风景"]=["山水","海岸","星空"],["抽象"]=["渐变","几何"],["极简"]=[],["城市"]=["夜景"]},["主题"]=new(){["效率"]=["日历","待办"],["监控"]=["性能","网络"],["氛围"]=[]},["时钟"]=new(){["数字"]=["翻页","大字"],["模拟"]=[],["极简"]=[]},["视频"]=new(){["自然"]=["云海","极光"],["科幻"]=[],["动画"]=[]}};
    LibraryEntry Entry(string id)=>library.Entries.First(x=>x.Resource.Id==id);
    void RenderLibrary()
    {
        RenderUndo(Body);var online=page=="在线资源";
        if (!online && state.Playlists.FirstOrDefault(list => list.Id == lastResourcePlaylist) is { } destination)
            Body.Children.Add(HomeButton($"查看轮播列表「{destination.Name}」 →", () =>
            {
                selectedList = destination.Id;
                lastResourcePlaylist = null;
                Navigate("轮播列表");
            }, "LinkButton"));
        if (!online) RenderLocalLibraryHeader();
        if (online) RenderLibraryToolbar(true);
        if (online) RenderOnlineActivity();
        if (online) AddCategoryRow(["全部","壁纸","主题","时钟","视频"],contentType,s=>{contentType=s;category=subcategory="全部";Render();});
        if(Categories.TryGetValue(contentType,out var tree)){AddCategoryRow(["全部",..tree.Keys],category,s=>{category=s;subcategory="全部";Render();});if(tree.TryGetValue(category,out var children)&&children.Length>0)AddCategoryRow(["全部",..children],subcategory,s=>{subcategory=s;Render();});}
        var filters = new WrapPanel();
        var sorting = Choose(online?["热门","新上架"]:["最近添加","最常用","名称"],
            online?(sort is "热门" or "新上架"?sort:"热门"):sort, s=>{sort=s;RefreshLibraryFilters();});
        System.Windows.Automation.AutomationProperties.SetName(sorting, "资源排序");
        filters.Children.Add(sorting);
        var reset = HomeButton("清除筛选", ResetLibraryFilters, "LinkButton");
        reset.Visibility = query.Length > 0 || contentType != "全部" || category != "全部" ||
            subcategory != "全部" || collection != "全部资源" || showAll || (!online && browseRotation != DefaultBrowseRotation()) ? Visibility.Visible : Visibility.Collapsed;
        if (online) filters.Children.Add(reset);
        if (online) Body.Children.Add(filters);
        var list = library.Query(new(query, collection, contentType, category, subcategory,
            online ? (sort == "新上架" ? sort : "热门") : sort), online)
            .Where(entry => !online || !OnlineCatalogConfigured || entry.MediaUrl != null)
            .Select(entry => entry.Resource).ToArray();
        var shown = list.Where(resource => showAll || Workspace.Difference(resource, BrowseCanvas()) is <= .05).ToArray();
        if (selecting && !online)
        {
            var selection = new WrapPanel();
            var hiddenSelected = selected.Count - shown.Count(resource => selected.Contains(resource.Id));
            selection.Children.Add(Text($"已选 {selected.Count} 项" +
                (hiddenSelected > 0 ? $" · {hiddenSelected} 项在当前结果之外" : "")));
            var allShown = shown.All(resource => selected.Contains(resource.Id));
            var selectShown = Action(allShown && shown.Length > 0 ? "取消选择当前结果" : "全选当前结果", () =>
            {
                foreach (var resource in shown)
                    if (allShown) selected.Remove(resource.Id); else selected.Add(resource.Id);
                Render();
            });
            selectShown.IsEnabled = shown.Length > 0;
            var add = Action("加入轮播列表", PickPlaylist);
            var delete = Action("移入回收站", () => DeleteResources(selected.ToArray()));
            add.IsEnabled = delete.IsEnabled = selected.Count > 0;
            selection.Children.Add(selectShown);
            selection.Children.Add(add);
            selection.Children.Add(delete);
            var clear = HomeButton("取消全部选择", () => { selected.Clear(); Render(); }, "LinkButton");
            clear.IsEnabled = selected.Count > 0;
            selection.Children.Add(clear);
            if (hiddenSelected > 0)
                selection.Children.Add(HomeButton("清除隐藏选择", () =>
                {
                    selected.IntersectWith(shown.Select(resource => resource.Id));
                    Render();
                }, "LinkButton"));
            Body.Children.Add(new Border { Child = selection, Padding = new Thickness(10, 6, 10, 6),
                CornerRadius = new CornerRadius(14), Background = (Brush)FindResource("HoverBrush") });
        }
        if (online) Body.Children.Add(Text($"{shown.Length} 项内容 · {list.Length-shown.Length} 项因筛选隐藏"));
        else
        {
            if (libraryResultCount != null) libraryResultCount.Text = $"{shown.Length} 项内容";
            if (reset.Visibility == Visibility.Visible) Body.Children.Add(reset);
        }
        if (shown.Length == 0)
        {
            var empty = new StackPanel();
            empty.Children.Add(Text("▱", 30));
            empty.Children.Add(Text("没有符合筛选条件的内容。"));
            if (!online && list.Length > 0) empty.Children.Add(HomeButton("显示其他尺寸", () => { showAll=true; RefreshLibraryFilters(); }, "PillButton")); else empty.Children.Add(HomeButton("清除筛选", ResetLibraryFilters, "PillButton"));
            if (!online) empty.Children.Add(HomeButton("导入素材", Import, "PrimaryButton"));
            Body.Children.Add(HomeCard(empty));
        }
        else
        {
            var canvas = BrowseCanvas();
            var maxHeight = canvas.Width < canvas.Height ? ResourceCardPortraitHeight : ResourceCardPreviewSize;
            var width = Math.Min(ResourceCardPreviewSize, maxHeight * canvas.Width / canvas.Height);
            var height = width * canvas.Height / canvas.Width;
            if (!online) Body.Children.Add(new VirtualResourceGrid(PageScroll, shown.Length, (double)canvas.Width / canvas.Height, () => ((FrameworkElement)Content).ActualWidth, (index,w) => BuildResourceCard(shown[index], canvas, false, w))); else Body.Children.Add(new VirtualResourceGrid(PageScroll, shown.Length, width + 2 * ResourceCardMargin,
                height + 2 * ResourceCardMargin, index => ResourceCard(shown[index], canvas)));
        }
    }

    void AddCategoryRow(IEnumerable<string> values, string active, Action<string> select)
    {
        var row = new WrapPanel();
        foreach (var value in values)
        {
            var item = Action(value, () => select(value));
            item.Tag = value == active ? "Selected" : null;
            item.SetResourceReference(StyleProperty, "CategoryButton");
            row.Children.Add(item);
        }
        Body.Children.Add(row);
    }
    FrameworkElement Thumbnail(Resource r,Size canvas,double maxW=285,double maxH=285,string fit="contain",
        ThemeProject? previewProject = null, bool liveClock = false, DateTimeOffset? clockTime = null)
    {
        var ratio=(double)canvas.Width/canvas.Height;var w=Math.Min(maxW,maxH*ratio);var h=w/ratio;var root=new Grid{Width=w,Height=h,Background=Brushes.Black,ClipToBounds=true};
        var match = Workspace.BestLayout(r, canvas);
        var size = match is { } best ? r.Layouts[best.Index] : canvas;
        var project=previewProject ?? state.Themes.GetValueOrDefault(r.Id);
        if (project?.CraftPlugin is { } craft)
        {
            root.Children.Add(WoodlandArtwork(craft, size));
            return root;
        }
        if (project?.EditorData is { } editorData && match is { } selectedLayout)
        {
            root.Children.Add(new Viewbox { Child = EnhancedThemeArtwork(editorData, selectedLayout.Index, size,
                clockTime ?? (liveClock ? state.LocalTime : null)), Stretch = Stretch.Uniform });
            return root;
        }
        var layout = (match is { } chosen ? project?.Layouts.ElementAtOrDefault(chosen.Index) : null) ??
            DemoAppearance(r.Id, size);
        var demoGradient = project == null ? FindDemoGradient(r.Id) : null;
        var artwork = new Grid { Width = size.Width, Height = size.Height, Background = demoGradient ?? StyleBrush(layout.Style) };
        var entry = library.Entries.FirstOrDefault(x => x.Resource.Id == r.Id);
        var image = project?.BackgroundFile ?? (entry?.Type == "视频" ? library.ThumbnailPath(entry) : images.GetValueOrDefault(r.Id));
        if (image != null)
        {
            try
            {
                var dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
                var decodeScale = fit == "cover" ? Math.Max(w / size.Width, h / size.Height) : Math.Min(w / size.Width, h / size.Height);
                var pixels = Math.Clamp((int)Math.Ceiling(size.Width * decodeScale * dpi), 1, 1920);
                artwork.Children.Add(new Image { Source = Load(image, pixels), Stretch = Stretch.UniformToFill });
            }
            catch { artwork.Children.Add(new TextBlock { Text = "图片无法读取", Foreground = Brushes.White, FontSize = 40 }); }
        }
        var type = entry?.Type;
        if (project != null || type is "主题" or "时钟")
        {
            var fontSize = Math.Min(size.Width, size.Height) * .27;
            var ink = ColorBrush(layout.Color);
            FrameworkElement overlay = layout.FlipClock is { } clock ? BuildFlipClock(clock, ink, fontSize, liveClock, clockTime) :
                new TextBlock { Text = layout.Text, FontSize = fontSize, Foreground = ink, TextAlignment = TextAlignment.Center };
            artwork.Children.Add(new Viewbox { Child = overlay, StretchDirection = StretchDirection.DownOnly,
                MaxWidth = size.Width * .9, MaxHeight = size.Height * .9,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
        }
        if (type == "视频" && image == null && demoGradient == null) artwork.Children.Add(new TextBlock { Text = "视频示意画面", Foreground = Brushes.White,
            FontSize = Math.Min(size.Width, size.Height) * .16, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center });
        var scale=fit=="cover"?Math.Max(w/size.Width,h/size.Height):Math.Min(w/size.Width,h/size.Height);var box=new Viewbox{Width=size.Width*scale,Height=size.Height*scale,Child=artwork,Stretch=Stretch.Fill,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};root.Children.Add(box);return root;
    }
    static Brush ColorBrush(string color){try{return (Brush)new BrushConverter().ConvertFromString(color)!;}catch{return Brushes.White;}}
    static Brush StyleBrush(string style) => style switch
    {
        "paper" => ColorBrush("#E7DFCF"), "sun" => ColorBrush("#BC7D70"),
        "mountain" => ColorBrush("#527F7D"), "pink" => ColorBrush("#B47F9A"),
        "aurora" => ColorBrush("#2E5B67"), _ => DemoStyle(style)
    };
    const double ResourceCardPreviewSize = 285;
    const double ResourceCardPortraitHeight = 440;
    const double ResourceCardMargin = 8;
    FrameworkElement ResourceCard(Resource r, Size? referenceCanvas = null, double? homeWidth = null)
    {
        var canvas = referenceCanvas ?? BrowseCanvas();
        return BuildResourceCard(r, canvas, homeWidth.HasValue, homeWidth);
    }
    FrameworkElement BuildResourceCard(Resource r, Size canvas, bool home, double? homeWidth)
    {
        var thumb = homeWidth is { } width
            ? Thumbnail(r, canvas, width, width * canvas.Height / canvas.Width, home ? "cover" : "contain")
            : Thumbnail(r, canvas, ResourceCardPreviewSize,
                canvas.Width < canvas.Height ? ResourceCardPortraitHeight : ResourceCardPreviewSize);
        var root = new Grid { Width=thumb.Width, Height=thumb.Height, Margin=new Thickness(homeWidth.HasValue ? 0 : ResourceCardMargin) };
        root.SetResourceReference(StyleProperty, "ResourceCard");
        var corner = ((CornerRadius)FindResource("CardRadius")).TopLeft;
        root.Clip = new RectangleGeometry(new Rect(0, 0, thumb.Width, thumb.Height), corner, corner);
        root.Children.Add(thumb);
        var cardEntry = Entry(r.Id);
        var videoTime = cardEntry.Duration is { } duration ? $" ▶ {duration:mm\\:ss}" : cardEntry.Type == "视频" ? " ▶" : "";
        if (home) AddHomeArtworkOverlay(root, r.Name, $"{cardEntry.Type}{videoTime}", Workspace.Difference(r, canvas), stacked: canvas.Width < canvas.Height);
        else AddHomeArtworkOverlay(root, r.Name, $"{cardEntry.Type}{videoTime}" + (page=="资源库" && cardEntry.Type is ("主题" or "时钟") && r.Layouts.Count>1 ? $" · {r.Layouts.Count} 布局" : ""), Workspace.Difference(r, canvas),
            stacked: canvas.Width < canvas.Height);
        root.ToolTip = home ? $"点击应用「{r.Name}」到{(UsesHardwareApply ? HardwareTargetLabel : target ?? "当前设备")}" : r.Name;
        void Toggle(){if(!selected.Add(r.Id))selected.Remove(r.Id);Render();}
        void Activate()
        {
            if (selecting && !home) Toggle();
            else if (home) Apply(r);
            else ResourceDetail(r);
        }
        System.Windows.Automation.AutomationProperties.SetName(root, r.Name);
        root.MouseLeftButtonUp += (_, e) => { Activate(); e.Handled = true; };
        root.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter || !ReferenceEquals(e.OriginalSource, root)) return;
            Activate();
            e.Handled = true;
        };
        if (selecting && !home)
        {
            root.Children.Add(new Border { BorderThickness = new Thickness(selected.Contains(r.Id) ? 2 : 0),
                BorderBrush = (Brush)FindResource("SearchFocusBrush"), CornerRadius = new CornerRadius(corner),
                IsHitTestVisible = false });
            var check = new CheckBox
            {
                IsChecked = selected.Contains(r.Id), HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(8)
            };
            check.Click += (_, e) => { Toggle(); e.Handled = true; };
            root.Children.Add(check);
        }
        else AddResourceCardActions(root, r, home);

        AddCardFocusOutline(root);
        if (home) return FrameHomeArtwork(root);
        return root;
    }
    Window Dialog(string title, UIElement content, double width = 780, double height = 620, bool scrollContent = true)
    {
        var dialog = new Window
        {
            Title = title, Width = width, Height = height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.CanResize
        };
        if (IsLoaded) dialog.Owner = this;
        dialog.SetResourceReference(BackgroundProperty, "WorkspaceBrush");
        dialog.SetResourceReference(ForegroundProperty, "InkBrush");
        System.Windows.Shell.WindowChrome.SetWindowChrome(dialog, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 48, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0)
        });
        var shell = new DockPanel();
        var heading = new DockPanel { Margin = new Thickness(14, 7, 10, 0), Background = Brushes.Transparent };
        var close = new Button { Content = "关闭", HorizontalAlignment = HorizontalAlignment.Right };
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(close, true);
        close.Click += (_, _) => dialog.Close();
        DockPanel.SetDock(close, Dock.Right);
        heading.Children.Add(close);
        heading.Children.Add(Text(title, 18));
        DockPanel.SetDock(heading, Dock.Top);
        shell.Children.Add(heading);
        shell.Children.Add(scrollContent ? new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } : content);
        var frame = new Border { Child = shell };
        frame.SetResourceReference(StyleProperty, "Modal");
        dialog.Content = frame;
        dialog.PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) dialog.Close(); };
        return dialog;
    }
    void ResourceDetail(Resource r, bool startVideoPreview = false)
    {
        var entry = Entry(r.Id);
        var devices = state.Devices.Values.ToArray();
        var reference = HardwarePlaybackSelected ? HardwareCanvas :
            devices.Length == 1 ? devices[0].Canvas : BrowseCanvas();
        var match = Workspace.BestLayout(r, reference);
        var canvas = match is { } best ? r.Layouts[best.Index] : reference;
        var portrait = canvas.Width < canvas.Height;
        var video = entry.Type == "视频";
        var root = new Grid { Margin = new Thickness(22) };
        var dialog = Dialog(r.Name, root);
        dialog.MinWidth = 480;
        dialog.MinHeight = 420;
        KeyboardNavigation.SetTabNavigation(root, KeyboardNavigationMode.Cycle);

        var details = new StackPanel();
        var poster = Thumbnail(r, canvas, portrait ? 250 : 680, portrait ? 440 : 340, liveClock: true);
        var art = video && r.Local ? BuildLocalVideoPreview(entry, poster, dialog, startVideoPreview) : poster;
        art.HorizontalAlignment = HorizontalAlignment.Center;
        var artHost = new Viewbox { Child = art, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        root.ColumnDefinitions.Add(new());
        root.ColumnDefinitions.Add(new() { Width = new GridLength(0) });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        art.VerticalAlignment = VerticalAlignment.Top;
        root.Children.Add(artHost);
        root.Children.Add(details);
        root.SizeChanged += (_, _) => ArrangeDetails();
        ArrangeDetails();

        var dimensions = r.Layouts.Where(size => size.Width > 0 && size.Height > 0).Select(SpecText).ToArray();
        var duration = video ? entry.Duration is { } length ? $" · 时长 {PlaybackTime(length)}" : " · 时长待检查" : "";
        details.Children.Add(Text($"{entry.Type} · {(dimensions.Length == 0 ? "尺寸待检查" : string.Join(" / ", dimensions))}{duration}"));
        if (HardwarePlaybackSelected) details.Children.Add(BuildHardwareOperationTarget());
        if (HardwarePlaybackSelected && entry.Type is "壁纸" or "视频")
        {
            details.Children.Add(Text("图片 / 视频应用方式（下次应用到当前设备时生效）", 13));
            details.Children.Add(HardwareMediaFitControl());
        }
        if (video)
        {
            var fit = AppliedLayout(r, HardwareCanvas, HardwareMediaFit);
            var compatible = Workspace.Difference(r, HardwareCanvas) is <= .05;
            var issue = r.Local ? HardwarePlaylistIssue(r) : null;
            var playlistNote = issue is "视频文件已丢失" or "D92 无法播放此内容" ?
                issue : compatible ? "适合轮播" : "轮播会跳过";
            details.Children.Add(Text($"{HardwareModel} 实机 · {SpecText(HardwareCanvas)} · {fit} · {playlistNote}", 13));
        }

        if (devices.Length > 1) details.Children.Add(Text("在我的设备上", 16));
        var editable = entry.Type is "主题" or "时钟";
        if (HardwarePlaybackSelected && !video)
            details.Children.Add(Text($"{HardwareTargetLabel} · {SpecText(HardwareCanvas)} · {FitText(r, HardwareCanvas)}" +
                (r.Local && HardwareApplyIssue(r) is { } issue ? $" · {issue}" : ""), 13));
        foreach (var device in devices.Where(_ => !HardwarePlaybackSelected))
        {
            var fit = editable && r.Layouts.Contains(device.Canvas) ? "专属布局" : FitText(r, device.Canvas);
            var name = devices.Length == 1 ? $"在「{device.Name}」上" :
                $"{device.Name} · {device.Model} · {SpecText(device.Canvas)}";
            details.Children.Add(Text($"{name}　{fit}{(device.Online ? "" : " · 离线")}"));
        }

        var secondary = new WrapPanel();
        var redrawOnClose = false;
        var favorite = Action("", () =>
        {
            library.Update(r.Id, item => item with { Favorite = !item.Favorite });
            SyncLibrary();
            redrawOnClose = true;
        });
        favorite.Content = favorites.Contains(r.Id) ? "取消收藏" : "收藏";
        favorite.Click += (_, _) => favorite.Content = favorites.Contains(r.Id) ? "取消收藏" : "收藏";
        dialog.Closed += (_, _) => { if (redrawOnClose) Render(); };
        secondary.Children.Add(favorite);
        details.Children.Add(secondary);

        var primary = new WrapPanel();
        details.Children.Add(primary);
        if (!r.Local)
        {
            primary.Children.Add(Action(DownloadLabel(r), () => { dialog.Close(); Download(r); }));
        }
        else
        {
            secondary.Children.Add(Action("加入轮播列表", () =>
            {
                dialog.Close();
                PickPlaylist([r.Id]);
            }));
            if (editable)
                secondary.Children.Add(Action("编辑布局", () => { dialog.Close(); OpenEditor(r); }));
            else
                secondary.Children.Add(Action("用作主题背景", () => { dialog.Close(); NewTheme(r); }));
            if (page != "在线资源")
                secondary.Children.Add(Action("删除", () => { dialog.Close(); DeleteResources([r.Id]); }));
            if (!video)
            {
                var preview = Action("预览 15 秒", () => { dialog.Close(); Preview(r); });
                preview.IsEnabled = !UsesHardwareApply || CanPreviewHardware(r);
                preview.ToolTip = UsesHardwareApply && !preview.IsEnabled ? HardwareApplyIssue(r) ??
                    "请先应用底层内容，再开始临时预览" : null;
                ToolTipService.SetShowOnDisabled(preview, true);
                primary.Children.Add(preview);
            }
            var apply = HomeButton(UsesHardwareApply ? $"应用到 {HardwareTargetLabel}" : target == null ? "应用" : $"使用到 {DeviceName(target)}",
                () => { dialog.Close(); Apply(r); }, "PrimaryButton");
            var applyIssue = UsesHardwareApply ? HardwareApplyIssue(r) : null;
            apply.IsEnabled = applyIssue == null;
            apply.ToolTip = applyIssue;
            ToolTipService.SetShowOnDisabled(apply, true);
            if (!apply.IsEnabled) apply.Opacity = .45;
            primary.Children.Add(apply);
            if (devices.Length > 1)
                primary.Children.Add(Action("其他设备…", () => { dialog.Close(); ApplyPanel(r); }));
        }
        dialog.ShowDialog();

        void ArrangeDetails()
        {
            var sideBySide = portrait && (root.ActualWidth > 0 ? root.ActualWidth : dialog.Width - 80) >= 580;
            root.ColumnDefinitions[0].Width = sideBySide ? new GridLength(160) : new GridLength(1, GridUnitType.Star);
            root.ColumnDefinitions[1].Width = sideBySide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            Grid.SetColumn(details, sideBySide ? 1 : 0);
            Grid.SetRow(details, sideBySide ? 0 : 1);
            Grid.SetColumnSpan(artHost, sideBySide ? 1 : 2);
            Grid.SetColumnSpan(details, sideBySide ? 1 : 2);
            details.Margin = sideBySide ? new Thickness(16, 0, 0, 0) : new Thickness(0, 16, 0, 0);
            artHost.MaxWidth = sideBySide ? 160 : Math.Max(1, root.ActualWidth > 0 ? root.ActualWidth : dialog.Width - 80);
        }
    }
    static string FitText(Resource r,Size size){var f=Workspace.Difference(r,size);return f is null?"尺寸未知":f<=.01?"适合":$"{(f<=.05?"轻微":"明显")}留边 {f:P1}";}
    IEnumerable<LibraryEntry> LocalDownloads() => library.Entries
        .Where(entry => entry.Downloaded && entry.Resource.Local).OrderByDescending(entry => entry.Added);

    void Apply(Resource resource)
    {
        if (!resource.Local)
        {
            Notice.Text = "请先下载到资源库。";
            return;
        }

        if (UsesHardwareApply)
        {
            ApplyHardwareResource(resource);
            return;
        }

        if (target == null || !state.Devices.TryGetValue(target, out var device) ||
            !(Workspace.Difference(resource, device.Canvas) <= .05))
        {
            ApplyPanel(resource);
            return;
        }

        state.ApplyDirect(device.Id, resource.Id);
        state.NoteUse(resource.Id);
        Notice.Text = device.Online ? $"已应用到模拟设备「{device.Name}」 · {AppliedLayout(resource, device.Canvas, "contain")}" :
            "设备离线，已保存修改，重新连接后自动显示。";
        Render();
    }
    void Preview(Resource resource)
    {
        if (UsesHardwareApply) { PreviewHardwareStill(resource); return; }
        if (target == null || !state.Devices.TryGetValue(target, out var device) || !device.Online)
        {
            Notice.Text = "当前设备未连接，无法预览。";
            return;
        }
        var loss = Workspace.Difference(resource, device.Canvas);
        if (!resource.Local || loss == null)
        {
            Notice.Text = "请先下载并检查素材尺寸。";
            return;
        }
        if (state.Preview is { } active) state.EndPreview(active.Token);
        RenderOutputs();
        void Begin()
        {
            state.BeginPreview(device.Id, resource.Id, true);
            RenderOutputs();
            UpdateHomePreview();
        }
        if (loss <= .05)
        {
            Begin();
            return;
        }
        var panel = new StackPanel { Margin = new Thickness(20) };
        var dialog = Dialog("预览会明显留边", panel, 560, 440);
        panel.Children.Add(Text($"设备方向保持不变。将在「{device.Name}」上完整显示，15 秒后恢复。"));
        panel.Children.Add(Thumbnail(resource, device.Canvas, 450, 235));
        var actions = new WrapPanel();
        actions.Children.Add(Action("取消", dialog.Close));
        actions.Children.Add(Action("仍然预览", () => { dialog.Close(); Begin(); }));
        panel.Children.Add(actions);
        dialog.ShowDialog();
    }
    void ApplyPanel(Resource r)
    {
        var panel=new StackPanel{Margin=new Thickness(20)};var dialog=Dialog("应用「"+r.Name+"」",panel);var choices=new List<(Device Device,CheckBox Check,ComboBox Mode)>();
        if (state.Devices.Count == 0)
        {
            panel.Children.Add(Text("请先连接设备。"));
            panel.Children.Add(Action("连接新设备", () => { dialog.Close(); ConnectWizard(); }));
        }
        else if (r.Layouts.All(size => size.Width <= 0 || size.Height <= 0))
        {
            panel.Children.Add(Text("尺寸未知，请先检查素材。"));
        }
        else if (state.Devices.Values.All(device => !device.Online))
        {
            panel.Children.Add(Text("当前没有已连接的设备。"));
        }
        var feedback = Text("");
        var confirmationPending = false;
        var type = Entry(r.Id).Type;
        foreach (var device in state.Devices.Values)
        {
            var difference = Workspace.Difference(r, device.Canvas);
            var enabled = device.Online && difference != null;
            var check = new CheckBox
            {
                Content = $"{device.Name} · {device.Model} · {SpecText(device.Canvas)} · {FitText(r, device.Canvas)}" +
                    (device.Online ? "" : " · 离线"),
                IsEnabled = enabled, IsChecked = enabled && device.Id == target, Margin = new Thickness(8)
            };
            panel.Children.Add(check);
            var preview = new ContentControl { Content = Thumbnail(r, device.Canvas, 460, 150),
                HorizontalAlignment = HorizontalAlignment.Left };
            var layoutNote = Text(AppliedLayout(r, device.Canvas, "contain"), 13);
            var mode = Choose(type is "壁纸" or "视频" ? ["完整显示", "裁切铺满"] : ["完整显示"],
                "完整显示", value =>
                {
                    var fit = value == "裁切铺满" ? "cover" : "contain";
                    preview.Content = Thumbnail(r, device.Canvas, 460, 150, fit);
                    layoutNote.Text = AppliedLayout(r, device.Canvas, fit);
                });
            mode.IsEnabled = enabled;
            panel.Children.Add(mode);
            panel.Children.Add(layoutNote);
            if (difference > .05 && type is "主题" or "时钟")
                panel.Children.Add(Text("完整显示会留边。可在编辑器创建专属布局。"));
            panel.Children.Add(preview);
            choices.Add((device, check, mode));
        }
        var actions = new WrapPanel();
        actions.Children.Add(Action("取消", dialog.Close));
        Button? apply = null;
        apply = Action("应用到所选设备", () => Confirm(apply!));
        void Confirm(Button button)
        {
            var recipients = choices.Where(choice => choice.Check.IsEnabled && choice.Check.IsChecked == true).ToArray();
            if (recipients.Length == 0)
            {
                feedback.Text = "请至少选择一台已连接设备。";
                return;
            }
            var mismatched = recipients.Where(choice => Workspace.Difference(r, choice.Device.Canvas) > .05).ToArray();
            if (mismatched.Length > 0 && !confirmationPending)
            {
                confirmationPending = true;
                button.Content = "仍然使用";
                feedback.Text = $"「{string.Join("、", mismatched.Select(choice => choice.Device.Name))}」有明显比例差异，请确认。";
                return;
            }
            foreach (var choice in recipients)
                state.ApplyConfirmed(choice.Device.Id, r.Id, (string)choice.Mode.SelectedItem == "裁切铺满" ? "cover" : "contain");
            state.NoteUse(r.Id);
            dialog.Close();
            Notice.Text = recipients.Length == 1
                ? $"已应用到「{recipients[0].Device.Name}」 · {AppliedLayout(r, recipients[0].Device.Canvas, recipients[0].Device.Fit)}"
                : "已更新：" + string.Join("、", recipients.Select(choice => choice.Device.Name));
            Render();
            if (state.Devices.Count > 1)
            {
                var result = new StackPanel { Margin = new Thickness(20) };
                var resultDialog = Dialog("应用结果", result, 460, 360);
                foreach (var recipient in recipients)
                    result.Children.Add(Text($"{recipient.Device.Name} · {AppliedLayout(r, recipient.Device.Canvas, recipient.Device.Fit)}"));
                result.Children.Add(Action("查看所有设备", () => { resultDialog.Close(); Navigate("首页"); }));
                resultDialog.ShowDialog();
            }
        }
        apply.IsEnabled = choices.Any(choice => choice.Check.IsEnabled);
        foreach (var choice in choices)
        {
            choice.Check.Click += (_, _) => ResetConfirmation();
            choice.Mode.SelectionChanged += (_, _) => ResetConfirmation();
        }
        void ResetConfirmation()
        {
            confirmationPending = false;
            feedback.Text = "";
            apply.Content = "应用到所选设备";
        }
        actions.Children.Add(apply);
        panel.Children.Add(feedback);
        panel.Children.Add(actions);
        dialog.ShowDialog();
    }
    string AppliedLayout(Resource resource, Size canvas, string fit)
    {
        if (Workspace.BestLayout(resource, canvas) is not { } match) return "素材尺寸未知";
        var size = resource.Layouts[match.Index];
        var layout = size == canvas ? "专属尺寸" : "使用布局";
        var difference = match.Loss > .00001
            ? fit == "cover" ? $" · 比例不同，裁切约 {match.Loss:P0}" : $" · 比例不同，留边约 {match.Loss:P0}"
            : "";
        return $"{layout} {SpecText(size)}{difference}";
    }

    void DeleteResources(string[] ids)
    {
        if(ids.Length==0)return;
        var affected = state.Devices.Values.Where(device => device.Content != null && ids.Contains(device.Content)).ToArray();
        var devices = affected.Select(device => device.Name);
        var physicalAffected = hardwareStill?.ResourceId is { } baseId && ids.Contains(baseId) ||
            hardwareVideoResourceId is { } videoId && ids.Contains(videoId);
        var lists=state.Playlists.Where(x=>x.Items.Any(i=>ids.Contains(i.ResourceId))).Select(x=>x.Name);
        var onlineCopy = library.Entries.Any(entry => ids.Contains(entry.Resource.Id) && (entry.Downloaded || entry.Online));
        var downloadHint = onlineCopy ? "\n下载资源只移除本地副本，仍可在在线资源重新下载。" : "";
        if(MessageBox.Show(this,$"将 {ids.Length} 项移入回收站，保留30天。\n设备：{string.Join("、",devices)}{(physicalAffected ? "、" + HardwareTargetLabel : "")}\n轮播列表：{string.Join("、",lists)}\n设备切回上一次适配内容，无候选时停止。不会删除电脑原文件。{downloadHint}","删除确认",MessageBoxButton.OKCancel)!=MessageBoxResult.OK)return;
        var before=SessionStore.Capture(state,library,Scenario.SelectedIndex);try{foreach(var id in ids)state.DeleteToTrash(id,library,DateTimeOffset.UtcNow);SaveWorkspace();}catch{state=SessionStore.Restore(before,library);throw;}selected.Clear();selecting=false;SyncLibrary();Render();
        Notice.Text = $"已将 {ids.Length} 项移入回收站。";
        _ = ReconcileHardwareLibraryDeletion(ids);
        if (affected.Length == 0) return;
        var result = new StackPanel { Margin = new Thickness(20) };
        var dialog = Dialog("删除结果", result, 580, 360);
        result.Children.Add(Text(Notice.Text));
        foreach (var device in affected)
        {
            var outcome = device.Content == null ? "无可回退内容，已停止输出。" :
                device.Running ? $"已切回「{ResourceName(device.Content)}」。" :
                $"已保留回退内容「{ResourceName(device.Content)}」，输出保持停止。";
            result.Children.Add(Text($"{device.Name} · {outcome}"));
        }
        result.Children.Add(Action("关闭", dialog.Close));
        dialog.ShowDialog();
    }
    void ShowHistory()
    {
        var physical = UsesHardwareApply;
        var device = target == null ? null : state.Devices.GetValueOrDefault(target);
        var canvas = physical ? HardwareCanvas : device?.Canvas ?? BrowseCanvas();
        var outputLabel = physical ? HardwareTargetLabel : device?.Name ?? "未选择设备";
        var portrait = canvas.Width < canvas.Height;
        var thumbnailWidth = portrait ? 90d * canvas.Width / canvas.Height : 140;
        var panel = new Grid { Margin = new Thickness(20, 8, 20, 20) };
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new() { Height = GridLength.Auto });
        panel.RowDefinitions.Add(new());
        var dialog = Dialog("使用记录", panel, scrollContent: false);
        dialog.MinWidth = 500;
        dialog.MinHeight = 360;
        panel.Children.Add(Text($"按时间倒序，可再次应用。当前目标：{outputLabel} · {SpecText(canvas)}", 13));
        var tabs = new WrapPanel { Margin = new Thickness(0, 2, 0, 10) };
        Grid.SetRow(tabs, 1);
        panel.Children.Add(tabs);
        var list = new StackPanel();
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 2);
        panel.Children.Add(scroll);
        foreach (var type in new[] { "全部", "壁纸", "视频", "主题", "时钟" })
        {
            var tab = new Button { Content = type };
            tab.SetResourceReference(StyleProperty, "CategoryButton");
            tab.Click += (_, _) => Draw(type);
            tabs.Children.Add(tab);
        }
        Draw("全部");
        dialog.ShowDialog();

        void Draw(string type)
        {
            foreach (var tab in tabs.Children.OfType<Button>())
                tab.Tag = Equals(tab.Content, type) ? "Selected" : null;
            scroll.ScrollToTop();
            list.Children.Clear();
            foreach (var use in state.Recent.OrderByDescending(use => use.At))
            {
                var entry = library.Entries.FirstOrDefault(entry => entry.Resource.Id == use.ResourceId);
                if (entry == null || type != "全部" && entry.Type != type) continue;
                var resource = entry.Resource;
                var row = new Grid { Margin = new Thickness(0, 8, 4, 8) };
                row.ColumnDefinitions.Add(new() { Width = new GridLength(thumbnailWidth + 14) });
                row.ColumnDefinitions.Add(new());
                row.ColumnDefinitions.Add(new() { Width = new GridLength(176) });
                row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
                var preview = new Border
                {
                    Width = thumbnailWidth, Height = portrait ? 90 : thumbnailWidth * canvas.Height / canvas.Width,
                    HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(8)
                };
                preview.SetResourceReference(Border.BackgroundProperty, "HomePreviewBrush");
                if (Workspace.BestLayout(resource, canvas) != null)
                {
                    var image = Thumbnail(resource, canvas, thumbnailWidth, 90);
                    image.Clip = new RectangleGeometry(new Rect(0, 0, image.Width, image.Height), 8, 8);
                    preview.Child = image;
                }
                row.Children.Add(preview);
                var layouts = resource.Layouts.Count > 1 ? $" · {resource.Layouts.Count} 个布局" : "";
                var status = resource.Local ? "" : " · 本地已删除";
                var information = new StackPanel { Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
                var name = HomeText(resource.Name, 15, bold: true);
                name.TextWrapping = TextWrapping.NoWrap;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.ToolTip = resource.Name;
                information.Children.Add(name);
                information.Children.Add(HomeText($"{entry.Type}{layouts} · {HistoryAge(use.At)}{status}", 12, muted: true));
                Grid.SetColumn(information, 1);
                row.Children.Add(information);
                var difference = Workspace.Difference(resource, canvas);
                var issue = physical && resource.Local ? HardwareApplyIssue(resource) : null;
                if (resource.Local && (issue != null || difference is null or > .01))
                {
                    var fit = HomeText(issue ?? FitText(resource, canvas), 12, muted: true);
                    fit.Margin = new Thickness(0, 0, 12, 0);
                    Grid.SetColumn(fit, 2);
                    row.Children.Add(fit);
                }
                var apply = HomeButton("应用", () => { dialog.Close(); Apply(resource); }, "SmallPrimaryButton");
                apply.IsEnabled = resource.Local && issue == null;
                if (!apply.IsEnabled) apply.Opacity = .45;
                apply.VerticalAlignment = VerticalAlignment.Center;
                apply.ToolTip = !resource.Local ? "本地内容已删除" : issue ?? $"应用到 {outputLabel}";
                ToolTipService.SetShowOnDisabled(apply, true);
                Grid.SetColumn(apply, 3);
                row.Children.Add(apply);
                var separator = new Border { Child = row, BorderThickness = new Thickness(0, 0, 0, 1) };
                separator.SetResourceReference(Border.BorderBrushProperty, "LineBrush");
                list.Children.Add(separator);
            }
            if (list.Children.Count == 0) list.Children.Add(Text("暂无记录。"));
        }
    }

    static string HistoryAge(DateTimeOffset at)
    {
        var minutes = Math.Round((DateTimeOffset.UtcNow - at).TotalMinutes);
        if (minutes < 1) return "刚刚";
        if (minutes < 60) return $"{minutes:0} 分钟前";
        var hours = Math.Round(minutes / 60);
        if (hours < 24) return $"{hours:0} 小时前";
        var days = Math.Round(hours / 24);
        return days == 1 ? "昨天" : $"{days:0} 天前";
    }
}
