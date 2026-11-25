# D92 实体设备验收 — 2026-10-21

客户端代码基线：`0.1.0-preview.20261021.10`。本轮没有修改应用业务、HID协议、驱动或固件。

## 结果

| 场景 | 时长（含前15秒静态检查） | 最终帧数 | 连接次数 | 主题显示事件（含首项） | 结果 |
|---|---:|---:|---:|---:|---|
| 横屏 | 360.69秒 | 338 | 2（含拔插后的重连） | 17，即16次观察到的切换 | 传输、轮播与视觉通过 |
| 竖屏 | 120.24秒 | 120 | 1 | 6，即5次观察到的切换 | 传输、轮播与视觉通过 |

两轮均执行暂停、继续和手动下一项，均正常结束为Stopped；没有Failed或BlockedByCraft状态。横屏有一次用户拔插引起的正常WaitingForDevice（错误信息“设备没有连接”），不能将它隐藏成“从未发生传输异常”。竖屏没有等待/断连。

## 本次补齐的热插拔证据

- 21:20:38.607：88帧、1连接，进入WaitingForDevice。
- 21:20:38.683：独立真实HID枚举确认目标设备不存在。
- 21:20:58.817：连接次数增至2。
- 21:20:58.861：恢复发送第89帧，随后持续至338帧。
- 枚举采样连续20次未发现目标，之后重新出现；`reconnectEvidence=true`。
- 用户确认：“画面和秒数正常，拔插后自动恢复”。

连接建立到恢复第一帧约44毫秒只表示这两条日志之间的时差，不代表从用户插回数据线起的总恢复时间。完整断开区间约20秒，包含用户持有数据线的时间。

此前长测只有视觉确认、没有重连机器日志的历史记录不修改；本次提供新的独立证据，补齐的是本机Windows/此设备/本次短测范围。

## 竖屏与配置保护

- 从用户现有设置只读取目标身份和参数：1920×462、90°；没有猜测或重设硬件配置。
- 实际主题通过已有ThemeOutputGeometry映射为462×1920、0°；生产USB日志确认有效几何。
- 用户确认：“竖屏方向、文字和秒数均正常”。
- 两轮诊断均在隔离目录保存设置和主题；运行前后用户settings.json的SHA256一致。
- 初始静态JPEG编码尺寸462×1920，与既有验证配置一致。

## 使用了什么

新增显式运行工具 `tools/DeskDisplay.HardwareAcceptance/`，通过 `--run-real-device` 才能发送真实命令；不加入普通自动回归。

它复用生产CreatorViewModel、UsbOutputViewModel、ThemeScene、SceneJpegRenderer和D92HidRenderTarget；A/B内容由真实可编辑ThemeProject生成，包含独立文字、静态图片、日期和走秒时钟，并保存为可重开的源项目。没有用固定JPEG冒充动态主题。

普通客户端测试期间正常关闭，避免两个进程同时写设备。输出停止时等待USB释放，不强制杀进程。新增工具Release构建0警告/0错误。应用代码未修改，因此没有为本次诊断重新发布客户端。

## 证据位置

- 总结：`artifacts/hardware-acceptance-20261021-summary.json`。
- 横屏：`artifacts/hardware-landscape-20261021/{events.jsonl,result.json,visual-confirmation.json}`。
- 竖屏：`artifacts/hardware-portrait-20261021/{events.jsonl,result.json,visual-confirmation.json}`。
- 各目录 `themes/` 为真实可编辑源与轮播配置，`captures/` 为显式采集的初始USB帧。
- 原始result.json中的visualConfirmation仍标记为等待外部确认，因为诊断进程不能读取聊天；可信用户确认在单独sidecar与本报告中记录，原始证据不覆盖。
- `staticFrames=1` 是静态阶段结束时最近一次生产状态公布的计数下界，生产每30帧公布一次，不是静态只写了一帧。manualNext字段表示发出了请求，实际变化以theme事件为准。
- 工具说明：`tools/DeskDisplay.HardwareAcceptance/README.md`；构建日志：`artifacts/hardware-acceptance-build.log`。

## 尚未验证

1. 小时级/过夜轮播长稳，不把约8分钟的短测称为长期认证。
2. Windows休眠/唤醒、设备被其他进程占用后的恢复、多个设备并存。
3. 本次竖屏没有再次物理拔插，不能单独宣称竖屏热插拔通过。
4. 实际桌面拖动手感、多DPI、不同分辨率/固件及macOS。

诊断结束后停止输出并释放连接，实体屏可能保留最后一帧；重新打开客户端后，需从正常入口应用内容才会继续实时输出。
