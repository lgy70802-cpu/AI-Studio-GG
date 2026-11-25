# 视频功能与体验第1–6项交付记录

## 当前状态

版本`0.1.0-preview.20261029.1`已安装并打开，488个发布文件大小及SHA256校验通过。实际窗口控件树确认新版版本及D92已连接；进程Responding=true。安装前后settings.json SHA256一致，旧程序保留为`backup-249a2ea110104e2a8cfee74b34422d60`。见`artifacts/video-features-installed.json`。

## 1. 视频混合轮播

- 复用现有混合轮播及资源ID，不创建第二套素材数据库。壁纸、MP4和已保存主题可加入同一列表。
- 混合轮播可按10–86400秒定时切换，视频或包含视频的主题可勾选“视频播完切换”。根据实际选中布局检查视频图层，使用原生媒体时钟循环回跳计数触发下一项。
- 暂停只暂停自动切换，当前视频及实时时钟继续运行。暂停期间视频完成会记录，继续切换时进入下一项；其他设备或其他资源的事件不会推进当前列表。
- 原壁纸轮播不再排除视频；轮播内部应用视频不会误停自己的调度器。缺失、损坏文件仍跳过。
- `MixedEntry.PlayToEnd`为可选字段，旧v1列表缺省false，保留原定时语义；内容仍使用Wallpaper/Theme引用，视频类型从已有资源元数据解析。

使用：首页 → 轮播管理 → 混合轮播，选择已确认设备，加入资源并设置时长/播完切换。

## 2. 创作中心视频背景主题

- 编辑器已有MP4图层现在可通过USB主题会话输出，复用原ThemeProject、LayoutProfile、图层位置/透明度/填充/裁剪、数字时钟、日期、文字、形状及现有插件。
- 每个布局支持一个可见视频；多个可见视频及错误的图层类型在准备时明确拒绝。隐藏图层不启动解码。
- 后台STA连续解码并合成，时钟使用真实系统时间；股票数据与相机时钟由现有插件源提供快照，不伪造业务数据。没有将主题替换成固定JPEG或修改源项目。
- Native帧、组件合成、旋转及JPEG编码使用同一输出链。首页接收冻结的实际合成帧，停止自己的独立视频预览播放器，防止时间线不一致和多余播放。
- 实际帧率取决于视频和图层开销；不承诺组合主题达到独立视频约28FPS的实测结果。质量90保持不变。

使用：创作中心添加MP4素材，叠加时钟等图层，保存后“完成并使用”，选择D92应用。

## 3. 按设备保存帧率

- `AppSettings.UsbVideoPreferences`按序列号保存5/10/20/30FPS及自动降档开关。
- 旧设置没有该字段时默认30FPS、自动降档关闭；非法FPS归一为30。加载不会覆盖用户原设备设置。
- 切换目标恢复各自偏好，设置通过现有串行合并更新服务落盘，保留其他设备和配置。关闭时等待保存完成。
- 独立视频和受管视频主题均使用相同设备偏好；新选择在下一次播放/应用时生效。

使用：设备管理 → 原有D92 USB输出 → 选择设备 → 视频目标帧率。

## 4. 实际帧率与可选降档

- 首页和USB输出页展示主机完整帧发送FPS、所选目标及当前上限，不冒充面板刷新率。
- 自动降档须用户开启。连续5个一秒统计窗口低于当前上限80%，按30→20→10→5降低一档，不降低JPEG质量、不改写所选目标；下次播放从用户目标重新开始。
- 暂停或断线重连清空低速窗口，避免将停顿误判为吞吐不足。始终保持最多预备一帧、过期帧丢弃、整帧串行写入。

## 5. 启动恢复视频

- 成功输出的独立视频记录为Video恢复项，主题/混合列表继续复用现有恢复记录。视频暂停状态及设备偏好可恢复。
- 原“启动时恢复上次D92内容”仍为可选开关，不自动替用户开启。
- 文件丢失、未连接、身份不唯一、未确认、占用或用户已有新选择时明确报告，不擅自改选设备/抢占外部软件；可用统一重试入口在条件恢复后重试。
- 新视频准备失败保留现有输出。已释放受管主题后新视频首帧失败时，尝试恢复捕获的同设备主题；过期恢复不会覆盖新接受的内容。

