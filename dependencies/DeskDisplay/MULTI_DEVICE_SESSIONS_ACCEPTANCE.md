# 多设备输出会话底座：阶段 2

日期：2026-10-22。分支：`feature/multi-device-foundation`。

后续状态：设备UI已接入，当前默认启用发现与页面、仍无自动输出，`DESKDISPLAY_MULTI_DEVICE=0`可回退。见 `MULTI_DEVICE_PAGE_ACCEPTANCE.md`。下文保留阶段2交付时默认关闭、尚无新设备页的历史范围。

## 本阶段交付

已实现会话管理、Windows 输出适配与离线测试。新会话运行时以环境变量 `DESKDISPLAY_MULTI_DEVICE=1` 显式启用，默认关闭；启用时只进行设备发现，不自动开始新的输出。设备管理卡片、多选应用、编辑器设备预览仍属于下一阶段。

正常界面仍使用原有入口；兼容入口继续保留原有单会话与 USB/Windows 切换行为。新会话与兼容入口在底层共享设备锁，因此它们不能通过不同入口同时占用同一已识别端点。没有覆盖用户设置、保存 V2 主题、修改 HID 报文字节、分包、唤醒、心跳或完整帧写入超时。

## 文件与职责

| 文件 | 实现 |
|---|---|
| `src/DeskDisplay.Core/Devices/DeviceRegistry.cs` | 成功的完整快照替换、传输隔离、缺失设备离线、稳定身份保留、连接 epoch、统一设备键 |
| `src/DeskDisplay.Core/Output/DeviceLease.cs` | 平台无关租约接口、线程安全进程内原子互斥 |
| `src/DeskDisplay.Core/Output/OutputSessionManager.cs` | 每设备准备/运行/等待/停止/失败，独立取消和驱动释放，异步准备后的 epoch 复核，旧回调隔离，失败独立记录 |
| `src/DeskDisplay.Display/Services/WindowsDeviceLeaseProvider.cs` | 同一 Windows 用户下的文件句柄锁；进程退出由 OS 释放；各设备不同锁，不依赖有线程归属限制的 Mutex |
| `src/DeskDisplay.Display/Services/WindowsDeviceDiscovery.cs` | 后台 USB 与系统副屏发现，失败保留上次快照；重复 HID 序列号、重复显示源标记歧义 |
| `src/DeskDisplay.Display/Services/WindowsThemeSessionFactory.cs` | 每会话独立 ThemeScene、JPEG renderer 或窗口；复用 D92HidRenderTarget、SceneView、图片解析和现有插件数据 |
| `src/DeskDisplay.Display/Services/SessionImagePool.cs` | 活动会话共享冻结图片，引用计数释放，串行解码，最多 256 MiB 保留图片预算；文件检查/解码在后台 |
| `src/DeskDisplay.Core/Models/ThemeScene.cs` | 可选只读 ResolvedLayout，旧主题路径不要求提供 |
| `src/DeskDisplay.Display/Views/SceneView.Theme.cs` | 按解析后的等比变换绘制原始图层；字体、阴影、居中图片和插件共同等比缩放，保留原有绘制链路 |
| `src/DeskDisplay.Display/Usb/D92HidRenderTarget.cs` | 在原连接循环外加入设备租约检查，最后关闭连接后释放；等待重连期间保留租约；不改协议 |
| `src/DeskDisplay.Display/Services/RenderWindowService.cs` | 兼容窗口检查同一设备租约，支持借用管理器持有的租约，释放窗口不提前释放借用租约 |
| `src/DeskDisplay.App/App.xaml.cs` | 开关控制装配 Registry/Factory/Manager/Discovery，复用本地资源 ID 查路径和启用的插件数据，正常退出等待全部会话和发现任务 |
| `tests/DeskDisplay.AdaptationTests/SessionTests.cs` | 真实会话管理器配虚拟驱动的生命周期/竞争/失败测试 |
| `tests/DeskDisplay.DisplayTests/MultiDeviceSessionTests.cs` | Windows 跨进程锁、旧入口冲突、真实 WPF/JPEG 与可控发送端、系统显示器替身、图片池测试 |

## 输出与资源隔离

