# D92 HID macOS 移植指南（只读准备）

此文依据 [D92_HID_PROTOCOL_SPEC.md](D92_HID_PROTOCOL_SPEC.md) 和当前 Windows 源码。当前尚无 macOS 实机 HID 结果；这里的步骤是移植与验证计划，不表示 Mac 已能控制 D92。

## 可直接复用的协议逻辑

`src/DeskDisplay.Display/Usb/D92FrameEncoder.cs:14-80` 不调用 Windows API，负责 `CRT\0\0DIS`、`CRT\0\0CONNECT`、32 字节 `CRT\0\0DRA` 头及连续流分包。可在 Mac 项目中移植或复制成不依赖 WPF 的库。保持长度大端、Report ID 参数化、报告长度运行时传入、末包零填充。当前全屏帧使用头偏移 12 值 `B1`，几何参数四个 0，见 `D92HidDevice.cs:138-145`；不要推断 `B1` 的通用含义。

串行发送状态机也可复用其逻辑：按所选序列号重连、每次连接先唤醒、约 10 秒心跳、单帧完整发送、写失败释放并等待重枚举，见 `D92HidRenderTarget.cs:45-112`。Mac 应以自己的异步/线程机制实现，不复制 Windows `Process.GetProcessesByName("MiraBox Craft")` 作为唯一占用检测。

JPEG 图像应先按设备配置旋转像素、再编码为完整 JPEG。旋转是在图像内容层完成，DRA 头没有已验证的旋转字段；本机 1920×462 画布旋转 90° 后的 JPEG 为 462×1920，见 `SceneJpegRenderer.cs:24-51`。macOS 可用自己的图像渲染和 JPEG 编码器，但需要在实体屏确认它生成的采样、颜色格式和方向；Windows 默认质量 90 只是通过的样本。

## 必须重写的平台边界

| Windows 当前实现 | macOS 需替换/核验 |
|---|---|
| `HidD_GetHidGuid`、SetupAPI 枚举，`D92HidDevice.cs:23-59,117-127` | 通过 macOS HID 枚举接口获取 VID/PID、Usage Page、Usage、序列号和输出报告容量 |
| `CreateFile`、`SafeFileHandle`、异步 `FileStream`，`D92HidDevice.cs:63-86,131-146` | Mac HID 设备打开、Output Report 写入、释放与断连通知 |
| `HidD_GetAttributes`、`HidP_GetCaps`、`HidD_GetSerialNumberString`，`D92HidDevice.cs:68-104` | 打开后再次核验身份和实际报告描述符，避免设备替换 |
| WPF `SceneView`、`RenderTargetBitmap`、`JpegBitmapEncoder`，`SceneJpegRenderer.cs:24-51` | Mac 图像渲染、旋转和 JPEG 编码实现 |
| Windows 进程名占用检测，`D92HidRenderTarget.cs:114-119` | 检查设备是否可打开及写入失败；另行评估 Mac 上可能的竞争进程 |
| WPF Dispatcher 和用户设置，`UsbOutputViewModel.cs:130-176` | Mac UI 主线程与后台写入协调、所选序列号及画布配置持久化 |

Windows 源码里使用的 `OutputReportByteLength` 已包含 Report ID；样机报告总长 1025。macOS 不同 HID 写入 API 对 Report ID 的要求可能不同：有的调用分别传 reportID 参数和纯 payload，有的缓冲区包含 Report ID。移植时必须读选定 API 的契约和实际描述符，**不能简单把 Windows 1025 字节缓冲区原样交给任意 Mac API**。

## Mac 实机验证顺序

1. **只读枚举**：确认 VID `5548`、PID `1011`、Usage Page `FFA0`、Usage `1`、真实序列号，以及目标 Output Report 的最大长度。不得根据首台 HID 或显示器编号选设备。
2. **报告缓冲区约定**：确认 macOS API 的 Report ID 参数与缓冲区是否包含首字节 `00`；记录实际成功的报文长度及返回值。Windows 样机为 1025 字节（1 字节 ID + 1024 字节数据），不是 USB 传输层包长。
3. **离线编码核对**：先在 Mac 上运行纯字节测试，比较 DIS、CONNECT、DRA 头和多种分包边界；不要为这一步打开 HID 设备。
4. **受控实体测试**：确认目标序列号后，以已知完整 JPEG 测试唤醒、单帧、心跳及显示方向。每一步记录传输结果并由人确认画面；写入成功不等于屏幕显示成功。
5. **生命周期**：测试停止时是否完整送完当前帧、5 秒 deadline、拔插后按原序列号重连、屏幕休眠/唤醒、重复打开以及潜在进程占用。测试时避免与 Windows/Craft 同时驱动该设备。

## 重点传输风险

- **Report ID 0**：在 Windows `FileStream` 报告缓冲区的首字节；在 Mac API 中要确认是独立参数还是缓冲区一部分。漏写或重复写会整体错位。
- **输出报告长度 1025**：必须以设备实际描述符及 API 要求为准；`D92FrameEncoder.Reports` 的有效数据容量是 `reportLength - 1`。长度不一致会改变分包边界。
- **零填充**：DIS、CONNECT 和最后一份 DRA 报告都填到完整报告长度。Mac API 如果只接受未填充 payload，要明确区分 API 表示与设备收到的报告。
- **完整 JPEG 顺序**：32 字节 DRA 头紧接完整 JPEG，连续报告不插入心跳；停机时避免截断帧。
- **写入完成语义**：Windows 异步 `FileStream.WriteAsync` 成功与实体显示不同；Mac HID 写返回成功也要单独核实可见画面和持续刷新。
- **休眠状态**：已有 Windows 实验证明，不发 DIS 时即便 JPEG 写入成功也可能黑屏；新连接需要唤醒，运行中保持心跳。
- **旋转与 JPEG**：Mac 编码器与 WPF 的像素旋转、JPEG 采样可能不同，需用设备实体确认。不要把 1920×462 或 90° 写死成所有 D92 的规则。
- **未知字段**：DRA 偏移 12 的 `B1`、保留字节和非全屏几何行为仍无完整定义。先只复现已通过的全屏路径。

## 建议复制给 Mac 上 Codex 的文件

**最小必需包**（保留相对目录结构）：

- `D92_HID_PROTOCOL_SPEC.md`、`D92_HID_MAC_PORTING_GUIDE.md`
- `src/DeskDisplay.Display/Usb/D92FrameEncoder.cs`
- `src/DeskDisplay.Display/Usb/D92HidDevice.cs`
- `src/DeskDisplay.Display/Usb/D92HidRenderTarget.cs`
- `src/DeskDisplay.Display/Usb/SceneJpegRenderer.cs`
- `src/DeskDisplay.Core/Models/UsbOutputSettings.cs`
- `tests/DeskDisplay.DisplayTests/UsbOutputTests.cs`
- `artifacts/d92-vendor-header-fixtures.json`
- `artifacts/d92-wake-heartbeat-result.json`
- `artifacts/d92-device-investigation.md`

**理解 Windows 集成时再加**：`src/DeskDisplay.App/ViewModels/UsbOutputViewModel.cs`、`tools/D92.ProtocolProbe/Program.cs`、`tools/D92.ProtocolProbe/CandidateFrameEncoder.cs`、`DEVELOPMENT_HANDOFF.md`、`AGENTS.md`。无需复制 `bin/`、`obj/`、厂商 DLL、真实设备路径、用户配置或诊断图像。Mac 上的 Codex 应先复现纯字节测试，再接入 Mac HID API。