## 6. 验证与尚未验证部分

- Windows完整回归：1345 PASS，退出0，`artifacts/video-next-confirmed-regression.log`。
- 新视频/客户端/混合资源专项：77 PASS，退出0，`artifacts/video-next-loop-final-special.log`（随后新增的回退保护用例包含在1345项完整回归中）。
- 适配95、配置19、打包7均通过，对应`artifacts/video-next-{adaptation,smoke,packaging}.log`。
- 真实Windows原生MP4离线检查：实际红蓝视频像素、实时组件合成、单次旋转、独立解码重建、两个可见视频拒绝、素材丢失拒绝、保持源项目、释放租约。
- 模拟传输断开：等待期间停止发送并保持设备租约，恢复后仍只有一个写入会话；停止后可重新打开原生解码器。另验证暂停、内容切换、偏好重载、启动视频恢复和缺失设备拒绝改选。
- 首次全回归遇到旧“USB不支持视频主题”断言，更新为新的缺失/未解码素材保护断言；新循环测试发现等待新事件期间的媒体回跳漏计，修复后保留实际像素/循环严格条件，并通过全回归。旧失败日志保留，未降低图像断言。
- 新功能实机短测已通过（`video-feature-hardware`显式测试模式）：静态→视频时钟主题→静态，使用已保存USB尺寸/旋转，不写用户资源库或设置。视频主题阶段在首帧后测量30.0005秒，完成474帧，平均15.7998FPS；JPEG均值约127KB，平均写入20.74ms。自动降档已实际从目标30调到当前上限20。Success=true、Error=null、测试退出0，切换及释放无冲突。证据`artifacts/video-feature-hardware-3a44bdec466246f1b50b193b54a50877/result.json`和`artifacts/video-feature-hardware-repeat.log`。
- 此结果明确暴露新视频主题的合成开销，不等同此前独立视频约28FPS。当前组合主题未达到30FPS，仍需优化WPF合成/编码。2026-10-29用户对本轮方向、视频、秒数及黑屏/花屏检查回复“正常”，目视确认已通过；独立保存user-confirmation.json，原始结果PhysicalDisplayConfirmed=false不改写，不将目视正常等同精密刷新率测量。
- 首次实机运行因D92 Studio仍在运行而在发送前退出，未抢占设备；该失败保护日志`artifacts/video-feature-hardware.log`保留。用户随后确认退出，才进行上述实测和安装。
- 用户此前明确要求跳过拔插，本轮不再要求操作；真实30FPS拔插和睡眠唤醒仍待验收，不自动让电脑睡眠。此前独立视频30分钟结果不能替代新视频主题或完整可视UI长测。

## 主要修改文件

- Core：`UsbVideoPreferences.cs`、`AppSettings.cs`、`ThemeScene.cs`、`OutputSessionManager.cs`。
- Display：`ThemeVideoComposition.cs`、`VideoJpegFrameSource.cs`、`WindowsLiveVideoFrameSource.cs`、`WindowsThemeSessionFactory.cs`、`SceneJpegRenderer.cs`、`SceneView.Theme.cs`。
- App：`UsbVideoOutput.cs`、`UsbOutputViewModel.cs`、`ManagedDevicesViewModel.cs`、`MixedPlaylistViewModel.cs`、`CreatorViewModel.cs`、`CreatorStudioTools.cs`、`OutputExperience.cs`、`D92StartupRestore.cs`、`App.xaml.cs`及首页/USB/混合轮播视图。
- Library：`ContentListStore.cs`保留原ID及列表格式。
- Tests：`VideoExperienceTests.cs`、`ClientVideoTests.cs`、`ContentExperienceTests.cs`、`VideoFeatureHardwareTests.cs`及旧断言更新。

发布包：`artifacts/releases/D92Studio-0.1.0-preview.20261029.1-win-x64.zip`。未提交或推送Git，未更改已验证HID报文或固件。
