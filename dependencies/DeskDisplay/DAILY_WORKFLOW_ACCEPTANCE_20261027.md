# 日常使用闭环：六项升级（2026-10-27）

交付版本：0.1.0-preview.20261027.2。20261027.1 是中间包，不作为安装交付。

## 功能与入口

1. **实机输出验收**：复用现有 HardwareAcceptance、Scene、USB 输出和轮播链路，未修改 HID 协议。120.50 秒测试完成；静态画面及 A/B 实时时钟主题输出，记录 5 次主题变化，暂停、继续、手动下一项均执行，无传输失败，用户 settings.json 不变。末次会话记录 120 帧，不能当成整个测试的总帧数。未检测到 USB 拔插；方向、文字、秒数的目视确认仍待用户回复，不能仅凭写入成功宣称画面正确。
2. **首页当前内容**：主预览上方显示设备、内容名称/类型、正在输出或等待重连；未运行时明确标记本地预览。提供定位资源及单独停止入口。USB 状态读取实际输出 Scene，浏览其他资源不改变其内容说明。临时预览保留独立标识。
3. **统一轮播入口**：首页轮播管理页分“壁纸轮播 / 主题轮播”两个标签。创作中心主题轮播入口也导航到该页。复用既有壁纸排序、随机、方案、暂停、上下项及主题停留秒数、排序、暂停/继续/下一项能力；主题列表提示项目/素材缺失，实际播放仍逐项检查目标能力。两套队列保留，尚不支持混编成一个壁纸与主题交错播放的列表。
4. **备份迁移**：设置 → 备份已保存数据 / 导入备份。备份已保存主题、模板、引用图片/视频、分类标签、收藏历史、轮播与已知设置。素材按哈希去重并流式读写，单包上限 4 GiB、元数据总量上限 128 MB。导入先校验清单/哈希/路径和版本并显示冲突、缺失数量，用户确认后准备下次启动恢复。恢复保留现有 settings.json、同 ID 主题和轮播配置，资源库合并新增 ID；旧元数据和新增文件在异常时回滚。新机器不自动启用登录启动、设备输出或复用旧机器确认信息。不含凭据、设备锁、日志、插件可执行程序与系统字体。未保存编辑不冒充正式主题备份。
5. **性能**：资源库搜索输入增加 250 ms 合并；诊断页显示最近/最大筛选提交耗时，并保留已有 UI 调度及 JPEG 指标。5000 条模拟元数据反复筛选、三屏加载与翻页检查通过，专项样本最大约 11 ms；不含图片解码、网络、HID，不代表所有卡顿已消失。显示卡片数量仍有界，未增加无界后台队列。
6. **版本入口**：设置显示当前版本及更新内容，可打开实际程序目录。选择本地更新 ZIP 后校验产品、架构、版本、完整文件清单及 SHA256，拒绝降级、越界路径和未声明程序；通过后打开新暂存目录，提示保存退出后执行 Install.ps1。未伪造线上更新 API，也未自动执行用户选择包中的脚本。哈希校验是完整性检查，不是发布者签名验证。

## 实际数据验证

- 用户真实数据备份约 1156.60 MiB，686 个文件，1 个原本缺失素材；已再次校验全部文件。
- 同一备份恢复到隔离工作区目录：686 个文件、0 个冲突、1 个原本缺失素材，现有用户目录未用于恢复测试。
- 备份位置见 `artifacts/workflow-real-backup.json`；隔离恢复位置见 `artifacts/workflow-real-restore.json`。这些本地个人数据产物位于已忽略的 artifacts，不提交到 Git。
- 初版 512 MiB 内存实现不能备份现有数据，已中止并替换为流式实现。旧失败日志不能视为最终结果。

## 自动化与证据

- 改造前 Windows 回归 1198 PASS：`artifacts/workflow-baseline.log`。
- 最终构建：`artifacts/workflow-build.log`。
- 最终 Windows 回归：`artifacts/workflow-streaming-regression.log`。
- 配置/适配/打包：`artifacts/workflow-smoke.log`、`workflow-adaptation.log`、`workflow-packaging.log`。
- 专项：`UserWorkflowTests.cs`（便携素材、凭据排除、冲突合并、路径越界、篡改、异常回滚、版本比较、降级与额外文件）；`PlaylistRecoveryTests.cs`（真实 WPF 控件、实际输出源、独立停止、5000 条元数据）。专项 USB 目标为模拟器，不计作实机通过。
- 实机：`artifacts/workflow-hardware-20261027/result.json`、`events.jsonl`，只有同序列号设备经过既有能力检查后才输出，用户设置只读。
- 真实发布包校验：`artifacts/workflow-real-package-check.log`，不执行安装包代码。

## 主要修改文件

- App：`ViewModels/HomeNowPlaying.cs`、`ClientToolsViewModel.cs`、`OutputExperience.cs`、`ManagedDevicesViewModel.cs`、`LibraryViewModel.cs`、`CreatorThemePlaylist.cs`、`CreatorViewModel.cs`；`Views/HomeView.xaml`、`WorkspacePanel.xaml`、`CarouselManagementView.xaml(.cs)`、`CreatorThemePlaylist.cs`；`App.xaml.cs`。
- Library：`UserDataBackup.cs`、`LocalUpdatePackage.cs`。
- Tests：`UserWorkflowTests.cs`、`PlaylistRecoveryTests.cs`、`Program.cs`。

## 尚需人工验收

- 本版实机方向、文字、秒数以及拔插恢复。
- 多台真实 D92 并行输出与独立停止；本轮单设备验收不能替代双设备实测。
- 不同 Windows 实际 DPI 的人工操作。WPF 构建/控件回归不等同于修改系统 DPI 后的实测。
- 在线更新服务与发布者签名、壁纸和主题混排轮播尚未实现。


最终结果：构建 0 警告/0 错误；Windows 1221、适配 95、配置 19、打包 7 项全部通过。20261027.2 已安装并打开，配置哈希不变。首页真实控件树确认新入口；原生截图超时、坐标几何不可用，未进行新版本原生鼠标交互视觉验收。


## 2026-10-27 用户补充实机确认
用户确认：方向、文字和秒数正常，USB 拔插后恢复。作为独立人工观察记录，证据 artifacts/workflow-hardware-20261027/user-confirmation.json。本轮自动测试日志未捕获断开/重连事件，保留其原始 reconnectEvidence=false，不回填或伪造日志。目视与人工拔插验收已得到用户确认；日志级重连证据仍未补齐。
