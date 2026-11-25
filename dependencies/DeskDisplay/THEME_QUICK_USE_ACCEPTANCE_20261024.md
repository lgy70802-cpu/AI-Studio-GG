# 已保存主题快速使用（2026-10-24）

## 使用路径

1. 创作中心保存主题。首页项目卡片新增“使用主题”；编辑器顶部“资源库主题”可查看已保存版本，不丢弃当前编辑。
2. 我的资源库选择“主题”，或在“全部”中查看独立的已保存主题区。卡片提供使用、编辑、收藏；两列、横图4:1/竖图1:4、等比完整显示，每页最多12项。壁纸继续使用原有分页加载，不混写壁纸资产数据。
3. 点击“使用主题”打开现有按设备预览与应用窗口：核对布局、真实设备和参数后应用。本软件管理的会话已有输出时，可先点击“停止该设备输出”，再应用新主题；不自动抢占外部软件设备。
4. 成功接受输出会话后，主题加入首页“最近使用”，与壁纸按实际使用时间混排，最多6项。点击封面或“使用主题”可再次打开预览与应用。保存、浏览和失败应用不产生使用记录。

这不是无条件跳过设备确认的一键输出。设备确认、能力检查、素材/插件依赖失败、D92 USB 视频限制仍沿用现有实现。

## 实现

- ThemeProjectStore 新增 `ReadSavedAsync`、`ReadUsageAsync`、`UpdateUsageAsync`：仍以原项目ID和文件为准；`Themes/usage.json` 仅存收藏和最后使用时间，不存素材副本。原子临时文件替换、现有gate串行读改写；损坏元数据保留，拒绝覆盖。
- CreatorQuickUse 提供读取已保存副本、记录使用、收藏及资源库入口；刷新 Projects 同步通知资源库与首页。
- SavedThemeUse 读取磁盘的已保存主题，复用 CreatorDevicePreviewViewModel；不加载进当前编辑器、不修改未保存内容、横竖布局或撤销历史。
- ManagedDevicesViewModel 在 StartAsync 返回 Started 后记录使用；失败设备不记录。此记录代表会话已接受，不把它当成首帧或持续运行的证明。元数据写入失败独立记录，不中断已启动设备。
- 原编辑器应用、主题轮播也在成功启动后接入使用记录。历史删除源项目后自动排除，不将本地主题标为已购买或已下载。
- HomeRecentEntry 保留对 WallpaperItemViewModel 的直接嵌套绑定，壁纸异步缩略图仍能更新。主题封面加载后释放文件流，避免占用导致保存/删除失败。

## 修改文件

- src/DeskDisplay.Library/ThemeUsageStore.cs（新）
- src/DeskDisplay.App/ViewModels/CreatorQuickUse.cs（新）、HomeRecentEntry.cs（新）
- src/DeskDisplay.App/ViewModels/CreatorViewModel.cs、CreatorThemePlaylist.cs、LibraryViewModel.cs、MainViewModel.cs、ManagedDevicesViewModel.cs
- src/DeskDisplay.App/App.xaml.cs
- src/DeskDisplay.App/Views/SavedThemeUse.cs（新）、CreatorQuickUse.cs（新）
- src/DeskDisplay.App/Views/CreatorView.xaml、LibraryView.xaml及代码后台、HomeView.xaml及代码后台、CreatorDevicePreview.xaml
- tests/DeskDisplay.DisplayTests/ThemeQuickUseTests.cs（新）、ManagedDevicePageTests.cs、Program.cs

没有修改 HID 协议、驱动、固件、Core 场景格式或已有壁纸资源库文件。

## 验证

| 检查 | 结果与证据 |
|---|---|
| 构建 | 0警告0错误，artifacts/quick-use-build.log |
| Windows完整回归 | 862项通过，artifacts/quick-use-regression-final.log |
| 收尾专项 | 19项通过，artifacts/theme-quick-use-tests-final.log；在首页封面点击恢复及截图底色收尾后执行 |
| 适配与会话 | 95项通过，artifacts/quick-use-adaptation.log；虚拟设备 |
| 配置 | 19项通过，artifacts/quick-use-smoke.log |
| Release | 自包含win-x64发布成功，artifacts/quick-use-publish-final.log |

专项覆盖保存后同步入库、保存不记使用、读取磁盘副本与脏编辑隔离、持久化恢复、收藏/搜索/范围、分页边界、使用时间排序、删除排除、真实WPF按钮及损坏使用元数据保留。既有设备测试增加成功/失败设备的使用回调检查。没有把模拟输出当作新实机验收。

离屏WPF截图已检查（使用隔离临时主题及真实渲染控件，并非用户资源截图）：

- artifacts/theme-quick-use-library.png
- artifacts/theme-quick-use-home.png

当前安装版本 `0.1.0-preview.20261024.3`，安装标记与EXE版本一致；已正常打开固定目录 `%LOCALAPPDATA%\D92Studio\app\DeskDisplay.App.exe`。控件树核对版本、D92已连接及新的首页最近记录结构；进程2828 Responding=True。用户旧窗口正常退出，没有强杀或丢弃编辑；安装保留先前app备份及APPDATA配置、项目和资源。

安装脚本已成功执行；外层一次检查误用了旧的 LASTEXITCODE，随后以实际安装标记和EXE版本重新核对确认成功，不将该包装检查误报为安装失败。

包：`artifacts/releases/D92Studio-0.1.0-preview.20261024.3-win-x64.zip`。

SHA256：`1E7B403ECE6FD63A0EF66947C942BA0DA738368BDEF26882384CDDC444600B3C`。

远程Computer Use鼠标定位返回 `coordinate input geometry is unavailable`，无法完成新入口的真实鼠标点击演练；已使用真实WPF控件与模型回归，窗口启动与连接状态用控件树核对。本轮未进行新入口的实体输出、多设备实机或长期运行验收，上一轮实体发布链路验收记录仍单独保留。未提交/推送，原有工作区变更保留。

## 后续

当前复用完整设备预览窗口；更紧凑的快速切换弹层、跨重启记住上次预览目标、正在使用主题的卡片角标仍可继续优化。主题轮播复用原入口，本轮未加入新的资源卡片轮播按钮。新用法支持静态图和现有可用实时组件，不能据此声称视频或外部插件分发已支持。
