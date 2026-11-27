using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

namespace MiraBox.App;

public partial class MainWindow
{
    void RenderLocalLibraryHeader()
    {
        var tabs = LibraryTabs(["全部资源", "收藏", "已下载", "我的创作"], collection, value => collection = value);
        tabs.Children.Add(HomeButton($"回收站 · {state.Trash.Count}", ShowTrash, "CategoryButton"));
        var tools = new WrapPanel();
        var search = new TextBox { Text = query, Width = 240, Tag = "搜索资源名称" };
        search.SetResourceReference(StyleProperty, "SearchBox");
        AutomationProperties.SetName(search, "搜索资源名称");
        search.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            query = search.Text.Trim();
            RefreshLibraryFilters();
            e.Handled = true;
        };
        tools.Children.Add(search);
        var import = HomeButton(importing ? "正在导入…" : "+ 导入素材", Import, "PillButton");
        import.IsEnabled = !importing;
        tools.Children.Add(import);
        tools.Children.Add(HomeButton("新建主题", () => NewTheme(), "PrimaryButton"));
        Body.Children.Add(LibraryRow(tabs, tools));

        var filters = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        var device = HomeText($"{(HardwarePlaybackSelected ? HardwareDeviceName(hardwareApplySerial!) : target == null ? "当前设备" : DeviceName(target))} · {SpecText(BrowseCanvas())}", 13);
        filters.Children.Add(new Border { Child = device, Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 0, 18, 0), CornerRadius = new CornerRadius(8),
            Background = (System.Windows.Media.Brush)FindResource("HoverBrush") });
        filters.Children.Add(LibraryTabs(["全部", "壁纸", "主题", "时钟", "视频"], contentType,
            value => { contentType = value; category = subcategory = "全部"; }));
        var other = new CheckBox { Content = "显示其他尺寸", IsChecked = showAll,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 8, 0) };
        other.Click += (_, _) => { showAll = other.IsChecked == true; RefreshLibraryFilters(); };
        filters.Children.Add(other);
        var right = new WrapPanel();
        libraryResultCount = HomeText("", 13, muted: true);
        right.Children.Add(libraryResultCount);
        right.Children.Add(HomeButton(selecting ? "退出多选" : "多选", () =>
        {
            selecting = !selecting;
            selected.Clear();
            Render();
        }, "SmallButton"));
        var order = Choose(["最近添加", "最常用", "名称"], sort, value => { sort = value; RefreshLibraryFilters(); });
        AutomationProperties.SetName(order, "资源排序");
        right.Children.Add(order);
        Body.Children.Add(new Border { Child = LibraryRow(filters, right), Padding = new Thickness(16),
            Margin = new Thickness(0, 14, 0, 10), CornerRadius = new CornerRadius(14),
            Background = System.Windows.Media.Brushes.White });
    }

    TextBlock? libraryResultCount;

    WrapPanel LibraryTabs(string[] values,string active,Action<string> select)
    {
        var tabs=new WrapPanel();
        foreach(var value in values)
        {
            var button=HomeButton(value,()=>{select(value);RefreshLibraryFilters();},"CategoryButton");
            button.Tag=value==active?"Selected":null;
            tabs.Children.Add(button);
        }
        return tabs;
    }

    static Grid LibraryRow(FrameworkElement left, FrameworkElement right)
    {
        var row=new Grid { Margin=new Thickness(0,3,0,3) };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        row.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
        left.VerticalAlignment=right.VerticalAlignment=VerticalAlignment.Center;
        left.HorizontalAlignment=HorizontalAlignment.Left;
        Grid.SetColumn(right,1);
        row.Children.Add(left); row.Children.Add(right);
        row.SizeChanged+=(_,e)=>{
            if (!e.WidthChanged) return;
            left.Measure(new System.Windows.Size(double.PositiveInfinity,double.PositiveInfinity));
            right.Measure(new System.Windows.Size(double.PositiveInfinity,double.PositiveInfinity));
            var wrap=left.DesiredSize.Width+right.DesiredSize.Width+12>e.NewSize.Width;
            Grid.SetColumnSpan(left,wrap?2:1); Grid.SetColumnSpan(right,wrap?2:1);
            Grid.SetColumn(right,wrap?0:1); Grid.SetRow(right,wrap?1:0);
            right.HorizontalAlignment=wrap?HorizontalAlignment.Left:HorizontalAlignment.Right;
        };
        return row;
    }

    void RefreshLibraryFilters()
    {
        Render();
        PageScroll.ScrollToTop();
    }

    void ResetLibraryFilters()
    {
        query = "";
        contentType = category = subcategory = "全部";
        collection = "全部资源";
        showAll = false;
        browseRotation = DefaultBrowseRotation();
        RefreshLibraryFilters();
    }

    void RenderLibraryToolbar(bool online)
    {
        var searchLine = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
        var search = new TextBox { Text = query, Width = 240, MaxLength = 100,
            Tag = "搜索资源名称", Margin = new Thickness(0, 4, 8, 4) };
        search.SetResourceReference(StyleProperty, "SearchBox");
        AutomationProperties.SetName(search, "搜索资源名称");
        searchLine.Children.Add(search);
        searchLine.Children.Add(HomeButton("搜索", SubmitSearch, "PillButton"));
        search.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) { SubmitSearch(); e.Handled = true; }
        };
        if (online) RenderOnlineControls(searchLine);
        else
        {
            var import = HomeButton(importing ? "正在导入…" : "导入素材", Import, "PrimaryButton");
            import.IsEnabled = !importing;
            searchLine.Children.Add(import);
            if (!importing && lastImportResults.Count > 0)
                searchLine.Children.Add(HomeButton("导入结果", ShowImportResults, "LinkButton"));
            searchLine.Children.Add(HomeButton("新建主题", () => NewTheme(), "PillButton"));
            searchLine.Children.Add(HomeButton(selecting ? "退出多选" : "多选", () =>
            {
                selecting = !selecting;
                selected.Clear();
                Render();
            }, "PillButton"));
            searchLine.Children.Add(HomeButton($"回收站 · {state.Trash.Count}", ShowTrash, "PillButton"));
        }
        if (online) Body.Children.Add(searchLine);
        else
        {
            var actions = new WrapPanel();
            while(searchLine.Children.Count > 2) { var action=searchLine.Children[2]; searchLine.Children.RemoveAt(2); actions.Children.Add(action); }
            Body.Children.Add(LibraryRow(HomeText("资源库",26,bold:true), actions));
            Body.Children.Add(LibraryRow(LibraryTabs(["全部资源","收藏","已下载","我的创作"],collection,v=>collection=v),searchLine));
        }
        libraryImportProgress = null;
        libraryImportBar = null;
        if (importing)
        {
            libraryImportProgress = HomeText("", 13, muted: true);
            libraryImportBar = new ProgressBar { Height = 5, Margin = new Thickness(0, 4, 0, 10) };
            Body.Children.Add(libraryImportProgress);
            Body.Children.Add(libraryImportBar);
            UpdateImportProgress();
        }

        var output = HardwarePlaybackSelected ? HardwareTargetLabel :
            target != null ? DeviceName(target) : "连接设备后可应用内容";
        FrameworkElement device = HardwarePlaybackSelected ? BuildHardwareOperationTarget() : HomeText(output, 13, muted: true);
        if (online) Body.Children.Add(device);
        else if (KnownHardwareSerials().Length > 1) { var picker=BuildHomeDevicePicker(); AutomationProperties.SetName(picker,"资源库操作设备"); picker.ToolTip="切换操作设备"; device=picker; }
        var directions = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
        foreach (var (label, rotation) in new[] { ("横屏素材", 0), ("竖屏素材", 90) })
        {
            var direction = HomeButton(label, () =>
            {
                browseRotation = rotation;
                showAll = false;
                RefreshLibraryFilters();
            }, "CategoryButton");
            direction.Tag = browseRotation % 180 == rotation ? "Selected" : null;
            directions.Children.Add(direction);
        }
        var all = new CheckBox { Content = "显示其他尺寸", IsChecked = showAll,
            Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        all.Click += (_, _) => { showAll = all.IsChecked == true; RefreshLibraryFilters(); };
        directions.Children.Add(all);
        directions.Children.Add(HomeText($"浏览尺寸 {SpecText(BrowseCanvas())}", 12, muted: true));
        if (online) Body.Children.Add(directions); else Body.Children.Add(LibraryRow(device,directions));

        void SubmitSearch()
        {
            query = search.Text.Trim();
            RefreshLibraryFilters();
        }
    }
}
