# D92 实体主题轮播验收工具

这是显式选择后才访问真实HID的独立诊断入口，不在自动回归中运行。

复用生产 `CreatorViewModel` / `UsbOutputViewModel` / `D92HidRenderTarget` / `ThemeScene`，没有另一套协议实现；不发送固件或未知控制命令。生成可重新编辑的A/B源项目，时钟来自系统时间。初始15秒静态图仅用于静态显示验收，不用于冒充动态主题。

## 运行

1. 退出DeskDisplay、UsbDemo和Craft，确保没有另一个进程写入D92。
2. 从实际保存设置取得序列号、宽、高、旋转。不要照抄其他设备身份或凭猜测修改旋转。
3. `dotnet build tools/DeskDisplay.HardwareAcceptance -c Release`
4. 使用显式入口：

```powershell
& tools/DeskDisplay.HardwareAcceptance/bin/Release/net8.0-windows10.0.19041.0/DeskDisplay.HardwareAcceptance.exe --run-real-device <plan.json> <new-output-directory>
```

计划字段：SerialNumber、Width、Height、RotationDegrees、DurationSeconds（60—900）、Orientation（Landscape/Portrait）。输出目录必须不存在，禁止覆盖既有证据。程序只选择明确指定序列号，匹配不唯一时拒绝开始。

测试配置、主题、素材及轮播文件全部位于诊断目录，不写用户资源库。用户settings.json只读SHA256对比；环境变量DESKDISPLAY_USB_CAPTURE_DIRECTORY可显式保存USB首三帧，默认不截取用户图像。

默认流程：15秒静态画面、20秒交替主题；80秒请求暂停、95秒继续、110秒请求手动下一项。较短计划可能没有达到所有步骤，结果字段必须据实解释。

## 证据解释

- `events.jsonl`：生产HID状态、真实枚举存在性、主题标题变化、暂停/恢复请求、内存/CPU样本。
- `result.json`：最终帧数和连接数、运行是否完整结束、主题变化次数、用户设置是否未变。
- staticFrames是静态阶段结束时最近一次公布的计数下界（生产状态每30帧报告一次），不是静态阶段精确帧数。
- manualNext表示发出请求；应结合随后theme事件确认实际切换，设备等待时请求可能被安全延后。
- reconnectEvidence仅在真实枚举消失后再出现、出现WaitingForDevice、连接数至少2、恢复后又发送超过5帧时为true。
- 退出0只表示诊断正常完成或正常取消，不等于画面与重连验收通过。必须结合completed、各项证据和实体视觉确认。
- 视觉方向、文字完整性和秒数是否持续变化需用户看实体屏确认。软件截图或写入成功不能替代。

关闭诊断窗口会取消测试、等待传输释放并保存结果，不强制终止进程。测试结束后再正常启动主应用。物理拔插测试请断开连接电脑的数据线至少5—8秒，而非仅调整屏幕摆放或断开其他供电线。
