# V6.3 迁移视觉基线（阶段 A 补证）

日期：2026-11-04。用户已批准保留 .NET 8/WPF、按 V6.3 迁移，暂不全量 WebView2。本文件记录基线和未完成证据，不是阶段 B 完成报告。

## 1. 安全基线已建立

- 原分支：`feature/multi-device-foundation`。
- 当前迁移分支：`ui/v63-shell-stage-b-20261104`。
- HEAD：`cd4d4c9efb07d96911f5213cbd3f5ce33f0b1ada`。工作区含大量既有未提交变更，不能将 HEAD 当作当前完整实现。
- 独立恢复目录：`E:\Codex\MLB\migration-baselines\v63-20261104-220441`。
- `workspace.zip`：备份 Git 已跟踪及未忽略的新文件，797 个文件，逐项 SHA-256 回读验证成功。
- `manifest.json`：文件路径和 SHA-256；`working.patch`、`staged.patch`、`status.txt`、`head.txt`、`branch.txt`：原工作区/暂存区和版本状态。
- 忽略的 bin/obj、用户 AppData 和外部素材不在这份源码快照内。本轮未修改这些用户数据；本快照也不能替代用户数据备份。
- 未执行 reset、clean、stash；未提交其他任务修改；没有上传或推送仓库。

未来提交 UI 修改前，应将“本轮修改后的文件”与本备份内的同名文件比较，形成独立增量。对于已有修改的 MainWindow、Navigation 等文件，不能直接整文件 git add 后声称提交只含 UI 迁移。若需要可独立构建的完整历史，应另行建立明确标记的前置工作基线提交，不能伪装为阶段 B 修改。

## 2. 真实截图排查结果

当前窗口进程 PID 15728，标题 DeskDisplay，路径：
`E:\Codex\MLB\MLB-DeskDisplay\src\DeskDisplay.App\bin\Debug\net8.0-windows10.0.19041.0\DeskDisplay.App.exe`。

观察顺序：

1. 工具成功枚举到窗口。
2. 首次无截图读取返回 `window is minimized; call activate_window, refresh with get_window, then retry get_window_state`。
3. 调用恢复窗口并重新取得句柄后，返回 `no screenshot targets found`。
4. 重新枚举并再次捕获，返回 `window capture timed out: timed out waiting on channel`。
5. 系统进程查询 `Responding=True`。这只表明消息响应检查通过，不证明所有页面正常，也不解释截图超时根因。

结论：最小化是本次发现的一个障碍，但恢复后问题未解决。可能与当前桌面会话或捕获链有关，尚无足够证据归因于 GPU、WPF 渲染或远程桌面。未强制关闭应用、重装、修改渲染模式或安全设置。

客户端 XAML 默认 Window Width=1440、Height=1000，manifest 声明 PerMonitorV2；**这是设计单位和能力声明，不是实际截图尺寸/DPI测量值**。本轮实际窗口尺寸、所在显示器 DPI 均未取得可靠证据，标记待补，不能填成96 DPI。

## 3. 逐页面对照矩阵

HTML 图片来自交付包原型的真实浏览器运行，1440×1000 CSS viewport。客户端图片列全部待补；下列差异是源码分析，尚非像素验收结论。详细业务差异见 UI_GAP_REPORT.md。

