# D92 共享场景与自动重连测试

此窗口复用 DeskDisplay 的 `ClockScene`、`ClockSceneDataProvider` 和 `SceneView`。预览和 USB JPEG 渲染读取**同一个场景实例**，仅输出端旋转像素。它是独立诊断入口；日常使用可在主应用“USB屏”页面配置输出。

```powershell
dotnet run --project tools/DeskDisplay.UsbDemo -c Release -- --list
dotnet run --project tools/DeskDisplay.UsbDemo -c Release -- --self-test
dotnet run --project tools/DeskDisplay.UsbDemo -c Release -- --snapshot artifacts/d92-shared-scene-plan.json artifacts/clock-snapshot.jpg
dotnet run --project tools/DeskDisplay.UsbDemo -c Release -- artifacts/d92-shared-scene-plan.json
```

`--snapshot` 只生成共享渲染器的 JPEG，不访问 USB，可用于与已确认的测试图比较。

诊断计划可指定 JpegQuality；省略时使用生产默认值 90。其他质量值只供兼容性诊断，不能因为 Windows 解码通过就认为 D92 支持。

`--send-static <plan.json> <image.jpg>` 用生产 USB 发送模块重放固定图像，绕过 Scene 渲染，用于区分图像与传输问题。此模式要求计划的 DurationSeconds 为 1–60，仍按序列号识别且要求退出 Craft。

计划文件使用设备真实序列号、逻辑画布宽高、像素旋转角度及运行时长，不依赖 Windows 显示器编号。当前实验计划的参数来自之前已验证的设备配置，不是程序内写死的分辨率。运行时长允许 10–1800 秒，结束或关闭窗口时取消输出并释放句柄。

使用前退出 Craft。程序检测到 Craft 主进程运行会停止输出，不自动结束其他进程。连接丢失时关闭旧句柄，每秒重新枚举同一序列号，重新读取设备能力；不复用过期的 HID 路径，不自动选择另一个设备。

生产发送模块现已在连接后唤醒屏幕，并每 10 秒发送心跳。`--wake <serial>` 可只发送一次唤醒命令来区分待机与图像问题。命令格式来自本机厂商库的离线检查，其中单次唤醒已由实体屏幕恢复显示验证。只读写入成功日志不足以验证实体画面。

## 实现位置

- `src/DeskDisplay.Display/Usb/D92HidDevice.cs`：只读枚举、稳定身份匹配、能力查询与 HID 连接。
- `D92FrameEncoder.cs`：已用厂商 fixtures 验证的 CRT/DRA 编码；原 ProtocolProbe 也复用这份实现。
- `D92HidRenderTarget.cs`：取消、每帧写入超时、断连释放、重连与结构化状态日志。一次只等待一帧，丢线期间不积压旧帧。
- `SceneJpegRenderer.cs`：UI 线程上的共享场景离屏渲染，旋转及 JPEG 编码。USB 写入在后台任务中执行，不访问传感器或音频设备。

当前 Alpha 支持时钟、图片和性能场景的渲染；本测试窗口只演示实时钟。视频没有接入 USB 输出。实际 HID 写入只支持已验证的 D92 协议，不包含固件升级、闪存上传或未知的设备控制命令。

## 验证边界

离线渲染检查覆盖场景变化产生新像素、任意画布尺寸、传输旋转不改变共享场景、非法参数和释放后拒绝工作。协议编码继续通过 33 个厂商对照、50 个分包边界和 8 个非法输入检查。

完整解决方案构建通过；显示回归测试应使用测试 EXE（需要其 Per-Monitor V2 清单），而不是直接用 dotnet 加载测试 DLL。测试输出保存到 `artifacts/d92-display-regression-tests.txt`。

短时实机日志在 `artifacts/d92-shared-scene-live.jsonl`。一次 5 分钟测试只能验证本机短时表现，不代表长时间运行、所有固件和所有画布尺寸均已完成验证。厂商监控服务与驱动仍保留。


## 可编辑组合主题验收

`--theme <plan.json> <source.theme.json>` 读取真实ThemeProject，复用ThemeScene、生产JPEG渲染和HID目标，时钟按系统时间更新。诊断支持文字、时钟、形状和可解析路径的静态图片；拒绝视频和依赖主应用实例的插件。路径应指向真实存在的本地素材，不能用固定JPEG假装动态主题。

本轮横屏原配置短时发送通过；交换逻辑宽高并保持原旋转的竖屏持续刷新失败。详见仓库CREATOR_CENTER_ADVANCED_ACCEPTANCE.md和artifacts/creator-device-*.jsonl。诊断退出码0只表示生命周期正常结束，不表示没有中途等待、自动重连或视觉问题。不能因此改变已验证协议，也不要把设计画布方向自动当成设备宽高交换。
