# D92 Studio V6.3 客户端 UI 差异报告

日期：2026-11-04。阶段 A：调查与方案，尚未批准实施。

## 1. 结论与证据边界

现有客户端不是空壳：资源、可编辑主题、时钟、视频、插件、设备会话和恢复逻辑已有大量实现。主要问题是旧页面组织与 V6.3 的设备上下文、统一内容操作和视觉系统没有完整对齐。只换颜色或把侧栏改成 204px，不能完成迁移。

**建议保留 WPF 客户端和现有业务/输出链路，按 V6.3 重建页面呈现与统一操作入口。WebView2 可以作为待批准的小范围技术验证方案，不建议本轮直接承载整份原型或替换创作编辑器。**

本轮没有修改应用源码、设置、主题格式、设备协议或驱动，没有发送 HID 命令。新增内容仅为原样解包的参考文件、原型审计脚本、截图和本报告。

证据等级：

- **本轮原型运行证据**：Edge/Playwright，1440×1000，实际切换五种场景、点击九个导航页面，截图见 `evidence/`，记录见 `prototype-audit.json`。这是 HTML 的真实浏览器渲染，但数据是原型模拟数据。
- **本轮源码证据**：检查当前工作树，而不是仅看历史文档。HEAD 为 `cd4d4c9efb07d96911f5213cbd3f5ce33f0b1ada`；调查时 git status 约 97 条记录（含本轮新参考目录），存在大量既有未提交改动。HEAD 不能单独代表正在运行的界面。
- **运行客户端截图未取得**：发现 PID 15728，运行路径为 `src/DeskDisplay.App/bin/Debug/net8.0-windows10.0.19041.0/DeskDisplay.App.exe`。Computer Use 两次捕获分别报 `FrameArrived timed out`、`window capture timed out`。没有据此判定应用崩溃，也没有强关或重装应用。
- 旧 `artifacts/v63-wpf-home.png` 属于测试视图渲染，不是本轮真实窗口截图；不可拿它替代用户要求的实窗验收。
- **本报告可用于审核迁移方案，但视觉验收证据未闭环**。逐页面客户端截图、相同数据同状态对照、高 DPI、小窗口和全部弹窗路径仍须补齐。本轮不宣称“全部差异已实测穷尽”或“阶段 A 完全验收通过”。

正式参考 HTML SHA256：`22EE4C6484BF2188893897246C6F7E66F41F8FC3F39756F35A35D2D42CEE8984`。以交付包版本为准，不继续使用桌面的历史副本作为权威。

## 2. 技术架构与目录

| 层 | 当前实现 | 迁移原则 |
|---|---|---|
| 应用入口 | `src/DeskDisplay.App/App.xaml(.cs)`，.NET 8，WPF，WindowsForms 框架引用 | 保留服务组合、生命周期和退出清理 |
| 外壳 | `Views/MainWindow.xaml(.cs)`、`ViewModels/MainViewModel.cs`、`AppPage.cs` | 保留 WindowChrome；重排导航、顶栏、底栏 |
| 页面 | WPF UserControl，页面常驻并切换 Visibility；MVVM 加 code-behind/对话框辅助类 | 重用命令与状态，避免隐藏页面继续高频预览 |
| 样式 | `Themes/Colors/Typography/Spacing/Controls/Cards/Navigation.xaml`，App 合并字典 | 新建 V6.3 命名资源，逐页切换，避免全局隐式样式伤及旧页 |
| 业务模型 | `DeskDisplay.Core` 的 Scene、Theme、Layout、Device、Output | 不为 HTML 另建一套资产、主题或设备模型 |
| 场景与输出 | `DeskDisplay.Display`，SceneView、JPEG 渲染、视频取帧、Windows 副屏、USB | 保留线程/帧队列/取消/心跳边界 |
| 资源与云端 | `DeskDisplay.Library`，WallpaperCatalog、StudioApiClient、ContentListStore、会话与备份 | 保留资源 ID、下载状态、授权、路径解析 |
| 编辑与插件 | CreatorViewModel 多个 partial、CreatorView/画布辅助类、PluginManagerViewModel | 功能比 HTML 演示丰富，重新组织而非删除 |

当前所查 `src/*.csproj` 未发现 WebView2 或 Avalonia 引用。此报告针对本仓库 Windows 客户端，不代表已审计另一台 Mac 的代码。

## 3. 全局视觉差异

