# D92 协议实验工具

## D69 单帧实验（2026-10-31）

新增显式 `DeviceModel: "D69"`、`SerialNumber` 的实验计划分支。仅支持 VID/PID 2100/0027、Usage FFA0/1、513/1025/0 capabilities、Report ID 0、Field12 177、HoldSeconds 0，以及 1280×480 或 480×1280 的 JPEG。D69 使用独占写句柄，先发送一份易失唤醒，再发送一帧图像；复核身份后才写入。运行中的 DeskDisplay.App 或 MiraBox Craft 会阻止发送，不自动强制退出或重试。

**历史未旋转 JPEG 的 D69 实体验收失败，不再重复此流程。** 单帧约一秒后回到 Logo；后续 `SessionSeconds: 30` 保持连接、每秒 CONNECT 的两轮实验均在下一次写入失败（Win32 31），未完成 30 秒。不能将该诊断作为可用输出方案。当时 SessionSeconds 默认 0，仅 D69 允许 0–40；实验节奏不是已验证的固件要求，不发送 SCREEN 模式或重置报文。

后续480×1280传输JPEG（逻辑1280×480顺时针90°）的30秒会话已获用户确认“期间显示正常，结束后才返回Logo”；上面的失败记录针对未旋转JPEG，不能混为一轮。目前 SessionSeconds 上限扩展到120秒，仅诊断使用。`LiveClock: true` 要求D69、SessionSeconds>0、480×1280传输尺寸，通过单独STA工作线程复用 ClockScene/SceneView/SceneJpegRenderer，按真实系统时间每秒重绘；每帧独立5秒写入超时，单写者、无自动重连、无模式/重置命令，结束后正常释放。不是视频或长期稳定性证明。

`--clock-self-test <新证据目录>` 只做离线渲染，检查连续时间样本生成不同JPEG、旧快照不被覆盖，以及480×1280尺寸；不访问设备。D69停止/退出后的行为与正式客户端接入仍待验证。

这是型号隔离的实验入口；不表示正式客户端已经支持 D69。主机写入完成与实体画面验收分别记录。依据、DLL/EXE 哈希及新版本 RVA 见仓库根目录 D69_HID_INSPECTION_20261031.md。原 D92 默认计划的型号与 VID/PID 限制保持原值。

此工具独立于 DeskDisplay 主程序和解决方案。C# / .NET 8，没有新增 NuGet 依赖；WPF 仅用于读取 JPEG 元信息。它不是完整管理软件，也没有实现虚拟显示器。

**实体验证已通过**：Craft 主进程停止时，独立发送 40 帧旋转后的测试 JPEG；用户确认“显示了，文字方向正确”。这是已有初始化状态下的接管验证，尚不是断电冷启动验证。实验后已重新启动 Craft。

**后续冷启动验证也已通过**：用户实际断电重连，日志记录离线 11.6 秒，Craft 主程序未运行，用户确认 `COLD START TEST` 已显示；连续 40 帧发送成功。厂商监控服务和驱动仍保留，发送中的热插拔自动恢复及长时间稳定性尚未验证。见 `artifacts/d92-cold-start-confirmation.json`。

## 已知协议

2026-09-29 本机 `SDLibrary1.dll` 的 33 组厂商函数返回值已用于逐字节核对 C# 编码器。另有 50 组分包边界测试及 8 组无效输入检查。

实测 D92 使用 Report ID 0、1025 字节输出报告；前 1 字节为 Report ID，随后 1024 字节承载连续的 `32 字节头 + JPEG`，末包不足部分补零。长度仍从设备 HID capabilities 读取，不在编码器中写死。

| 绘图头偏移（不含 Report ID） | 内容 |
| --- | --- |
| 0–2 | ASCII `CRT` |
| 3–4 | 0 |
| 5–7 | ASCII `DRA` |
| 8–11 | 大端 `JPEG 长度 + 32` |
| 12 | 本机完整画面观察值 `0xB1`；完整语义尚未知 |
| 13–14 / 15–16 | 大端宽 / 高 |
| 17–18 / 19–20 | 大端 X / Y |
| 21–31 | 0 |

Craft 的完整画面使用宽、高、X、Y 全零。实际图像尺寸由 JPEG 携带。本机 Windows 桌面为 1920×462，但线上 JPEG 为 **462×1920**，基线 SOF0、8 bit、YCbCr 4:2:0。不能把桌面方向等同于设备传输方向。

## 离线验证

```powershell
dotnet run --project tools/D92.ProtocolProbe -c Release -- artifacts/d92-vendor-header-fixtures.json
```

`tools/validate_d92_header.py` 可用本机已安装的厂商库重新生成合成数据 fixtures。它核对 DLL SHA-256，只调用已完整静态审查的绘图头构造函数，不创建设备对象，不调用 HID 函数。不能用于其他版本。

## 实验发送

```powershell
dotnet run --project tools/D92.ProtocolProbe -c Release -- --send artifacts/d92-send-plan-rotated.json
```

发送前必须退出 Craft；工具检测到 Craft 正在运行会拒绝发送。计划文件显式给出设备接口、测试 JPEG 路径、线上参数、JPEG 像素宽高和可选 HoldSeconds（0–60）。工具再次核对设备 VID/PID、UsagePage 和 Usage，并验证 JPEG 尺寸。HoldSeconds 大于 0 时每秒发送一次；有总超时，异常退出时释放句柄，不自动重试 USB 错误。

只实现 `CRT/DRA` 图像发送，不包含固件、上传到闪存、灯光、亮度或休眠命令。`all_reports_written` 仅意味着 Windows 写入完成，不能代替实体屏幕观察。当前未实现冷启动初始化和读取设备应答；退出 Craft 后的实验属于已有初始化状态下的接管。

## 断电冷启动实验

`tools/Watch-D92ColdStart.ps1` 要求开始时只有一个 D92 在线且 Craft 主程序已退出。它依次记录 USB 消失、同一设备重新出现，重新枚举 HID 接口，再使用显式计划发送测试图。若 Craft 在监测期间启动、设备身份发生变化、拔插间隔过短或等待超时，会终止实验并记录原因；不会自动启动 Craft，也不会发送固件或猜测的初始化命令。

```powershell
.\tools\Watch-D92ColdStart.ps1 -PlanPath artifacts\d92-cold-start-plan.json -LogPath artifacts\d92-cold-start-observation.jsonl
```

只有 USB 消失不能证明电源已断，仍需用户确认所有供电连接已断开、屏幕熄灭至少 5 秒，并确认重连后出现新生成的 `COLD START TEST` 画面。本轮准备时厂商 `MiraboxCraftPersistentService` 硬件监控服务仍在运行，测试结论必须保留这一边界，不能声称整个厂商环境已移除。

## 调查工具与限制

- `Inspect-D92Hid.ps1`、`Inspect-ActiveDisplays.ps1`：只读枚举。
- `inspect_craft_binary.py`：离线反汇编。分析专用 pefile/capstone 安装在 `%LOCALAPPDATA%/DeskDisplayBuild/analysis-python`，不是应用依赖。
- `observe_d92_protocol.py`：短时 Frida 函数挂接，只记录协议头、JPEG 元信息和少量签名字节，不保存画面。一次观测后 Windows 记录了 Craft 访问冲突崩溃，原因未确定，不能排除挂接影响，因此后续实验已停止使用此方式。不要把它作为日常诊断或产品功能。
- 本机供电重连后的冷启动显示已验证；发送中的自动重连、长期输出、应答和多种固件兼容性仍需单独验证。
