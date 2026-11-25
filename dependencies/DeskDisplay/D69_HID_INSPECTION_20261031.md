# D69 HID 只读检查（2026-10-31）

后续状态：已完成受限客户端实验接入并安装0.1.0-rc.20261031.1；详见D69_CLIENT_ACCEPTANCE_20261031.md。本文按历史顺序保留早期只读、失败和探针记录，其中“尚未开放正式客户端”的表述对应当时阶段。当前是受限实验支持，退出后Logo、重连和视频等边界仍未验收。

Windows 通用产品字符串没有直接包含型号。后续核对本机 CRAFT 日志，确认同一序列号被识别为 D69，固件记录为 `V25.D69_LLT.01.002`。该版本来自厂商既有日志，本工具没有发送固件查询。

## 实测结果

通过现有 `tools/Inspect-D92Hid.ps1 -VendorId 0x2100 -ProductId 0x0027` 查询 Windows HID capabilities；句柄访问权限为 0、共享读写，仅查询属性和预解析数据，没有读写 HID reports。

| 属性 | D69 本次读取 | D92 既有记录 |
|---|---|---|
| VID / PID | 0x2100 / 0x0027 | 0x5548 / 0x1011 |
| HID 序列号 | C511D3782B2E（与 PnP 实例路径一致） | C511D3782B05（既有实机） |
| Product | HOTSPOTEKUSB HID DEMO | 以既有设备资料为准 |
| Manufacturer | HOTSPOTEKUSB | 以既有设备资料为准 |
| Usage Page | 0xFFA0（65440） | 0xFFA0 |
| Usage | 1 | 1 |
| InputReportByteLength | 513 | 513 |
| OutputReportByteLength | 1025 | 1025 |
| FeatureReportByteLength | 0 | 0 |
| Input / Output Report ID | 0 / 0（capabilities 实测） | 0 / 0（既有记录） |
| PnP 状态 | OK | 本轮未重新检查 |

接口路径：

```text
\\?\hid#vid_2100&pid_0027#6&3263abd5&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}
```

序列号已用 HidD_GetSerialNumberString 交叉确认。Report ID 通过 HidP_GetValueCaps / HidP_GetButtonCaps 查询，空 Feature 列表不表示存在 ID 为 0 的 Feature 报告。1025 是 Windows HID 报告缓冲区长度，不能据此推断 USB 端点包大小、轮询间隔或吞吐率。

## 与客户端的差异

`src/DeskDisplay.Display/Usb/D92HidDevice.cs` 的枚举筛选（约 48 行）及写句柄身份复核（约 70 行）均限定 D92 VID/PID。D69 当前不会进入这个 D92 输出通道。这是既有型号边界，不能仅追加 VID/PID 就宣称支持。

Usage 和报告长度与既有 D92 相同，为可能复用 HID 传输基础提供线索，但不足以证明 CRT DIS、CRT CONNECT、CRT DRA、JPEG 编码及旋转参数兼容。

## 进一步的离线证据及单帧实验

- 厂商日志：`C:/Users/Administrator/AppData/Roaming/HotSpot/MiraBox Craft/logs/first_2026-10-31_10-23-26.log`，约 59、76 行记录 D69DeviceOperation、固件及 VID/PID/序列号。对应 D69.mPanelTheme/manifest.json 的 ThemeBackgroundWidth/Height 为 1280/480；这是厂商主题配置证据，实际面板边界仍需视觉验证。
- 本机 SDLibrary1.dll SHA256：`D1814EC62BCD096029A9756578EFCBCBB378826623F024FC5A74D403C5ABFBF1`，与旧 D92 分析使用的 DLL 版本不同，不沿用旧 RVA。
- 本机 MiraBox Craft.exe SHA256：`19C742FCD06788E1EBCF5DCF896A209A738EC4DA956E59317E866B561CA109AF`。
- EXE RVA 0x22E4A0，通过日志函数名字符串交叉定位 D69DeviceOperation::sendExtendedFrame 的 lambda；0x22E76D 设置 ImageStruct 类型 0x10，0x22E783 设置字段 0xB1，0x22E794 调用 SDDevice::appendSendData。默认 QRect 构造见 EXE RVA 0x626C0，随后显式设置 X/Y 为零。
- DLL RVA 0x29550 构造 CRT/DRA 头、大端长度及几何字段；RVA 0x32B70 构造 CRT/DIS 唤醒；RVA 0x23580 构造 CRT/CONNECT 心跳。存在分支，不能仅凭这些公共函数宣称所有型号协议一致。
- 全程离线读取 PE，未加载执行厂商 DLL，未附加厂商进程，未运行固件工具。
- 独立实验工具新增显式 D69 计划，仅允许指定序列号、1280×480 或 480×1280 测试 JPEG、Report ID 0、0xB1 和单帧。以独占写句柄拒绝已占用设备，打开后复核 VID/PID、序列号及 capabilities；不修改正式应用的 D92 匹配规则。
- 第一次发送被 D92 Studio 运行保护拒绝，没有发送。确认软件在首页后按用户既有授权正常 Alt+F4 退出，重试成功：58,425 字节 JPEG，58 份图像报告，Windows 写入约 25ms，另发一份唤醒报告。没有发送心跳、模式、亮度、休眠、固件或闪存命令。
- `all_reports_written` 只是主机写入成功。实体显示、方向及完整边界正在等待用户确认；不能标记硬件验收通过。
- 构建 0 警告/0 错误；原编码器 33 组头比较、50 组分包、8 组无效输入通过；新增 D69 计划 6 组拒绝检查通过，没有发设备命令。
- 原始证据：`artifacts/d69-hid-descriptor-20261031.json`、`artifacts/d69-vendor-disassembly-20261031.txt`、`artifacts/d69-extended-frame-20261031.txt`、`artifacts/d69-image-struct-20261031.txt`、`artifacts/d69-first-frame-20261031/`。保留了首次拒绝和重试记录。