| 项目 | V6.3 | 当前源码 | 处理 |
|---|---|---|---|
| 主色 | #4268EF | Colors.xaml #4C85F6；部分 Logo 已单独用 #4268EF | 消除混用 |
| 背景 | #F5F7FB | #F4F7FB | 统一 token |
| 正文 / 次级 | #25314B / #748198 | #202C43 / #8C9AB1 | 统一文本层级 |
| 面板 | 白底、1px 淡边、常用 14px 圆角 | SoftCard 12px、无边框、默认阴影 | 采用原型边界与层次 |
| 外壳 | 204px 分组侧栏、74px 顶栏、底部输出栏 | 主尺寸已接近；单一 ListBox，没有完整分组和底部个人区 | 不是仅调整宽度 |
| 顶栏 | 面包屑、真实设备状态、管理入口 | 在线页仍有前后翻页和搜索顶栏，其他页缺统一设备状态 | 以本次 V6.3 覆盖旧在线页设计要求 |
| 内容卡片 | 原比例、名称/类型覆于封面，悬停操作 | 有 hover 操作，但元信息与容器仍混用旧模板 | 共享 ResourceCard 模板；键盘焦点也可操作 |
| 缩略比例 | 实际素材/选中布局比例，按规格分组 | 壁纸 LibraryAspectRatio 固定横4、竖0.25 | 改用实际宽高，未知尺寸单独呈现 |
| 弹窗 | 同一遮罩、标题、底部操作和反馈 | 多个独立 Window、MessageBox、程序生成控件 | 统一 DialogHost/样式，不改变业务结果判断 |

源码定位：`Themes/Colors.xaml:3`、`Themes/Cards.xaml:4`、`Views/MainWindow.xaml:7`、`ViewModels/WallpaperItemViewModel.cs:40`。

## 4. 逐页面差异与复用计划

下表“现状”主要为源码确认；实时窗口视觉仍待补证。P0 是功能/数据风险，P1 是主要布局/流程偏差，P2 是细节。