| 页面 | HTML 图 | WPF 图（待补文件名） | 布局/组件差异 | 交互差异及复用 | 拟修改文件（src/DeskDisplay.App 下） |
|---|---|---|---|---|---|
| 首页 | [单屏](evidence/prototype-home-single92.png)、[多屏](evidence/prototype-home-mixed.png) | wpf-home.png | 旧设备顶区与单 HomeScene；缺完整多设备卡片与轮播chips | 保留真实 SceneView/最近记录；设备上下文适配放阶段 C | Views/HomeView.xaml、ViewModels/HomeNowPlaying.cs、MainViewModel.cs |
| 在线资源 | [原型](evidence/prototype-discover.png) | wpf-online.png | 旧顶栏搜索/分类/卡片，与V6.3内容区组织不同 | 保留真实下载/权限/取消/重试；未开放类型继续禁用 | Views/OnlineResourcesView.xaml、ViewModels/CloudLibraryViewModel.cs |
| 资源库 | [原型](evidence/prototype-library.png) | wpf-library.png | 固定4:1/1:4，缺真实规格分组与其他尺寸折叠 | 方向筛选复用；回收站不能以现有移除操作代替 | Views/LibraryView.xaml、ViewModels/LibraryViewModel.cs、WallpaperItemViewModel.cs |
| 创作首页 | [原型](evidence/prototype-creator.png) | wpf-creator.png | 旧模板/项目卡片及操作分区 | 保留模板、草稿恢复、真实封面；功能不能删减 | Views/CreatorView.xaml、ViewModels/CreatorProjectManagement.cs |
| 主题编辑器 | [原型](evidence/prototype-editor.png) | wpf-editor.png | 真实编辑器比模拟版复杂，需对齐三栏与公共组件 | 保留拖拽缩放/时钟/撤销/布局/视频；仅公共样式属B | Views/CreatorView.xaml、CreatorInspector.cs、CreatorWorkbench.cs |
| 轮播 | [原型](evidence/prototype-playlists.png) | wpf-playlists.png | 原型左列表右大预览，客户端多种轮播分栏 | 保留调度器，列表统一属于后续业务阶段 | Views/CarouselManagementView.xaml、MixedPlaylistView.xaml |
| 插件 | [原型](evidence/prototype-plugins.png) | wpf-plugins.png | 旧管理布局/配置入口 | 保留真实插件，不照搬模拟数据 | Views/PluginManagementView.xaml、ViewModels/PluginManagerViewModel.cs |
| 设备管理 | [原型](evidence/prototype-devices.png) | wpf-devices.png | 单/多设备视觉分支不完整 | 保留注册表/独立会话/能力限制；B不改发现与协议 | Views/DeviceManagementView.xaml、ManagedDevicePanel.xaml |
| 个人中心 | [原型](evidence/prototype-account.png) | wpf-account.png | 实际账号表单与原型说明不同 | 保留登录与服务端权益；未实现服务明确显示 | Views/AccountView.xaml、ViewModels/StudioAccountViewModel.cs |
| 设置 | [原型](evidence/prototype-settings.png) | wpf-settings.png | 当前使用WorkspacePanel，分区不同 | 保留启动/托盘/备份/诊断功能 | Views/WorkspacePanel.xaml、ViewModels/WorkspaceViewModel.cs |

## 4. 公共组件的阶段 B 边界

| 组件 | 当前差异 | B允许修改 | B不允许借机修改 |
|---|---|---|---|
| Token | 主色#4C85F6、背景#F4F7FB、卡片12px无边/阴影，与原型不一致 | 按原型#4268EF/#F5F7FB/#25314B/#748198/#E5EAF2，统一字体/圆角/间距 | 设备尺寸、JPEG尺寸、主题画布默认值 |
| 导航 | 204px/74px外形已接近，分组/排序/个人区不同 | 分组、选中态、悬停、顶栏真实状态布局 | 删除路由、关闭现有业务入口 |
| 底栏 | 有设备状态和停止，缺统一缩略/操作结构 | 对现有状态/命令做视图绑定，未具备命令标明不可用 | 新写输出调度器、模拟播放成功、硬件协议 |
| 资源卡片 | 模板重复、元信息与悬停行为不统一 | 公共视觉模板、焦点态、按钮间距 | 阶段C规格分组与资源查询重写 |
| 按钮/标签/弹窗 | 多种旧样式及程序生成Window | 统一样式键与已存在命令接线 | 改授权、删除语义、会话所有权 |

每个组件须保留“原型截图、修改前WPF实窗、修改后WPF实窗、尺寸/DPI/状态记录”。功能尚未接入时不能只为截图显示成功状态。

## 5. 自动捕获失败后的手动补图办法

1. 在实际运行 D92 Studio 的 Windows 桌面操作，点击任务栏图标恢复窗口，确保不处于最小化、锁屏或其他窗口遮挡状态。无需停止设备输出或清空数据。
2. 打开系统“设置→系统→显示”，只查看并记录所在显示器的缩放比例与分辨率，不必更改。常规映射：100%=96、125%=120、150%=144、200%=192 DPI；自定义缩放需要另记实际值。
3. 使用窗口普通状态，目标为 **1440×1000物理像素的完整应用窗口截图**。用 Alt+PrintScreen 捕获当前窗口，粘贴到画图，查看图片像素尺寸；拖动窗口边缘后重复直至符合。不要在画图中缩放截图凑成1440×1000。若当前桌面不足以容纳此尺寸，记录实际尺寸并说明限制。
4. 逐个打开上表页面，使用相同窗口位置和尺寸截图，PNG保存到 `docs/ui-reference/evidence/wpf-baseline/`，采用表中名称。主题编辑器打开现有主题即可，不修改、不保存；进入前保存用户已有未保存编辑。
5. 每张图附记录：页面、窗口像素宽高、系统缩放、显示器、时间、应用版本、单/多设备状态。不要截入密码、令牌或私人账号信息；个人页可在未登录状态采集，但应注明数据状态不同。
6. 应用内真实数据与HTML演示数据不同时，只评估结构/样式差异，不能输出像素一致率。后续可用明确标记的隔离测试数据驱动真实客户端窗口做严格同状态对照，但不得用离屏RenderTargetBitmap替代桌面截图。