## 实体反馈：单帧验收失败

用户要求重发后，2026-10-31 10:51:28 再次完成主机写入（58 份、16ms）。用户明确反馈：测试图只显示约一秒，随后消失并停在开机 Logo。**本轮实体显示持久性验收失败，不能以主机成功写入标记通过。** 之前“画面会保留”的表述未经 D69 实体验证，已撤回。

只读查询发现该 USB 实例的 LastArrivalDate 为 10:51:45.321，当前 ProblemCode=0、在线。系统最近 15 分钟筛选未取得相关事件。到达时间晚于本次发送，但没有连续枚举记录，不能据此认定发送命令导致 USB 重启，也不能确定固件 watchdog、模式或供电原因。

目前独立实验在唤醒和一帧后立即释放连接，没有持续会话或心跳。是否因缺失 D69 模式/会话步骤导致恢复 Logo 尚未证实。离线继续定位 EXE RVA 0x233150（D69DeviceOperation::sendScreenCommandToDevice），其中在处理模式及 directJpeg 状态后调用 RVA 0x67990；证据见 artifacts/d69-screen-command-20261031.txt。尚未把该流程加入发送器，未向设备发送模式、重置或其他试探指令。

用户反馈独立保存在 artifacts/d69-first-frame-20261031/visual-failure.json，原始发送日志保留。暂停进一步 HID 输出，先核对完整生命周期；正式客户端 D69 支持仍未开放。

## 尚待确认

### 90秒真实时钟变化帧测试（主机及目视通过）

在独立探针添加受限LiveClock诊断，复用生产ClockScene、SceneView、SceneJpegRenderer。专用STA线程和容量1请求队列，每秒采样真实DateTimeOffset.Now；逻辑画布1280×480，输出旋转90°为480×1280、质量85 JPEG。单写者完整帧后才心跳，每帧独立5秒超时，无自动重试或重连；结束后释放渲染线程和设备连接。未修改正式客户端或协议编码器，没有执行模式/固件命令。

90.019秒共88帧、3173份图像报告、88次CONNECT全部写入，87条后续图像记录对应87个不同秒样本。会话前/中PnP LastArrival均12:02:01.583，无写入错误。结束后只读查询LastArrival变为12:08:07.154；退出边界仍未解决，不能标为正常停止验收通过。证据artifacts/d69-clock-20261031-120623/result.json及pnp-after.jsonl。

用户随后明确确认“画面和秒数正常，期间稳定”，本轮90秒时钟方向、文字、走秒及会话期间稳定性目视验证通过。反馈独立保存在同目录visual-confirmation.json；原result.json中反馈前生成的PhysicalDisplayConfirmed=false保留，不改写历史记录。没有验证拔插、自动重连、多设备或视频；退出后返回Logo/重新枚举仍待处理，不以会话期间通过替代退出验收。

构建0警告0错误；离线真实渲染检查确认时间变化生成不同JPEG、480×1280尺寸正确、旧JPEG快照不变且工作线程可正常退出；原33头+50分包+8无效输入检查通过，另4项LiveClock/会话范围拒绝检查通过。新增工具文件tools/D92.ProtocolProbe/DiagnosticClockFrames.cs；SessionSeconds上限扩为120秒，旧计划默认0不变。

### 旋转 JPEG 对照：两轮连接期间写入正常，退出后仍有重连

随后用户在屏幕旁要求再次测试，相同配置第三轮完成30.038秒、57份图像报告、30次心跳，无写入错误。用户明确确认：**“期间显示正常，结束后才返回 Logo”。** 因此本次480×1280 JPEG、同一连接、30秒心跳会话的实体显示通过；退出后不保留画面，不能标记停止输出或退出生命周期验收通过。反馈独立保存于 artifacts/d69-observe-location.txt 指向目录的 visual-confirmation.json，未篡改发送日志中的 pending 标记。