| 页面/区域 | 原型目标 | 当前实现与差异 | 可复用/需实现 | 优先级 |
|---|---|---|---|---|
| 导航外壳 | 首页、发现、我的、设备、个人分组 | AppPage 已强类型；旧名称、排序与单列表组织不同；创作编辑隐藏侧栏 | 保留导航状态，重建分组和当前态，统一返回路径 | P1 |
| 首页：单 D92 | 设备名称、真实比例大预览、横竖切换、当前内容操作、列表 chips、最近/热门 | HomeView 已有 SceneView、最近使用和推荐；仍含 D92 专属字段和旧顶区，列表快捷入口不等价于原型 chips | 复用 HomeScene/history/API；添加选中设备的完整呈现模型 | P1 |
| 首页：单 D69 | 同一单设备体验，使用本机实际能力 | 快捷应用已能走 D69，首页状态仍有 D92 依赖；不能开放原型模拟的任意方向/视频 | 复用 ManagedDeviceCard、D69Compatibility；按能力禁用并说明 | P0 |
| 首页：双/三设备 | 每台独立预览/内容/状态 | 目前一个 HomeScene 与 QuickDevice，不是完整多设备首页 | 新增设备卡片集合投影，订阅独立 session 快照 | P0/P1 |
| 首页：无设备/离线 | 连接引导，保留身份与待恢复状态 | 有输出错误和设备集合，但没有本轮确认的专用 V6.3 空态 | 用真实 registry 区分无记录、离线、占用、重连 | P1 |
| 最近使用/热门 | 壁纸、视频、主题真实记录，按设备适配 | 已有混合最近记录；推荐来源不同于原型硬编码排序 | 保留真实记录；无数据为空态，不拿 60 个演示项冒充线上 | P1 |
| 在线资源 | 类型/分类/排序/方向/尺寸、下载状态、详情 | CloudLibraryViewModel 已有 API、分页、取消/重试、收藏、权限；仅 Wallpaper 类型开放，Clock/Plugin/Theme 标为未开放 | 重做布局和卡片，保留服务；不复制原型点下载即成功 | P1 |
| 资源库 | 全部/收藏/下载/创作，方向、规格分组、其他尺寸折叠 | LibraryViewModel 有分类、二级分类、搜索、分页和独立 BrowseOrientation；主题按封面方向，卡片仍固定4:1 | 新增统一资源投影、布局适配分组，保留资产 ID 和分页性能 | P1 |
| 资源详情/应用 | 详情弹窗、多设备选择/批量、明显留边确认 | 本地详情为页面；QuickDeviceUse 原生 ListBox 单选；主题可用 managed 多目标底层但快捷入口未统一 | 共用详情和 ApplyDialog；共享能力/布局解析，不在 UI 另写判断 | P0/P1 |
| 删除/回收站 | 影响设备和列表提示、可恢复、持久化 | LibraryViewModel RemoveCommand 删除资产索引/轮播引用/历史；不删原文件不等于有回收站 | 新增可恢复事务，记录引用/顺序；恢复不自动输出 | P0 |
| 创作首页 | 创建、主题卡片、最近编辑 | 已有模板、真实封面、项目列表、恢复等，比原型丰富 | 保留功能，统一卡片/入口，进阶操作收进更多菜单 | P1 |
| 创作编辑器 | 左图层、中画布、右属性、多布局 | 已实现真实时间、撤销/重做、拖拽缩放、锁定、素材、布局、预览应用；原型仅简化文字/时间 | 保留 WPF 画布和 SceneView，逐区套版；不能退化成 HTML 固定时钟 | P1 |
| 轮播列表 | 左列表、右大缩略行、统一壁纸/视频/主题，心情、单项秒数、暂停/下项 | CarouselManagementView 仍分壁纸/主题/混合；MixedPlaylistViewModel 有持久化、暂停/倒计时/视频结束等；不是原型多命名列表模型 | 统一呈现并逐步迁移列表数据，复用真实调度器；补心情/列表管理/批量添加 | P0/P1 |
| 插件中心 | 插件卡片、配置、加入主题 | PluginManagementView/PluginManagerViewModel 有实际插件管理与配置 | 换布局与状态展示；不照搬模拟行情、安装 toast | P1 |
| 设备管理 | 单设备简化、多设备卡片、命名、识别、方向、独立输出 | DeviceManagementView/ManagedDevicePanel 已有注册表/会话与能力检查；入口和密度不同 | 重排现有功能；识别屏幕属于设备写操作，应显式触发 | P0/P1 |
| 首次连接向导 | 识别→身份→方向/适配→完成 | 现有确认逻辑可复用，未发现完整原型向导闭环证据 | 建向导呈现，禁止无序列号时静默绑定第一台 | P1 |
| 个人中心 | 登录状态与数据入口 | AccountView/StudioAccountViewModel 为真实登录，原型是流程说明 | 保留账号服务/会话隔离，不用模拟会员状态 | P1 |
| 设置 | 启动、托盘、恢复、备份、诊断 | MainWindow 使用 WorkspacePanel，不是独立 SettingsView；有真实工具/备份/恢复 | 重组分区，不将原型“导出规划JSON”代替真实备份 | P1 |
| 底部输出栏 | 每台缩略图、当前内容/列表进度、前后项、停止恢复 | MainWindow 已绑定 Devices.Managed.Cards，显示名称/内容/状态/停止；另有旧 Usb 状态；缺统一缩略、恢复和前后项 | 单一输出投影，静态缩略缓存，可见时有限刷新，不能各自新开输出 | P0/P1 |

## 5. 最重要的流程缺口

### 5.1 内容应用仍分叉

`Views/QuickDeviceUse.cs` 的 D92 壁纸/视频分支修改 `Usb.SelectedSerial` 后进入 `Library.UseWallpaperAsync`；D69 静态素材转为临时 ThemeProject 经 `ManagedDevices.ApplySourceAsync`；保存主题走 `SavedThemeUse`。这解释了“已有多设备底层”并不等于“所有快捷入口都可多设备并行”。旧 USB 正在输出其他序列号时仍会拒绝切换。

迁移应在现有服务之上增加窄接口：以 deviceId、resourceId、layoutId、fitMode 发起应用；返回逐设备结果与会话版本。复用所有权租约和已验证发送实现，不让按钮直接调用 HID。先解决同一设备内容切换与状态回流，再接批量入口。

### 5.2 临时预览不等于普通应用

`OutputExperience.cs:50` 已能保存恢复回调、显示预览状态、结束后恢复；当前证据不足以证明具备原型完整的15秒、保留并使用、列表剩余时间恢复、多设备独立预览事务。需要明确状态机：准备→预览→恢复/保留→完成/恢复失败；恢复必须核对设备和会话版本，避免旧回调覆盖用户后续应用。

### 5.3 方向是三种不同状态

浏览方向只筛选素材；主题布局方向只选择编辑布局；设备方向改变实际 profile。原型中的旋转是模拟值，不能直接作为 HID 旋转字段。先运行 LayoutSelector/Resolver 与能力检查，必要时提示保持原方向、停止后切换或选择替代内容。取消不改设置。

### 5.4 数据操作必须有事务

