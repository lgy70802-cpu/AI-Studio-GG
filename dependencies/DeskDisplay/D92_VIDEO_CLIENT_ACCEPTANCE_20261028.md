# D92 Windows 客户端独立视频验收 — 2026-10-28

## 已交付

版本 `0.1.0-preview.20261028.1` 已安装并启动。实际窗口控件树确认版本号、D92 已连接、原有使用壁纸及使用主题入口仍在。安装前后 settings.json SHA256 一致；旧程序保留在安装目录的 backup-006e4dc535734acc80ae6fbffb18ea84。

我的资源库导入可解码的 MP4 后可点击“使用视频”，直接向选定 D92 输出。最近使用支持同一入口，首页显示实际输出源的未旋转帧，支持暂停、继续及停止。视频默认静音循环，USB 请求上限 5 FPS，等比完整显示。暂停冻结解码画面但仍发送缓存 JPEG，不代表停止 USB 通信。

替换内容前先准备首帧；文件缺失或解码失败保留原输出。成功应用视频后停止旧的单设备轮播计时器，避免随后覆盖视频；既有托管的其他设备会话保持独立。静态图与视频可直接切换，无须手动停止。发送失败可显式重试，停止会取消准备过程并等待解码器及输出释放。

未修改已验证 HID 报文结构。UI 不执行 HID 写入；解码在独立 STA，最多一个待处理首页帧通知，不积压托管输出帧。

## 实机证据

使用正式客户端 ViewModel、临时资源库和临时配置运行 180 秒验收，实际 USB 设备 C511D3782B05。包含视频、暂停/继续、静态替换与重新播放。最后 Streaming 计数 750 帧、2 次连接，ReconnectObserved=true。用户确认：“画面正常，拔插后自动恢复”。

证据：`artifacts/video-client-hardware-repeat-b63760e8a34547fa93d486d6c7fd7fa6.json`、同名 `.user-confirmation.json`、`artifacts/video-client-hardware-repeat.log`。

此前 120 秒运行用户未观察，末段断开且未记录恢复，不计为目视或重连通过。180 秒实机运行后修复了循环解码等待新帧事件的约束；该修复经过原生视频及完整自动化回归，未重新进行相同时长实机运行。主机帧计数不等于精密测量的面板刷新率。

## 验证结果

- 最终 Windows 回归：1294 PASS，退出 0；`artifacts/video-client-release-regression.log`。
- 视频客户端专项：16 PASS，退出 0；`artifacts/video-client-final-special.log`。
- 适配检查 95、配置冒烟 19、打包检查 7 均通过。
- Release 发布成功；488 个文件的大小及 SHA256 校验通过。安装证据：`artifacts/video-client-installed.json`。
- 首轮完整回归发现循环位置可能读到上一轮表面；保留失败日志 `artifacts/video-client-regression-initial.log`，修复为等待新的原生帧事件，保留严格图像断言，后续完整回归通过。
- 已核对安装后真实窗口控件树；本次没有新增截图或多 DPI 视觉验收。

## 主要源码

- `src/DeskDisplay.Core/Models/VideoFrameScene.cs`：平台无关输出帧场景。
- `src/DeskDisplay.Display/Services/VideoJpegFrameSource.cs`：冻结预览帧、暂停、私有 STA 编码。
- `src/DeskDisplay.Display/Services/WindowsLiveVideoFrameSource.cs`：连续原生取帧及新帧等待。
- `src/DeskDisplay.Display/Views/SceneView.cs`：实际视频帧预览。
- `src/DeskDisplay.App/ViewModels/UsbVideoOutput.cs`、`UsbOutputViewModel.cs`：准备、替换、独立输出、暂停、取消与重试。
- `LibraryViewModel.cs`、`HomeNowPlaying.cs`、`HomeRecentEntry.cs` 及相应 Views：直接使用视频和首页同步。
- `tests/DeskDisplay.DisplayTests/ClientVideoTests.cs`：正式客户端链路模拟及显式实机模式。

## 使用方式与边界

打开新版 → 我的资源库 → 导入 MP4 → 使用视频；播放时在首页暂停/继续或停止，也可以直接使用另一份静态壁纸或主题。

视频组合主题、视频轮播、启动自动恢复视频、并行多设备视频尚未开放。未验证长时间运行内存稳定性、睡眠唤醒、所有视频编码格式或更高刷新率。只有一次外部内存采样，不能据此声明无内存泄漏。macOS 视频未在本轮实现。

发布包：`artifacts/releases/D92Studio-0.1.0-preview.20261028.1-win-x64.zip`。本轮未提交或推送 Git。