该结果支持图像方向及会话维持是接入的重要条件，但没有单独隔离心跳与保持句柄的作用，也没有证明退出后回Logo是固件预期行为。下一步应核对厂商持续会话/正常停止行为，进行更长会话与变化帧测试；正式客户端接入、自动重连及视频能力均未验收。

下一轮仅改变 JPEG 方向：用同一测试图顺时针旋转90°，传输尺寸480×1280，重新按相同质量85/4:2:0编码（57800B）；会话仍为唤醒、完整一帧、每秒CONNECT、保持连接30秒，没有执行SCREEN模式。方向和重新编码造成的内容差异不能完全分离，不能据此宣称已定位唯一固件根因。

- 首轮30.022秒：1帧、57份图像报告、30次心跳，全部主机写入完成，无IOException。
- 用户要求再发一次后，第二轮30.026秒，同样1帧/57报告/30心跳，全部主机写入完成。
- 对照先前1280×480在约1秒后的写入失败，这两轮证明480×1280组合在本次30秒会话期间保持可写。实体方向、边框、显示稳定性仍待用户确认。
- 首轮开始约11:56:46，结束约11:57:16，后续PnP LastArrival为11:57:29.843；第二轮目录repeat-115759、约11:58:29结束，后续LastArrival为11:58:41.039。两个到达时间都在会话结束后；没有连续PnP监测，不把时间关系直接视为根因。**不能宣称退出后的保持画面或会话关闭已解决。**
- 原始证据目录artifacts/d69-rotation-20261031-115646及其repeat-115759；定位文件artifacts/d69-rotation-location.txt、d69-rotation-repeat-location.txt。无新增代码、模式、固件或用户设置更改。

### 连接保持复测仍失败

用户授权继续后，新增独立探针 `SessionSeconds`（默认0，仅 D69 可用、最大40秒）：整帧写完后保持同一独占连接，每秒发送已知格式的 CONNECT；没有重复图像、没有发送 SCREEN 模式或重置命令。两轮均在第一次图像和一次心跳写入成功后，下一次写入失败，IOException / HRESULT 0x8007001F（Win32 31，连接到系统上的设备没有发挥作用），自动释放连接且不重试。第二轮为用户明确要求“重新来一次”后的单次重试。

这两轮都未完成计划的30秒。第一轮前后即时 PnP 快照不足以捕获完整离线区间，不把当前又在线等同于未断开；没有新的视觉成功确认。结果排除了“仅仅因为单帧后立即关闭句柄”的简单解释，但不能独立区分心跳、模式、JPEG方向/解码或其他固件行为。

新增离线 EXE RVA 0x67990：由 D69 模式函数调用，构造 12 字节 `43 52 54 00 00 53 43 52 45 45 4E xx`；输入参数2对应末字节00、参数1对应01。这里只记录静态映射，不推断持久性，也未执行。证据 artifacts/d69-base-screen-command-20261031.txt。后续先核对模式、图像方向及完整调用链，再安排更改过的受控实验，暂停重复当前失败流程。

会话测试目录通过 artifacts/d69-session-location.txt 和 artifacts/d69-session-retry-location.txt 定位；send.jsonl/error.jsonl保留。构建0警告0错误，原91项离线检查通过，新增3项会话边界拒绝检查通过。没有修改正式客户端、设备设置或固件。

- D69 实际像素尺寸、方向及支持的图像格式。
- 传输 JPEG 的方向、正式能力范围和持续发送表现。
- 唤醒及单帧实验之外的初始化、心跳、断线恢复和输出生命周期。
- 稳定身份读取、连接占用、断线恢复，以及持续发送能力。
- 多型号并行输出及设备配置恢复行为。

## 后续接入顺序

1. 读取 HID report capabilities 中的 Report ID，并交叉确认 HID 序列号。
2. 优先使用官方 D69 资料或现有厂商程序的离线实现证据，确定像素尺寸及发送格式。
3. 基于已确认的证据建立独立型号配置和兼容性规则；复用通用会话和渲染能力。
4. 协议证据充分后，再安排单帧边界测试、方向检查与持续输出测试。

最初阶段仅只读。后续在用户明确要求直接继续后，进行了上述独立唤醒与单帧 RAM 图像实验；没有修改正式应用、用户配置、驱动或固件，也未发布安装支持 D69 的版本。

API 结构依据：[HIDP_CAPS](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/hidpi/ns-hidpi-_hidp_caps)、[HIDP_BUTTON_CAPS](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/hidpi/ns-hidpi-_hidp_button_caps)。