回收站要保存原资源记录、列表引用与位置、相关源项目关系；不默认删除外部源文件。删除前列出正在使用的设备并处理输出；恢复仅恢复数据，不重启输出。轮播迁移要兼容旧壁纸方案、主题方案和 MixedList，保留原 ID/顺序/秒数，不覆盖原文件。

## 6. WebView2 评估

| 方案 | 优点 | 成本与风险 | 建议 |
|---|---|---|---|
| WPF 页面按原型重建 | 最大化保留 ViewModel、命令、编辑画布和测试；最少平台依赖 | CSS 需转成 XAML 布局和模板；必须截图约束，不能凭感觉近似 | 默认迁移路线 |
| WPF 内局部 WebView2 浏览页 | CSS 布局复用程度高，适合卡片/导航验证 | 新 runtime、消息桥、资源访问、焦点/DPI、预览帧回传与 WPF 混排成本 | 审核后只做一页技术验证，失败可整页回退 |
| 全量 HTML+JS 替换 | 外壳视觉起步快 | 原型多次函数覆盖、模拟持久化和 setInterval 不是生产业务；创作画布/原生视频要重接；双状态源风险大 | 不推荐直接采用 |

WebView2 在 WPF 有官方支持，但标准 HWND 承载存在 airspace 叠层问题，官方提供 CompositionControl 路线；不能据此假设现有画布、弹层和视频能无成本混合。[微软 WPF 文档](https://learn.microsoft.com/en-us/microsoft-edge/webview2/platforms/wpf)

如试验 WebView2：只加载打包的可信本地 UI；命令白名单、版本化 DTO、requestId、参数/来源校验、取消和错误结果；禁止 JS 访问任意路径、HID 句柄或执行系统命令。真实状态由 C# 推送，不能 JS 先把 running=true 当成功。页面销毁不得停止后台 session，也不得重复启动定时器。[微软安全指引](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/security)

跨平台只复用 DTO/规则，不把 WebView2 当作 macOS 已解决方案。该取舍须在实施前审核。

## 7. 模块影响与拟议文件范围

保持不改：`Display/Usb/D92HidDevice.cs`、`D92HidRenderTarget.cs` 的协议与写入；视频 JPEG 管线；设备租约；Core 布局选择/适配规则。UI 通过现有接口使用它们。

重用核心：`Core/Devices/DeviceRegistry.cs`、`DeviceContracts.cs`、`D69Compatibility.cs`，`Core/Output/OutputSessionManager.cs`，`Display/Services/WindowsDeviceDiscovery.cs`、`WindowsThemeSessionFactory.cs`、`ThemeDevicePreview.cs`，`Library/WallpaperCatalog.cs`、`StudioApiClient.cs`、`ContentListStore.cs`。

计划修改（未执行）：

- App/Themes 六个字典及新的 V6.3 资源；MainWindow、HomeView、LibraryView、OnlineResourcesView。
- CarouselManagementView/MixedPlaylistView；CreatorView 和已有属性区；DeviceManagementView/ManagedDevicePanel；PluginManagementView、AccountView、WorkspacePanel。
- MainViewModel/HomeNowPlaying/QuickDeviceUse/OutputExperience：只做统一状态投影与命令接线。
- 可能新增共享 ResourceCard、OutputBar、DeviceSelector、统一 DialogHost 及对应 ViewModel（名称待实施设计）。
- Library：仅在回收站和多列表阶段新增版本化存储；Core 数据契约只做经测试的增量扩展。
- tests：补 UI 场景夹具、输入/焦点、状态转换、事务恢复测试；既有硬件测试独立运行。

## 8. 分阶段计划、验收与回滚

工期是单人有效开发时间粗估，不含等待用户验收/设备可用时间，不能作为交付承诺。

| 阶段 | 范围 | 预计 | 放行条件 | 回滚 |
|---|---|---|---|---|
| A 补证 | 本报告审核；补真实窗口截图、完整核心弹窗操作、版本/工作树基线 | 1–2天 | 原型与客户端同尺寸/同数据/同状态证据，方案确认 | 仅文档，无运行影响 |
| B 外壳与组件 | token、导航、顶栏、卡片、弹窗、底栏骨架 | 2–4天 | 1440×1000 主参考及三种验收尺寸通过，旧页仍可访问 | 独立提交/页面开关回旧样式 |
| C 首页/资源/应用 | 单设备→多设备→在线/本地、适配确认、共享目标与输出状态 | 4–7天 | 应用后首页/底栏/实体屏一致，失败不误报，旧接口回归 | 按页面开关回退，服务不换 |
| D 数据闭环 | 回收站、引用恢复、15秒预览事务、多列表迁移 | 4–7天 | 重启持久化、故障注入、取消/覆盖/断线安全 | 迁移前快照，版本并存，旧文件不覆写 |
| E 创作/轮播/设备等 | 三栏整理、统一轮播、插件/设备/账号/设置 | 4–7天 | 既有功能清单逐项保留，能力边界可见 | 分页提交，旧编辑器可切回 |
| F 验收发布 | UI_PARITY_REPORT、回归、实机与打包 | 2–4天 | 检查清单三维结果明确，重大问题关闭 | 原安装包/数据备份回滚 |

开始 B 前先保护已有脏工作树：记录 diff 与新文件清单、建立可追溯基线后创建迁移分支；不能直接 reset/stash 丢弃其他工作。每个阶段独立提交，只提交该阶段文件。数据库/JSON 回滚与 UI 回滚分别设计，禁止老程序读写不兼容新格式。

## 9. 测试矩阵与发布门槛

- 视觉：1280×800、1440×900、1920×1080，另1440×1000主对照；100%/125%/150%/200% DPI；键盘焦点、hover、弹窗、长名称、空态/加载/错误态。截图须记录像素尺寸和缩放，不能只拉伸比较。
- 场景：无设备、单D92、单D69、双D92横竖、D92×2+D69；可用 VirtualDisplayTarget 验逻辑，但不当实机通过。
- 流程：浏览方向不改设备；应用/取消/留边确认；并行输出；只停一台；预览到期/保留/恢复失败；删除影响和恢复；列表重启恢复；编辑未保存保护。
- 性能：大图库虚拟化/分页、缩略后台解码有界缓存；底栏不全帧无节流刷新；隐藏页面取消预览任务；输出不依赖页面驻留。
- 硬件：复用既有 D92/D69 测试；不因 UI 迁移调整 Report ID、分包、旋转约定。D69 仅现有1280×480逻辑/480×1280传输/90°旋转、静态与时钟范围；视频不能跟随 D92 一起解锁。
- 构建/回归：实施每阶段运行 `dotnet build DeskDisplay.sln --no-restore`；DisplayTests 的 `ui-refactor`、`theme-quick-use` 及相关子集，发布前完整 DisplayTests、SmokeTests、AdaptationTests。测试配置必须隔离用户资料。

本轮原型审计共记录20个状态：五种设备首页、九个导航页面、六个弹窗（回收站、新建列表、添加内容、新建主题、连接设备、资源详情）；最终运行未捕获 JavaScript 页面异常。打开弹窗不等于完成其所有提交/恢复分支。应用适配确认、15秒恢复、方向冲突、编辑离开等分支仍须专门验收。审计中早期导航定位器因换行文本超时，改为页面已有 onclick 属性定位后重跑通过，未改 HTML。

本轮未运行构建或回归：未改应用代码，避免把历史1490项或其他旧数字写成本轮结果。已有双屏实机记录见根目录 `V63_WPF_UI_ACCEPTANCE.md`，它不能代替新 UI 验收。

## 10. 截图索引及待确认事项

原型首页：[单D92](evidence/prototype-home-single92.png)、[单D69](evidence/prototype-home-single69.png)、[双D92](evidence/prototype-home-dual.png)、[三设备](evidence/prototype-home-mixed.png)、[无设备](evidence/prototype-home-empty.png)。

页面：[在线](evidence/prototype-discover.png)、[资源库](evidence/prototype-library.png)、[创作](evidence/prototype-creator.png)、[轮播](evidence/prototype-playlists.png)、[插件](evidence/prototype-plugins.png)、[设备](evidence/prototype-devices.png)、[个人](evidence/prototype-account.png)、[设置](evidence/prototype-settings.png)。弹窗取证结果以 audit JSON 和实际存在的图片为准，不将脚本计划当作成功。

当前客户端对应截图全部标记“待补”，不能生成虚假的差异百分比。先审核 WPF 默认路线、阶段顺序及旧功能保留范围；审核后才开始页面改造。动态时钟、视频、D69 的真实能力以仓库验证结果为准，原型说明中“尚未实现/待验证”的历史描述不能覆盖已有证据。

新版原型的页面源码映射和运行取证状态见 [视觉基线补充](UI_VISUAL_BASELINE.md#新版-mirabox-原型对照)。本报告前文的截图与布局结论对应历史 HTML。

新版原型已构建并采集 15 张浏览器截图。首页预览尺寸缺陷及 WPF 截图缺口记录于视觉基线补充。
