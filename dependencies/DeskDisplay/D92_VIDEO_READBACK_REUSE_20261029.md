# 视频读回目标帧复用实验（2026-10-29）

## 改动

`WindowsLiveVideoFrameSource`每个解码会话持有一个BGRA8、Ignore alpha的CPU `VideoFrame`，GPU画面通过`VideoFrame.CopyToAsync`复制到该帧，再写入调用者提供的托管数组。替代每次`SoftwareBitmap.CreateCopyFromSurfaceAsync`创建新的目标位图。目标帧在打开失败或会话关闭时释放。

该改动只复用API层目标帧，不能据此声称驱动内部没有分配或GPU读回被消除。输出仍为真实解码画面；预览和暂停快照保持独立，不改变HID协议、单写者、整帧取消、质量或目标帧率。

接口依据：[Microsoft VideoFrame.CopyToAsync](https://learn.microsoft.com/en-us/uwp/api/windows.media.videoframe.copytoasync)，[VideoFrame类型与构造方法](https://learn.microsoft.com/en-us/uwp/api/windows.media.videoframe)。本轮已在本机验证该GPU表面到CPU目标帧的组合可用，不等同所有显卡或驱动均已验证。

## 离线ABBA

沿用20261029.7代码为基线，原生比例真实MP4＋实时时钟，1920×462、旋转90°、质量90、24FPS；每组15秒，无HID，诊断开启。

| 顺序 | FPS | 间隔P99 | 读回平均 / P95 | Gen2次数 | GC暂停合计 |
|---|---:|---:|---:|---:|---:|
| 旧A | 24.13 | 44.04ms | 5.34 / 6.52ms | 480 | 169.05ms |
| 新A | 24.13 | 42.93ms | 5.01 / 5.92ms | 479 | 178.09ms |
| 新B | 24.00 | 48.65ms | 5.18 / 6.35ms | 473 | 169.83ms |
| 旧B | 24.13 | 44.96ms | 5.39 / 6.60ms | 480 | 173.09ms |

读回平均减少约0.2–0.3ms，托管分配约266KB/帧降至262KB/帧，FPS和GC基本持平，P99没有一致改善。不是卡顿根因解决，也不是30FPS验收。

## 正确性与边界

缓冲专项通过，覆盖数组切片边界、真实红蓝视频BGRA通道与不透明度、独立历史快照、暂停、取消、在途关闭和重复释放。完整回归1435项通过、退出0，工具与测试构建0警告0错误。完整日志`artifacts/video-readback-regression.log`。

按照用户此前授权正常关闭安装版，进程已结束，没有强杀。实机对照在唯一设备检查处失败（`Unique connected target required`），尚未发送HID画面；随后只读HID枚举为`[]`，Windows即插即用列表也未发现VID5548/PID1011。原始日志`hardware-before.log`及`device-list.log`保留。缺少本轮实机性能、视觉、拔插与长测，不作通过声明。

当前改动仍为未发布候选，未打包/安装新版本，安装版保持20261029.7。设置SHA256仍`2E256A832DFE8607540ECF3AC7A326617F43746E01F19B159A551EF9A2EFEC1F`。设备重新连接后先做相同素材参数的旧→新实机对照，再决定是否发布；不能拿离线结果代替。

后续状态（2026-10-30）：设备恢复后已补齐各60秒实机对照，旧24.00FPS/P99 53.02ms，新24.07FPS/P99 53.75ms，无错误、无明显提速。该候选随20261030.1发布；详见`D92_VIDEO_MODES_ACCEPTANCE_20261030.md`。此前缺设备失败记录保持不变。

证据目录：`artifacts/video-readback-reuse-1793287040624/`；汇总`comparison.json`，含各组原始结果。用户设置、资源和已保存主题不参与实验写入。
