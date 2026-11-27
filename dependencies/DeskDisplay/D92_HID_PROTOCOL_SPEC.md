# D92 USB HID 协议说明（Windows 已验证部分）

本文只描述当前 DeskDisplay 源码与已有测试、实体反馈支持的行为。它不是厂商完整协议规范，不覆盖固件、闪存或视频传输。源码行号以 2026-10-11 工作区版本为准。

## 证据等级

- **实现并经实体显示确认**：本机 D92 在 Craft 主进程退出时，接收 DeskDisplay 画面；用户确认方向正确、时钟持续变化约 6 分钟。见 `artifacts/d92-wake-heartbeat-result.json`、`artifacts/d92-device-investigation.md:155-162`。
- **离线字节测试**：唤醒和心跳的前缀及零填充经过 `tests/DeskDisplay.DisplayTests/UsbOutputTests.cs:18-25` 检查；DRA 头还与厂商对照夹具比较，见 `tools/D92.ProtocolProbe/Program.cs:26-38` 与 `artifacts/d92-vendor-header-fixtures.json`。
- **仍未知**：偏移 12 的 `0xB1` 完整语义、保留字节含义、其他设备型号及 macOS HID API 对 Report ID 的缓冲区约定。不得依据本文件猜测这些字段。

## 设备识别与枚举

Windows 实现先通过 `HidD_GetHidGuid`、`SetupDiGetClassDevs` 和 `SetupDiEnumDeviceInterfaces` 枚举 HID 接口，再从设备路径筛选 `vid_5548&pid_1011`（忽略大小写）。它以只读共享句柄读取 capabilities 和序列号；要求 Usage Page `0xFFA0`、Usage `1`、输出报告长度至少 33 字节以及非空序列号。打开写句柄后再次用 `HidD_GetAttributes` 核验 VID `0x5548`、PID `0x1011`、所选序列号与 Usage，防止枚举和打开之间设备被替换。见 `src/DeskDisplay.Display/Usb/D92HidDevice.cs:23-75`。

本机采集的 InputReportByteLength=513、OutputReportByteLength=1025、FeatureReportByteLength=0；**1025 是 Windows HID 输出报告缓冲区长度，含首字节 Report ID，不是 USB 物理包长度**。见 `artifacts/d92-device-investigation.md:38-43`。应用选择并保存的是序列号，不根据显示器编号或分辨率认设备；断开时保留所选序列号，见 `src/DeskDisplay.App/ViewModels/UsbOutputViewModel.cs:48-81`。

## 单份输出报告

当前连接把 Report ID `0x00` 写在字节 0。运行时报告总长取当前 HID capabilities 的 OutputReportByteLength，样机为 1025；其余最多 1024 字节承载连续协议数据，不足一份的尾部用 `0x00` 填满。应用没有另加块编号、序号或每块头。见 `src/DeskDisplay.Display/Usb/D92FrameEncoder.cs:55-79`、`src/DeskDisplay.Display/Usb/D92HidDevice.cs:72-75,133-145`。实体观测也记录了 Report ID 0 和 1025 字节报告，见 `artifacts/d92-device-investigation.md:118-120`。

## CRT DIS 唤醒报告

每次成功打开连接，先发一份完整输出报告。按报告缓冲区索引，字节 `0` 为 Report ID `00`，字节 `1..8` 为 `43 52 54 00 00 44 49 53`（ASCII `CRT\0\0DIS`），余下字节全部为 `00`。1025 字节样机上的前 9 字节为 `00 43 52 54 00 00 44 49 53`。见 `src/DeskDisplay.Display/Usb/D92FrameEncoder.cs:12-21`、`D92HidRenderTarget.cs:61-65`。字节测试在 `tests/DeskDisplay.DisplayTests/UsbOutputTests.cs:18-23`。厂商库离线分析对应 SHA-256 `533926144e928d5c3e2c0012b52b086922bcd9c884f14228042fb3208782c397` 的 RVA `0x31310`；仅发送一次唤醒后用户确认已有画面亮起，见 `artifacts/d92-device-investigation.md:155-157`。

## CRT CONNECT 心跳报告

按报告缓冲区索引，字节 `0` 为 Report ID `00`，字节 `1..12` 为 `43 52 54 00 00 43 4F 4E 4E 45 43 54`（ASCII `CRT\0\0CONNECT`），余下字节全部为 `00`。1025 字节样机上的前 13 字节为 `00 43 52 54 00 00 43 4F 4E 4E 45 43 54`。见 `D92FrameEncoder.cs:23-31` 与测试 `UsbOutputTests.cs:18-25`。每次新连接复位心跳计时，之后约每 10 秒发送；对应厂商库 RVA `0x22830`，见 `D92HidRenderTarget.cs:62-73`。计时在帧循环内检查，实际间隔还会受渲染和写入耗时影响。

## CRT DRA 图像帧

一帧为 **32 字节头 + 一份完整 JPEG 文件字节**。头按协议流（不含 Report ID）偏移：

