# D92 视频原型与传输测量工具

阶段1–2开发工具，不是正式客户端视频入口。使用现有D92图像协议，默认仅按显式命令执行；`--list`只读枚举，不打开写入连接。

## P0 分段测量与固定JPEG曲线

计划可增加 `"CaptureDiagnostics": true`，输出取帧等待、表面复制、GPU读回、像素复制、Bitmap创建、合成、旋转、JPEG编码/复制的p50/p95/p99；`"ComposeClock": true` 使用真实系统时间的诊断组合主题，不写入用户资源库。默认关闭详细计时，不改变正式客户端设置。

统计记录有界保存在内存中，阶段结束统一写盘。`ProcessAllocatedBytesPerSecond`与`ProcessGen2Collections`覆盖整个诊断进程，包含统计开销；它们不能直接代表单个线程或正式客户端。NativeRead内含NativeWait/Readback等子项，Composition内含混合等子项，不能重复相加。GenerationMs对同一帧的原有解码/编码计时求和，不包含HID。

```powershell
dotnet run --project tools/DeskDisplay.VideoPrototype -- --make-hid-fixtures plan.json artifacts/p0-fixtures-new
dotnet run --project tools/DeskDisplay.VideoPrototype -- --run-real-device artifacts/p0-fixtures-new/plan-60000.json artifacts/p0-hid-new/60000
# 同理执行 plan-80000.json、plan-100000.json、plan-120000.json，分别使用不同证据目录
dotnet run --project tools/DeskDisplay.VideoPrototype -- --analyze-hid artifacts/p0-hid-new artifacts/p0-curve-new
```

生成器选择接近60000/80000/100000/120000字节的合法JPEG，保留实际字节数、质量与旋转后尺寸，不补零。测试计划保留显式序列号及原有占用/租约检查。既有30FPS安全上限不变，故这是30FPS请求下的独立写入耗时曲线，**不是无节流极限压测，也不是面板呈现测量**。每个计划最长60秒，建议15秒/档。开始后停止仍沿用整帧发送与独立超时，不更改协议。

分析输出按实际ReportLength计入32字节帧头计算report数，拟合截距、每report斜率及R²。R²较低或截距为负时不能将斜率解释为固定USB轮询周期。读取bInterval需要另行取得真实USB端点描述符；本工具不从HID报告长度猜测端点属性。

## 运行

在项目根目录安装既有 .NET 8 SDK 后执行：

```powershell
dotnet run --project tools/DeskDisplay.VideoPrototype -- --list
dotnet run --project tools/DeskDisplay.VideoPrototype -- --offline plan.json artifacts/video-offline-new
dotnet run --project tools/DeskDisplay.VideoPrototype -- --run-real-device plan.json artifacts/video-hid-new
```

证据目录必须不存在，避免覆盖原始结果。真实输出前要求唯一序列号，D92 Studio/CRAFT/旧演示与验收程序退出，并通过原有租约与设备占用检查。不能使用另外一台设备代替选定序列号。

示例计划，VideoPath改为实际MP4绝对路径，SerialNumber用只读枚举结果：

```json
{
  "VideoPath": "E:\\Videos\\test.mp4",
  "Width": 1920,
  "Height": 462,
  "Rotation": 90,
  "SecondsPerStage": 8,
  "RequestedFps": [1, 2, 5, 10],
  "Backend": "ContinuousFrameServer",
  "SerialNumber": "从枚举结果填写"
}
```

宽高与旋转需来自已确认设备配置；示例值只适用于既有D92验证配置，不能用于未知型号。每档3–60秒，最多4档，1–10 FPS；保留现有HID最短100ms间隔保护。

显式诊断计划可设置 `"ExperimentalHighFrameRate": true`，允许最高请求30 FPS，使用与客户端共享的单帧预备流水线和会话计时精度。默认HID调用仍保留100ms下限；新版客户端独立视频显式允许5/10/20/30档。保持整帧串行写入、心跳、租约及超时机制，不修改协议。请求档位不能作为实际帧率或面板能力声明。

## 区分两类测量

1. **真实视频播放**：不填写BaselineJpegs。Backend选ContinuousFrameServer时，Windows播放器连续解码，输出时读取最新GPU画面，等比合成目标画布、旋转一次、编码JPEG，再发送。TimestampSampling保留原先按时间点采样方式作对照；未填写Backend时沿用这个较慢的对照后端。没有待发帧队列，慢时跳过过期画面，不慢放。
2. **传输基线**：在计划增加BaselineJpegs数组，填2–4张预编码JPEG路径。图像必须符合旋转后的输出尺寸；测量时交替发送，排除解码/编码开销。此模式不是视频播放，不得拿其结果冒充真实视频FPS。

```json
"BaselineJpegs": ["E:\\test\\frame-a.jpg", "E:\\test\\frame-b.jpg"]
```

工具检查JPEG格式、尺寸及单帧16MiB上限。传输基线不启动视频解码器，VideoPath可留空字符串；BaselineJpegs非空时Backend不参与取帧。验证视频与素材发生在打开设备前。默认不转换/删除原视频，不修改用户设置。

## 结果

result.json包含每档请求FPS、完整帧完成数、完成FPS、解码/编码/写入p95、JPEG大小、CPU秒数、工作集和进程峰值工作集、逐帧采样时间点、设备状态及错误。峰值为进程启动以来高水位。

写入完成不等于屏幕实际刷新。PhysicalDisplayConfirmed和HighFrameRateValidated保持false，目视/录制结果另存独立确认，不能根据发送计数自动改成true。意外失败保留已完成阶段及当前阶段错误。

Ctrl+C请求正常取消；遵循现有已开始JPEG完整发送/5秒写入期限。断线重连仍由现有输出器处理。Windows休眠、高帧率、长时间播放、多设备实机及视频组合主题不因该工具存在而视为通过。

## 当前解码后端边界

WindowsVideoFrameSource使用Windows原生MediaComposition按时间提取真实帧，离线高请求档位约2.5FPS，保留为对照。

新增WindowsLiveVideoFrameSource使用原生MediaPlayer的帧服务器模式连续播放；按需CopyFrameToVideoSurface，再读取像素。只有一个GPU目标表面与一个合并的可用信号，不把所有解码帧送进托管队列。使用默认Direct3D设备不等于已验证硬件解码，硬解状态未作承诺。该后端不可用时明确失败，不偷偷回落后仍报告相同能力。

全分辨率1920×462合成运动片段离线实测，请求5/10FPS分别完成约4.9/9.3FPS；每档10秒，含读回与JPEG编码，不含HID。原视频每250ms更换图案，编码后包含重复帧，因此完成FPS不能当作独立画面更新率。

接口依据：[Microsoft MediaComposition.GetThumbnailAsync](https://learn.microsoft.com/en-us/uwp/api/windows.media.editing.mediacomposition.getthumbnailasync)。只指定采样宽度以保持原始比例；采样时间是请求时间，并非解码器返回的精确PTS。

连续后端依据：[MediaPlayer.CopyFrameToVideoSurface](https://learn.microsoft.com/en-us/uwp/api/windows.media.playback.mediaplayer.copyframetovideosurface)、[VideoFrame.CreateAsDirect3D11SurfaceBacked](https://learn.microsoft.com/en-us/uwp/api/windows.media.videoframe.createasdirect3d11surfacebacked)。日志采样时间是播放器媒体时钟，不是精确解码PTS。
