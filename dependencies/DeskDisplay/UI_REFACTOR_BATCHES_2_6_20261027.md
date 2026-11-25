# 全局 UI 重构：第 2–6 批交付记录

本次沿用既有 WPF/MVVM、场景与输出服务，逐页整理已有界面；不是重新开发客户端。此前待安装的 20261027.3 功能也包含在本轮源码中。

## 页面与入口

| 区域 | 本次调整 | 原有能力 |
| --- | --- | --- |
| 主窗口 | 像素对齐；侧栏焦点提示 | 68 DIP 导航、窗口拖动/缩放/最大化、输出状态栏保留 |
| 首页 | 设备信息与操作可换行；卡片标题和按钮分行；预览减少不必要最小留白 | 实际场景同步、设备选择、方向、轮播/设置、定位/停止、直接使用、常用/最近/推荐保留 |
| 我的资源库 | 来源与分类统一为可换行筛选；壁纸卡片文字与操作分区；批量移除危险色 | 双列、完整缩略图、原分页/三屏加载、状态记忆、批量修改、直接使用保留 |
| 在线资源 | 两级分类、范围共用筛选样式 | 侧栏资源子菜单、顶部搜索/前后导航、比例、排序、布局、API/下载/授权流程保留 |
| 创作中心 | 表单接入公共样式；属性面板 260 DIP；左右面板拖动调宽；草稿丢弃危险色 | 模板、素材/图层、撤销重做、数字时钟、横竖布局、保存/草稿、设备预览、完成并使用保留 |
| 预览与应用 | 长标题截断，关闭操作不再挤占标题；统一选择器 | 临时预览恢复、正式应用、多设备与适配折叠区保留 |
| 轮播 | 三种播放标签统一；主题与混合表格、选择器和按钮统一；表格列收紧 | 壁纸/主题/混合各自引擎、排序、时长、随机、暂停、下一项、停止保留 |
| 设备/插件 | 选择器和旧页面按钮统一 | 稳定身份确认、能力校验、设备独立状态与输出、插件真实配置保留 |
| 个人中心 | 宽度由固定值改为上限，内容可滚动 | 真实登录/注册/会员状态/退出保留 |
| 设置 | 常规、资源与备份、版本与更新、运行诊断四组 | 托盘/登录启动/恢复开关、资源检查、备份导入、本地更新校验、日志与性能数据保留 |
| 辅助窗口 | 资源检查表格和操作、保存提示危险操作统一 | 原有检查、修复确认与保存/取消行为保留 |

## 公共样式

`Themes/Controls.xaml` 新增 SoftField、FilterItem/FilterList、SectionTab/SectionTabs、SoftDataGrid，保留已有键。文本输入保持系统模板及编辑能力；可编辑颜色/字体 ComboBox 保留原编辑模板，不套用只读选择器。

不删除局部资源字典，以兼容独立窗口和无 Application 实例的测试。未新增第三方 UI 框架、素材数据库或硬编码线上资源。

## 修改文件

- Themes：Controls.xaml、Navigation.xaml（Colors.xaml 的正文与按钮资源承接第一批）。
- Views：MainWindow、HomeView、LibraryView、OnlineResourcesView、CreatorView、CreatorDevicePreview、AccountView、CarouselManagementView、MixedPlaylistView、ManagedDevicePanel、DevicesView、UsbOutputView、PluginManagementView、WorkspacePanel 的 XAML。
- 代码创建面板：CreatorInspector.cs、CreatorWorkbench.cs、CreatorThemePlaylist.cs、CreatorSavePrompt.cs、ResourceMaintenance.cs；CreatorView.xaml.cs 的属性面板恢复宽度同步。
- ClientToolsViewModel.cs：版本说明更新。
- UiRefactorTests.cs、Program.cs：新增布局/可用性检查并纳入全回归。

## 验证与限制

- 第 2、3、4、5 批均执行应用编译，日志为 artifacts/ui-batch2-build.log 至 ui-batch5-build.log；第 5 批曾出现同一 ComboBox 重复 Style 定义，已修复后重新编译通过。
- 专项 15 项检查通过：960×640、1320×900 首页操作不溢出；四组设置存在可滚动区域；双面板编辑画布可用；调宽入口可见；实际按钮键盘焦点可见。日志 artifacts/ui-layout-special.log。
- 隔离运行使用临时目录、模拟输出服务，不读取或覆盖用户主题，也不发送 HID 指令。原生工具读取到真实运行的首页控件树。
- 截图先报 FrameArrived timed out，重新选择并激活后再次报 window capture timed out；因此没有新截图，不宣称完成像素级视觉验收。
- 尚未分别切换 Windows 100/125/150/200% 系统 DPI；窗口尺寸检查不能替代多 DPI 实测。
- 真实云端连通、设备屏幕目视和多设备 HID 不属于本轮新增验收；不扩大 USB VideoScene 能力。

完整回归与安装状态在检查完成后追加，不将旧版本测试计作本轮通过。

## 最终自动检查与打包
- Windows 完整回归 1258 PASS（含新增15项 UI检查），退出码0；适配95、配置19、打包7项均通过。
- 分批应用构建及最终打包成功；第一次测试构建误用了不存在的测试辅助方法，删除多余调用后专项及全量回归通过，没有改低断言。
- 包版本0.1.0-preview.20261027.4；488个负载文件的长度和SHA256校验通过，记录 artifacts/ui-package-check.json。
- ZIP：artifacts/releases/D92Studio-0.1.0-preview.20261027.4-win-x64.zip；摘要：artifacts/ui-refactor-summary.json。
- 当前安装标记仍是20261027.2，客户端运行中，已请求用户保存关闭；尚未安装.4，未覆盖当前程序或用户设置。第6批仍有截图及多DPI人工验收待补。

## 已安装并打开
用户确认保存关闭后，安装20261027.4；settings.json安装前后SHA256一致，旧app备份保留。实际窗口控件树确认版本20261027.4、首页D92已连接、使用壁纸与使用主题入口可见。进程21268响应正常。记录 artifacts/ui-installed.json。未新增HID输出或补称截图/DPI验收。
