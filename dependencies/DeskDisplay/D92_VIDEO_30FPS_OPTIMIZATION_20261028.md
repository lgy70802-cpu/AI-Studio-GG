# D92 视频 30 FPS 目标优化

## 实现

- 精确匹配目标尺寸时跳过 WPF RenderTargetBitmap 合成与重复读回；其他尺寸仍等比完整显示及黑边合成，不拉伸。
- 90/180/270°正交旋转先物化像素，避免 JPEG 编码时执行惰性变换；保留 JPEG 质量90及1920×462实测输出尺寸。
- 连续取帧可使用编码/发送期间新到达的合并事件；启动及循环回跳时仍等候新事件，避免旧画面跨循环。
- 新增共享 VideoFramePipeline：最多预备一帧，编码与当前JPEG发送重叠；没有多帧FIFO积压，不并发写HID。超过150ms的预备画面丢弃重取，断线恢复时追到当前播放位置。
- JPEG、未旋转预览和采样指标作为同一个快照传递，首页不会误用下一帧预览。暂停先收敛预备帧，停止取消并等待流水线及解码器。
- 高频会话成对请求/释放1ms计时精度，结束后释放请求，不常驻请求。
- 正式客户端提供5/10/20/30 FPS，默认目标30。设备管理的USB设置中停止输出后可选；为本次运行参数，重新启动默认30。客户端仍不开放视频主题、视频轮播或开机视频恢复。

默认D92输出调用仍保留100ms间隔保护；独立视频调用显式允许到30FPS，不更改协议报文、心跳、租约、整帧顺序及写入超时。没有依赖新视频库或改变固件。

## 基准与实机

同一30FPS内容变化、约3秒循环的真实MP4，JPEG质量90：

| 阶段 | 实际主机完成FPS |
|---|---:|
| 优化前60秒D92实机 | 11.53 |
| 跳过重复合成/物化旋转，10秒离线 | 20.29 |
| 合并帧事件，10秒离线 | 21.56 |
| 单帧预备及计时精度，10秒离线 | 29.67 |
| 首轮优化60秒D92实机 | 28.77 |
| 正式客户端共享流水线60秒D92实机 | 28.90 |

首轮优化实机完成1726帧、60.00秒、Failed=false，证据`artifacts/video30-optimized-hardware-9c874c2449ef4f8f843f9e077c36ee20/result.json`。后续将相同策略提取到共享流水线，正式客户端链路验收结果单独记录，不混同这次工具测量。

正式客户端共享流水线实测：1735帧 / 60.0257秒 = 28.9043 FPS，没有Failed/BlockedByCraft状态。包含暂停/继续、缺失/损坏替换保留原输出、静态切换、再次视频以及停止释放，测试进程退出0。证据`artifacts/video30-client-hardware-8ab5159ec2f54adcb3cc4b7ea1605667.json`和`artifacts/video30-client-hardware.log`。

2026-10-29 用户分别对上述优化工具实测与正式客户端链路实测回复“正常”。两轮目视确认现均已补齐，各自保存独立user-confirmation.json，不修改原始测量结果，也不将目视正常视为精密面板FPS测量。

这是接近30FPS的主机完整JPEG写入计数，不能宣称锁定30FPS、面板精密刷新率或所有素材均达标。复杂内容、不同源分辨率/比例及USB负载可能降低实际速度。

## 测试及边界

完整Windows回归1308 PASS，退出0（`artifacts/video30-full-regression.log`）。包括非对称像素旋转方向、连续循环、预备帧过期丢弃、暂停精确快照、重复释放、30FPS及5/10/20回退、失败重试和停止取消。随后资源释放结构整理通过正式客户端构建及实机链路检查；最终结果见下方追加记录。

2026-10-29已补30分钟正式输出ViewModel+真实HID持续运行：1802.339秒、50,501帧、平均28.0197FPS，退出0，无失败，停止释放通过，用户确认画面正常。详见`D92_VIDEO_30FPS_SOAK_ACCEPTANCE_20261029.md`。这是隔离测试进程，不等于完整可视UI长测。

仍未进行数小时长测、30FPS拔插、睡眠唤醒、多设备高频输出、macOS视频或所有编码格式验证；用户明确要求跳过本轮拔插。低帧率旧版拔插通过不能替代30FPS拔插验收。原有用户配置和资源不覆盖；不提交或推送Git。

## 发布安装

`0.1.0-preview.20261028.2` 已安装并打开。488个文件大小及SHA256校验通过，安装前后settings.json SHA256一致，原程序保留在`C:\Users\Administrator\AppData\Local\D92Studio\backup-e3dfb19ae3c14aec9550aa86758c5090`。见`artifacts/video30-installed.json`及`artifacts/video30-publish.log`。

实际窗口控件树确认20261028.2版本、D92已连接及原有使用视频/主题/壁纸入口。远程点击设备设置两次返回`coordinate input geometry is unavailable`，按恢复流程刷新后仍失败，停止UI输入；没有声称完成设置页的人工视觉验收或提供新截图。

使用：我的资源库或首页点击“使用视频”，默认目标30FPS。调整档位：停止输出 → 设备管理 → USB直连屏幕 → 视频目标帧率，选择5/10/20/30，再使用视频。

## 主要文件

- `src/DeskDisplay.Display/Services/VideoJpegFrameSource.cs`
- `src/DeskDisplay.Display/Services/WindowsLiveVideoFrameSource.cs`
- `src/DeskDisplay.Display/Services/VideoFramePipeline.cs`
- `src/DeskDisplay.Display/Usb/D92HidRenderTarget.cs`
- `src/DeskDisplay.App/ViewModels/UsbVideoOutput.cs`
- `src/DeskDisplay.App/ViewModels/UsbOutputViewModel.cs`
- `src/DeskDisplay.App/Views/UsbOutputView.xaml`
- `tests/DeskDisplay.DisplayTests/VideoFramePrototypeTests.cs`
- `tests/DeskDisplay.DisplayTests/ClientVideoTests.cs`
- `tools/DeskDisplay.VideoPrototype/Program.cs`