- 每次 Start 产生自己的 SessionId、取消令牌、解析布局、场景、渲染器和目标生命周期；后台运行驱动，所有 WPF 绘制/释放回到 Dispatcher。
- 设备租约在异步准备前原子取得；准备失败、取消、运行失败、清理失败均结束该设备的结果，其他设备继续。停止等待驱动结束和资源清理后才释放租约。
- D92 继续使用原有拉取式发送循环，一帧完成后才请求下一帧，不增加历史帧队列。当前保持每秒一帧；明确低于此吞吐能力的配置会拒绝，等待独立验证更低频心跳调度，不擅自提高帧率。
- 时间从真实系统时间更新；股票共享既有数据源，但复制行情数据到本会话 StockScene；相机时钟复制配置到本会话对象，不让其他会话释放同一个可变插件 Scene。
- 图片引用复用现有资源 ID 和路径解析；不新建资产数据库或下载权益。图片最后一个引用释放即从池中移除。
- 系统显示器按已选路径重新枚举，丢失时仅关闭自己的窗口并等待；恢复相同身份且尺寸未变时重新创建窗口。变成主屏、身份/镜像有歧义或尺寸变化时不会静默投到其他屏；尺寸变化要求重新适配。
- 新路径当前支持静态图片、文字、形状、数字时钟/日期/星期和启用的股票/相机组件；未迁入视频播放，旧视频能力仍走原入口。仅有 StillImage 证据不能启动实时组件。
- Started 表示准备完成并已调度，不代表已生成首帧、成功写入硬件或用户确认画面；Streaming 和实机视觉验收要分别记录。

## 运行与验证

```powershell
dotnet build DeskDisplay.sln --nologo -v quiet
dotnet run --project tests/DeskDisplay.AdaptationTests --no-build
dotnet run --project tests/DeskDisplay.DisplayTests --no-build
dotnet run --project tests/DeskDisplay.SmokeTests --no-build
```

只运行 Windows 会话检查可在同一个 PowerShell 中设置：

```powershell
$env:DESKDISPLAY_TEST_SUITE = 'multi-device'
dotnet run --project tests/DeskDisplay.DisplayTests --no-build
Remove-Item Env:DESKDISPLAY_TEST_SUITE
```

测试没有向实体 D92 发送命令。它们验证的是：

1. 真实 Core 管理器驱动虚拟目标：并发竞争只有一个拥有者、同主题不同尺寸、目标等待/故障不取消其他目标、准备取消/失败释放、停止等待清理、过期回调不污染新会话、关闭后拒绝启动、释放失败不会挂住。
2. 真实 Windows 文件句柄：第二个 Provider 和真正的子进程不能获取已持有锁，不同设备可独立获取，释放后可恢复。
3. 旧 HID 入口在设备锁已占用时，在枚举/渲染/发送前拒绝；旧窗口入口在创建窗口前拒绝。
4. 两套独立 WPF 渲染器实际生成 80×320（320×80 旋转90°）及 640×160 JPEG；检查真实像素坐标与真实系统时间的下一帧变化。
5. 两套系统窗口驱动使用可控显示器列表和窗口替身，验证各自的 ThemeScene、尺寸、断开、逻辑占用、模拟恢复与单独停止。
6. 冻结图片共享，释放一个引用不影响另一个，最后引用移除缓存。

完整回归曾发现新测试把“准备完成”误当作“发送端和首帧已经创建”；已改为预设独立目标及显式等待首帧，不依赖线程调度顺序。

本轮日志位于 `artifacts/session-build.log`、`session-adaptation.log`、`session-wpf.log`、`session-regression.log` 和 `session-smoke.log`。最终计数见文末验收记录。

最终验收记录：解决方案构建 **0 警告、0 错误**；平台无关适配/会话测试 **95 项通过**（含阶段1的63项）；Windows完整回归 **640项 PASS**（含新增会话检查29项）；配置 SmokeTests **19项 PASS**。最终完整回归进程退出码0。上述测试均未向实体D92发送指令。

## 验收边界和下一步

- 这不是双 D92 实机验收。没有运行新的单设备/双设备 HID、真实多副屏拓扑、小时级内存或 macOS 测试。本阶段代码与离线验证完成后，硬件门槛仍需补齐。
- 当前锁限于使用同一用户目录锁协议的 Studio 进程；旧版进程和其他厂商软件不会主动遵守。保留 Craft 检查，但不宣称能检测任意第三方占用，也不执行终止进程或强制抢占。
- USB 与 Windows 显示器是否对应同一物理屏，尚未建立人工确认的持久关联；不能只凭名称/分辨率合并。这一项与无唯一标识设备的人工确认 UI、跨端点冲突组仍待后续实现，不宣称所有物理冲突已经解决。
- Registry 的连接 epoch 是发现快照版本；会话快照保留启动时的 epoch，D92 在同一会话内的连接次数仍由原发送器日志记录。尚未将每次 HID 打开与发现 epoch 做逐帧关联认证。
- WPF 绘制和 JPEG 编码仍在 UI Dispatcher，仅调度为后台优先级；尚未承诺任意设备数量下无卡顿。256 MiB 是图片池预算，不是整个进程内存上限；每会话 Bitmap/JPEG 和长期资源趋势须另测。
- 完整设备管理 UI、多设备选择/批量结果、编辑器目标预览、自动恢复策略持久化、V2 写回/导出及跨平台驱动未在此阶段接入。原配置和主题继续保留，下一阶段先上设备卡片与明确的逐设备控制。

未提交或推送 Git，没有重新发布或重启用户正在运行的软件。