| 偏移 | 长度 | 当前实现/依据 |
|---|---:|---|
| 0–7 | 8 | `43 52 54 00 00 44 52 41`，ASCII `CRT\0\0DRA` |
| 8–11 | 4 | 无符号大端整数，值为 `32 + JPEG 字节数` |
| 12 | 1 | 当前发送 `B1`；完整语义未知 |
| 13–14 | 2 | 宽度参数，无符号大端；当前全屏帧为 `0000` |
| 15–16 | 2 | 高度参数，无符号大端；当前全屏帧为 `0000` |
| 17–18 | 2 | X 参数，无符号大端；当前全屏帧为 `0000` |
| 19–20 | 2 | Y 参数，无符号大端；当前全屏帧为 `0000` |
| 21–31 | 11 | 当前编码器保持 `00`；未确定字段语义 |

编码依据 `src/DeskDisplay.Display/Usb/D92FrameEncoder.cs:33-53`；当前发送调用为 `D92HidDevice.cs:138-145`。离线观察记录头长、长度大端、偏移 12 的 `B1` 和全屏零几何，见 `artifacts/d92-device-investigation.md:118-120`。头里的几何宽高不是当前完整 JPEG 的像素尺寸，**没有已确认的单独旋转字段**。当前输出在 WPF 中先把画布像素旋转 0/90/180/270 度，再编码 JPEG；JPEG 自身携带编码后的像素宽高。例如本机配置画布 1920×462、旋转 90° 后，线上 JPEG 是 462×1920，见 `src/DeskDisplay.Display/Usb/SceneJpegRenderer.cs:24-51`、`artifacts/d92-device-investigation.md:27-31`。

发送前要求 JPEG 长度 4 字节至 16 MiB，首尾分别为 SOI `FF D8` 和 EOI `FF D9`，见 `D92HidDevice.cs:138-145`。当前默认 WPF JPEG QualityLevel=90；这是实体通过的编码配置，不说明其他质量必然不兼容，见 `SceneJpegRenderer.cs:13-15` 和 `artifacts/d92-wake-heartbeat-result.json`。

## 完整帧分包和发送顺序

协议流按 `header || jpeg` 拼接，按 `OutputReportByteLength - 1` 切成连续块。每块新建完整报告，在字节 0 写 Report ID，字节 1 起写下一段流；最后一份剩余位置补零。第一份通常同时包含完整 32 字节 DRA 头及 JPEG 开始部分。逐份 `await FileStream.WriteAsync`，上一份完成后才写下一份，见 `D92FrameEncoder.cs:55-79`、`D92HidDevice.cs:138-145`。唤醒、心跳、JPEG 使用同一连接的串行发送流程，不插在图像帧中间，见 `D92HidRenderTarget.cs:61-78`。

## 超时、停止、重连与占用

- `CreateFile` 用写权限、共享读写、`FILE_FLAG_OVERLAPPED` 打开；`SafeFileHandle` 包装成异步 `FileStream`。枚举读取使用零数据权限共享句柄。见 `D92HidDevice.cs:63-86`。
- 单次唤醒、心跳或完整 JPEG 发送前检查用户停止；发送开始后使用独立 5 秒 deadline，不让普通停止切断 JPEG 中间块。完成后再响应停止。见 `D92HidRenderTarget.cs:105-112`，离线生命周期测试见 `UsbOutputTests.cs:27-40`。
- 断开、写入异常或该 deadline 到期时释放连接，状态改为等待，约 1 秒后重新枚举**相同序列号**；重新打开会再次唤醒。见 `D92HidRenderTarget.cs:45-86,94-102`。同一进程只允许一个输出会话，见 `:28-29`。
- 每轮检查 Windows 进程名 `MiraBox Craft`；发现时停止输出并报告占用。其他进程的句柄占用依赖 `CreateFile`/写入异常，源码没有通用“独占锁”检测。见 `D92HidRenderTarget.cs:47,114-119`。
- 关闭时先取消会话，再等待发送任务和资源释放；设置保存尚未完成时不允许延迟启动，见 `UsbOutputViewModel.cs:86-107,192-200,232-238`。

## 协议相关源码清单

| 文件 | 作用 |
|---|---|
| `src/DeskDisplay.Display/Usb/D92FrameEncoder.cs` | 唤醒、心跳、DRA 头及 HID 分包；可移植的纯字节逻辑 |
| `src/DeskDisplay.Display/Usb/D92HidDevice.cs` | Windows HID 枚举、身份校验、句柄打开和写入 |
| `src/DeskDisplay.Display/Usb/D92HidRenderTarget.cs` | 会话、心跳、超时、重连及关闭 |
| `src/DeskDisplay.Display/Usb/SceneJpegRenderer.cs` | WPF 场景渲染、旋转、JPEG 编码 |
| `src/DeskDisplay.App/ViewModels/UsbOutputViewModel.cs` | 设备选择、持久化与生命周期协调 |
| `src/DeskDisplay.Core/Models/UsbOutputSettings.cs` | 所选序列号、画布和旋转配置 |
| `tests/DeskDisplay.DisplayTests/UsbOutputTests.cs` | 报文、旋转、停止和配置离线回归 |
| `tools/D92.ProtocolProbe/Program.cs`、`CandidateFrameEncoder.cs` | 厂商头夹具与分包边界离线核对 |
| `artifacts/d92-vendor-header-fixtures.json` | DRA 头对照数据 |

历史实验工具 `tools/D92.ProtocolProbe/DirectImageSender.cs` 和 `tools/DeskDisplay.UsbDemo` 含实际发送入口，移植资料阅读可用，但**不要在 Mac 上直接运行**。
