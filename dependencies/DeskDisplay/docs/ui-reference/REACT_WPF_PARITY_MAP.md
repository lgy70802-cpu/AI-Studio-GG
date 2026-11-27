# React 到 WPF 的页面验收映射

参考 ZIP SHA256 为 E9F820223BE85C47AF2044B2F9C8E65763DEE378E9F080C5228D882752F6E51D。

| React 源码 | WPF 对应 | 结构要求 | 当前验收状态 |
|---|---|---|---|
| App.tsx | MainWindow.xaml | 208侧栏、64顶栏、底部输出栏、个人区 | 部分完成，状态与缩略图仍需对照 |
| OverviewPage.tsx 的 Screen Hero | HomeView.xaml | 页标题、主预览头部、画面、内容操作分区 | 本批按区块重新建立XAML，待实窗 |
| OverviewPage.tsx 多设备 | HomeView.xaml 设备集合 | 每设备状态、当前内容和独立操作 | 有卡片，缺每台真实画面缩略图 |
| ResourceCard.tsx | HomeView/LibraryView模板 | 标题类型、封面和悬停操作 | 部分完成，待统一模板和实窗 |
| DiscoverPage.tsx | OnlineResourcesView.xaml | 查询、分类、卡片和详情 | 尚未整页对齐 |
| LibraryPage.tsx | LibraryView.xaml | 规格分组、筛选、批量操作 | 尚未整页对齐 |
| CreatorAndEditorPage.tsx | CreatorView.xaml | 主题列表、画布三栏、多布局 | 保留现有功能，尚未整页对齐 |
| PlaylistsPage.tsx | MixedPlaylistView.xaml | 左列表、右播放控制、预览行 | 已重排，功能和视觉待专项验收 |
| DevicesPage.tsx | DeviceManagementView.xaml | 真实设备身份、方向、输出状态 | 尚未整页对齐 |
| Modals.tsx 等 | 原生Window与MessageBox入口 | 统一遮罩/布局/确认流程 | 尚未统一 |

验收证据按原型截图、WPF实窗截图、交互测试逐项填写。构建与布局测试通过只记录相应结果。旧版本截图位于evidence/mirabox。当前首页原型的ScreenBox缺少确定尺寸，WPF使用有效可用空间与真实场景比例求尺寸。