可以只先补首页一张，确认捕获路径有效后再批量补其余页。补图完成前，阶段 B 保持未开始。

## 6. 恢复办法

本轮没有应用代码修改，通常无需恢复。源码快照应先解压到**新的空目录**，按manifest核对，再选择需要恢复的文件；不要整包覆盖现有工作区或AppData。恢复Git原分支前先确认本轮新文档是否需要保留，禁止用reset/clean丢弃它们。后续 UI 回滚只逆向本轮增量，不逆向基线之前的业务修改。

## 7. 当前门槛

- 安全分支/可恢复源码快照：完成。
- 原型截图：十个页面（含编辑器）、五种设备首页及六个弹窗已留图；完整交互分支仍需补。
- 客户端实窗截图、实际DPI、逐页面视觉核验：未完成，捕获失败。
- 阶段B开发：未开始，遵守“阶段A补证完成后”条件。
- 未生成声称阶段B完成的UI_PARITY_REPORT_B.md；不进入阶段C。


## 2026-11-05 继续检查

- Release重新构建成功，0警告/0错误，见evidence/baseline-build-retry.log。首次构建因本轮测试进程占用复制目标失败，已在测试退出后串行重建。
- 重建后的ui-refactor日志包含960×640、1320×900布局及键盘焦点PASS，见evidence/baseline-ui-tests-verified.log；当前会话无法取回原进程退出码，不扩大为完整回归通过。
- 当日客户端原本未运行，已打开原Debug客户端。重新枚举真实窗口后仍捕获失败；不是仅原窗口最小化导致。实际截图/DPI尚未取得，仍不能放行阶段B。

## 新版 MiraBox 原型对照

现行标准见 [CURRENT_UI_STANDARD.md](CURRENT_UI_STANDARD.md)。上面的 HTML 图保留为历史记录。新版源码与 WPF 的对照入口如下。最终布局以新版浏览器实拍为准。

| 页面 | React 基准源码 | WPF 对照 |
|---|---|---|
| 外壳和输出栏 | App.tsx、index.css | MainWindow.xaml、Themes |
| 首页 | OverviewPage.tsx、ScreenBox.tsx | HomeView.xaml |
| 在线资源 | DiscoverPage.tsx、ResourceCard.tsx | OnlineResourcesView.xaml |
| 资源库 | LibraryPage.tsx、ResourceCard.tsx、Modals.tsx | LibraryView.xaml |
| 创作及编辑器 | CreatorAndEditorPage.tsx、EditorModals.tsx | CreatorView.xaml |
| 轮播 | PlaylistsPage.tsx | CarouselManagementView.xaml、MixedPlaylistView.xaml |
| 设备 | DevicesPage.tsx、DeviceWizardAndPickers.tsx | DeviceManagementView.xaml、ManagedDevicePanel.xaml |
| 插件、个人、设置 | OtherPages.tsx | PluginManagementView.xaml、AccountView.xaml、WorkspacePanel.xaml |

源码已确认新版外壳侧栏采用 w-52、固定定位，导航含独立轮播及插件入口。精确尺寸须取浏览器计算值。新版构建与截图仍待本机依赖安装完成。WPF 实窗截图和实际 DPI 仍待补，阶段 B 的放行条件维持原要求。

新版运行取证已完成。隔离副本仅安装浏览器构建所需依赖，沿用原型声明的版本范围。Vite 8.3.5 构建成功，报告一条未来配置加载兼容提示。Edge Playwright 以 1440×1000 采集了 [首页](evidence/mirabox/overview.png)、[在线资源](evidence/mirabox/discover.png)、[资源库](evidence/mirabox/library.png)、[创作中心](evidence/mirabox/creator.png)、[编辑器](evidence/mirabox/editor.png)、[轮播](evidence/mirabox/playlists.png)、[插件](evidence/mirabox/plugins.png)、[设备](evidence/mirabox/devices.png)、[个人](evidence/mirabox/account.png)、[设置](evidence/mirabox/settings.png)及五种首页设备场景。运行记录见 [runtime.json](evidence/mirabox/runtime.json)。

首页实拍显示约 208px 固定侧栏、64px 顶栏、底部常驻输出栏。近期内容三列，热门内容四列。默认设备主预览缩成一点。ScreenBox 的根节点只有比例和最大尺寸限制，内部内容绝对定位，首页调用未提供可确定的尺寸。WPF 对照时应依据容器与设备比例计算有效预览区域，并使用真实 SceneView。

本轮 WPF 首页重新捕获返回 window capture timed out。WPF 实窗图和实际 DPI 仍缺，阶段 B 尚未放行。手动补图步骤见本文第 5 节。
