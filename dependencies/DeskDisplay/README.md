# DeskDisplay

## 当前 RC 状态（2026-10-31）

本机最新修复版为0.1.0-rc.20261031.2，修复设备预览关闭时同步清理导致的Close重入退出；原D69实验范围不变。

Windows x64 封闭测试候选已包含资源库、创作中心、可编辑主题、直接应用、混合轮播、Windows副屏及D92 USB视频输出。视频提供画质优先、流畅优先和自定义，目标帧率不等于稳定实测能力；每布局支持一个可见视频。

D69新增实验支持：从设备管理或创作中心选择D69，核对1280×480和90°传输旋转后应用静态素材/数字时钟主题，每秒更新。视频、复杂插件及其他方向暂不开放；停止后可能返回设备Logo，重连仍待实机验收。首页和资源库原D92快捷入口不自动改用D69。详见[D69接入与验收](D69_CLIENT_ACCEPTANCE_20261031.md)。

安装与操作说明见 [发布包说明](tools/packaging/README.txt)，发布边界见 [已知问题](tools/packaging/KNOWN_ISSUES.md)，新电脑步骤见 [人工验收单](tools/packaging/ACCEPTANCE.md)。本机隔离安装和首次启动测试不代表干净Windows或标准用户已经验收。`tools/Test-Release.ps1` 可对实际发布包执行隔离安装、启动、更新、回滚和卸载检查；不会自动输出到设备。

## D92 USB 直连（历史 Alpha 记录）

以下记录早期Alpha的界面与验证过程，当前能力以上方RC说明为准。早期“USB屏”页提供设备选择、画布宽高、旋转、保存并开始、停止和连接状态，静态/组件场景每秒发送一帧。当前已增加视频输出；USB直连本身不创建Windows扩展桌面，Windows副屏使用独立输出链路。

小组件页的时钟、性能、静态图片和组合场景现在可以直接点击“显示到 D92”；静态壁纸详情提供“显示到 D92”和“壁纸＋组件到 D92”。该操作使用“USB屏”页已保存的设备序列号、逻辑画布和旋转设置。尚未配置时会跳转到“USB屏”，不会自动选择检测到的第一台设备。D92 已在输出时切换内容会复用当前 USB 会话，不重复断开设备。

资源库底栏的轮播目标可选“Windows 副屏”或“D92 USB”。D92 轮播支持顺序、随机、间隔、上一张和下一张，只处理轮播列表中的静态 JPG/PNG；视频、丢失文件和解码失败的图片会跳过。设备断开时保留同一设备身份并暂停切换，连接恢复后继续；停止显示、USB 页停止、休眠和程序退出都会停止轮播并等待输出释放。轮播目标会保存，重启后不会自动开始轮播。

设备按序列号识别，不自动选择第一台设备。宽高由配置提供，旋转仅应用于发送的 JPEG，预览与输出复用同一 Scene。断线后等待同一设备重连，停止和退出会取消并等待传输释放；停止后屏幕可能保留最后一帧。配置保存到本地 settings.json 的 UsbOutput 字段，启动时不会自动开启 USB。切换到 Windows 副屏输出会先停止 USB。

USB 连接后发送厂商协议的唤醒命令，运行期间每 10 秒发送心跳；控制报文和图像串行写入。此前黑屏在仅发送唤醒命令后恢复，JPEG 写入成功并不代表屏幕已经亮起。JPEG 默认质量保留为已实测显示的 90，但没有证据认定其他质量值不受支持。正常停止会等待当前帧发送完成，单帧仍有 5 秒超时上限。

2026-09-30 实机验证：Craft 主程序未运行，DeskDisplay 主程序持续输出约 6 分钟后，用户确认画面正常、秒数持续变化。207 项回归检查通过；长期稳定性仍需后续使用观察。

现有独立 [USB 实时预览测试窗口](tools/DeskDisplay.UsbDemo/README.md) 保留用于诊断。此前已实测 Craft 主进程停止后的输出和断电冷启动；厂商驱动及监测服务仍保留，尚未验证完全卸载厂商组件的环境。

基于 **C# / .NET 8 / WPF** 的 Windows 第二显示器控制软件基础项目。

当前版本提供显示器管理、目标副屏持久化、时钟 / JPG / PNG 图片预览及独立全屏输出。首页与副屏显示同一个 ClockScene 的时间、日期和星期，复用同一套 SceneView，仅有一个每秒采样的时钟 Timer。现已加入本地视频背景和 CPU / GPU / RAM 性能场景；不包含音频可视化。

## 项目结构

```text
DeskDisplay.sln
Directory.Build.props
src/
  DeskDisplay.App/                 WPF 入口与 UI
    App.xaml(.cs)                 组合服务、启动、全局异常日志
    ViewModels/
      ObservableObject.cs         INotifyPropertyChanged 基础类
      MainViewModel.cs            导航、共享 Scene 引用和预览比例
      DevicesViewModel.cs         显示器快照、目标操作、保存反馈与异常恢复
      ImageSceneViewModel.cs      图片选择、预览草稿、应用、恢复和友好错误
      AsyncCommand.cs             异步 UI 命令边界
    Views/MainWindow.xaml(.cs)    主窗口，仅初始化视图
    Views/DevicesView.xaml(.cs)    可滚动显示器卡片列表
    app.manifest                  Per-Monitor V2 DPI Awareness
  DeskDisplay.Core/               无 UI / 第三方依赖
    Models/                       配置、DisplayInfo、TargetDisplaySettings、Scene、ClockScene、ImageScene
    Interfaces/                   配置、显示器、目标选择、IRenderWindowService
  DeskDisplay.Display/            Windows 显示器基础能力
    Services/WindowsDisplayManager.cs
    Services/TargetDisplayService.cs 目标匹配、保存与失败回滚
    Services/RenderWindowService.cs 单实例输出与资源释放，不创建时钟
    DataProviders/ClockSceneDataProvider.cs 应用唯一时钟采样源
    DataProviders/ImageSceneLoader.cs 后台有界解码，不锁定源文件
    Interop/                      Windows API 声明与快照读取
    Views/SceneView.cs             唯一的场景绘制代码
    Views/PreviewHost.cs           等比例缩放布局，不包含时钟 UI
    Views/RenderWindow.xaml(.cs)  副屏窗口与原生像素定位
  DeskDisplay.Infrastructure/     基础设施
    AppPaths.cs                   本地存储路径
    Configuration/JsonSettingsService.cs
    Logging/LoggingSetup.cs
tests/
  DeskDisplay.SmokeTests/          无测试框架依赖的配置容错验证
  DeskDisplay.DisplayTests/        枚举、目标、窗口、共享场景及比例验证
```

依赖方向：`App → Core / Display / Infrastructure`，`Display → Core`，`Infrastructure → Core`。采用轻量 MVVM，通过属性绑定管理导航，无 DI 容器、消息总线或额外 MVVM 框架；入口负责对象组装。

## 开发架构约束

项目开发规则见 [AGENTS.md](AGENTS.md)。后续功能沿 `DataProvider → Scene → Renderer → RenderTarget` 架构扩展，预览与外部 Windows 显示器共享同一 Scene 模型。数据采样与 UI 渲染频率独立，UI 不直接访问传感器库或 WASAPI。

目标显示器不能固定为“显示器 2”，也不能写死 D92 或其他设备分辨率；显示逻辑须适配任意分辨率，并为自定义硬件保留扩展空间。后续显示输出必须处理热插拔和资源释放，传感器缺失须允许降级运行。

当前输出沿 `TimeProvider → ClockSceneDataProvider → ClockScene → SceneView → PreviewHost / RenderWindow` 运行。Scene 不依赖 WPF、窗口或具体硬件。同时支持 ImageScene；两个场景均使用同一个 SceneView，没有通用场景编辑框架，也没有为两个宿主分别实现 UI。

## 共享 Scene 与实时预览

```text
TimeProvider
    ↓ 每秒采样一次
ClockSceneDataProvider（应用中仅创建一个）
    ↓ 更新同一个对象
Scene / ClockScene（INotifyPropertyChanged，兼作共享 ViewModel）
    ↓
SceneView（同一份绘制实现）
    ├── PreviewHost → 主程序首页
    └── RenderWindow → 已选择的 Windows 副屏
```

`Scene` 提供逻辑画布尺寸及 AspectRatio。`ClockScene` 提供 `CurrentTime`、`TimeText`、`DateText`、`WeekdayText`，先提交一个完整时间戳，再通知视图，因此跨午夜时三项内容来自同一次采样。日期和星期使用中文，时间采用 24 小时制。

App 创建一次 ClockScene，将同一个实例交给 MainViewModel 与 RenderWindowService。两个宿主各有一个 SceneView 控件实例，它们的 Scene 和 DataContext 指向相同对象；没有复制 ViewModel，也没有第二份时钟 UI。SceneView 是可复用的 WPF FrameworkElement，通过统一绘制逻辑按可用区域等比例显示时间、日期、星期；它不读取系统时间，也不创建 Timer。

ClockSceneDataProvider 持有唯一的 1 秒 DispatcherTimer，统一从 TimeProvider 采样，支持重复 Start 而不会重复创建采样循环。RenderWindowService 已移除原来的 Timer。关闭副屏只释放该视图，首页继续实时更新；应用退出时才停止时钟采样。视图卸载或关闭时解除 Scene 事件订阅。显示器模块原有的 400ms 防抖 Timer 只处理设备变化，并非第二个时钟采样源。

首页 PreviewHost 只负责缩放布局，始终使用目标像素宽高比，不拉伸内容。目标分辨率变化时更新已有 Scene 的画布尺寸；断开后沿用配置中最近一次分辨率；未设置目标或尺寸未知时使用明确标注的 16:9 示例比例。超宽屏会呈现横向窄预览，竖屏会按可用高度缩小。UI Automation 的整数像素边界可能出现不超过 1 像素的取整差异。

原有“测试副屏”按钮现在打开相同的 ClockScene 内容。旧 DisplayTestScene / DisplayTestRenderer 已移除；无边框、2 物理像素边框、Per-Monitor V2、原生像素定位及断连保护保持有效。时钟模块没有额外 NuGet 依赖；视频和传感器新增依赖见下文。

## 编译与运行

需要 Windows 10 2004（19041）或更新版本 / Windows 11，以及 **.NET 8 SDK**（仅运行时无法编译）。也可使用 Visual Studio 2022 17.8 或更高版本，安装“.NET 桌面开发”工作负载，打开 `DeskDisplay.sln`，将 `DeskDisplay.App` 设为启动项目。

在仓库根目录执行：

```powershell
dotnet restore DeskDisplay.sln
dotnet build DeskDisplay.sln -c Release
dotnet run --project src/DeskDisplay.App/DeskDisplay.App.csproj -c Release
```

当前开发机器的 SDK 安装在用户本地目录；若 `dotnet` 仍提示没有 SDK，可先在当前 PowerShell 会话执行：

```powershell
$env:PATH = "$env:LOCALAPPDATA\DeskDisplayBuild\dotnet;$env:PATH"
```

构建后可直接启动 `src\DeskDisplay.App\bin\Release\net8.0-windows10.0.19041.0\DeskDisplay.App.exe`，目标机器需要 .NET 8 Desktop Runtime。

配置容错验证：

```powershell
dotnet run --project tests/DeskDisplay.SmokeTests/DeskDisplay.SmokeTests.csproj -c Release
dotnet run --project tests/DeskDisplay.DisplayTests/DeskDisplay.DisplayTests.csproj -c Release
```

## NuGet 依赖

使用以下依赖，未增加新的包种类：

| 包 | 版本 | 用途 |
| --- | --- | --- |
| Serilog | 4.2.0 | Infrastructure 与 Display 的结构化日志 |
| Serilog.Sinks.File | 6.0.0 | 每日滚动文件日志 |

JSON 使用 .NET 内置 `System.Text.Json`。显示器识别使用封装在 Display 项目内部的 Win32 P/Invoke，已移除 Windows Forms 框架引用。UI 使用 WPF。

## 显示器管理

`IDisplayManager` 位于 Core，暴露异步快照读取 `GetDisplaysAsync(CancellationToken)`、配置变化事件 `DisplaysChanged` 和 `Dispose()`。`WindowsDisplayManager` 在 UI 线程创建及释放，枚举在后台线程执行，变化事件在创建它的 UI 线程派发。该类不选择目标设备，不使用显示器序号推断 D92。

`DisplayInfo` 包含 `DeviceName`、`FriendlyName`、可空的 `DeviceId`、`X`、`Y`、`Width`、`Height`、`IsPrimary`。这些坐标和尺寸表示当前 Windows 桌面模式的像素区域，允许负坐标、纵向屏幕和任意分辨率，不表示面板的原生最大分辨率。`DeviceName` 是 GDI 名称（例如 `\\.\DISPLAY1`），可能随配置改变；`DeviceId` 优先使用监视器设备接口路径，比显示器序号更适合持久化匹配，但换接口、驱动重装或部分虚拟设备仍可能使其变化，它不是永久硬件序列号。

原生 API 均封装在 `DeskDisplay.Display/Interop`：

| Windows API | 用途 |
| --- | --- |
| `GetDisplayConfigBufferSizes` / `QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS)` | 枚举活动输出路径及源模式，读取桌面像素尺寸与位置；缓冲区因热插拔失效时最多重试三次 |
| `DisplayConfigGetDeviceInfo` | 将源映射到 GDI DeviceName，尽可能获取 EDID 友好名称及 monitorDevicePath |
| `EnumDisplayMonitors` / `GetMonitorInfoW` | 获取桌面监视器区域和 `MONITORINFOF_PRIMARY` 标记，也作为 CCD 不可用时的基础信息 |
| `EnumDisplayDevicesW(EDD_GET_DEVICE_INTERFACE_NAME)` | CCD 不可用时补充活动监视器名称和设备接口路径 |
| `SetThreadDpiAwarenessContext` | 在后台同步枚举范围内启用 Per-Monitor V2，完成后恢复原线程上下文 |

API 依据：[QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig)、[GetMonitorInfoW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmonitorinfow)、[EnumDisplayDevicesW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaydevicesw)、[目标设备信息](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_target_device_name)。

优先按活动路径为每个目标输出创建卡片；复制模式中多块物理显示器可以共享 DeviceName 和桌面 Bounds，主显示器标记也对应共享桌面源。远程桌面、驱动限制或设备信息读取失败时记录结构化日志并降级：友好名称最终退回 DeviceName，设备标识可为 null；GDI 回退只能展示驱动当前可枚举的信息。已连接但禁用的输出不属于活动显示器。

通过隐藏的顶层 `HwndSource` 接收 `WM_DISPLAYCHANGE` 与设备增减相关的 `WM_DEVICECHANGE`；400ms 防抖合并连续通知，空闲时不轮询。刷新过程中收到通知会补做一次刷新。成功后整体替换快照；失败保留上次结果并显示重试提示，不让异常逃出 UI 事件。退出时取消刷新、退订事件、停止定时器、移除消息钩子并释放 HWND。

### DPI 与坐标

应用 manifest 从进程启动即声明 `PerMonitorV2, PerMonitor`，并提供旧版 `true/pm` 声明，遵循 [Microsoft 的 DPI manifest 设置方式](https://learn.microsoft.com/en-us/windows/win32/hidpi/setting-the-default-dpi-awareness-for-a-process)。当前主窗口 DPI Awareness 已通过运行时 API 验证为 Per-Monitor V2。

WPF 布局继续使用 DIP，卡片上的像素数值不乘除 UI 缩放比例。RenderWindow 不把 `DisplayInfo.X/Y/Width/Height` 直接赋给 WPF 的同类属性，而使用原生像素定位。边框厚度则以 `2 / DpiScaleX` 和 `2 / DpiScaleY` 转为 DIP，保证实际输出为 2 像素。

## 副屏测试窗口

在设备页面设置一个已连接的非主目标屏后，点击“测试副屏”。没有可用目标时按钮禁用；点击时再次枚举并解析目标，关闭 / 断开期间返回的旧请求不能创建新窗口。程序不会在启动时自动打开测试输出。

RenderWindow 无标题栏、无边框、不可调整尺寸、不显示在任务栏，默认置顶且显示时不抢占激活。这里的“全屏”指 `rcMonitor` 的完整像素 Bounds，包含任务栏所在区域，不是排除任务栏的 `rcWork`。保持 `WindowState.Normal`，不用最大化来模拟全屏。

定位步骤：

1. 重新确认目标仍为非主屏，使用 `EnumDisplayMonitors` / `GetMonitorInfoW` 验证活动设备及最新 Bounds。
2. 在窗口尚未显示时创建 HWND，通过 `SetWindowPos(HWND_TOPMOST, X, Y, Width, Height, SWP_NOACTIVATE | SWP_NOOWNERZORDER)` 移动到目标屏，让 WPF 获取目标 DPI。
3. 显示窗口后重新验证显示器和覆盖范围。用 `GetWindowRect` 与 `GetClientRect` 检查窗口及客户区都精确等于目标像素区域；不满足则关闭测试窗口并记录错误。
4. `WM_DPICHANGED` 交给 WPF 更新内容 DPI，随后重新应用目标像素 Bounds；`WM_WINDOWPOSCHANGING` 保持目标几何尺寸，防止 WPF 的建议 DIP 尺寸改变全屏覆盖。

相关 API：[SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)、[WM_DPICHANGED](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged)、[GetWindowRect](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowrect)、[GetDpiForWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getdpiforwindow)。所有 P/Invoke 仍位于 Display 项目。Windows 11 上还尝试关闭 DWM 圆角，以保留四角边框；不支持此属性的系统允许降级。

`RenderWindowService` 管理唯一的输出实例，重复点击不会新建多窗口。已打开时分辨率 / 排列变化会更新同一窗口；收到显示配置变化消息时先隐藏输出，再等待枚举确认。目标断开、变为主屏、选择被取消或更换目标时关闭旧窗口，重新连接不会自动弹出输出。

“关闭副屏显示”释放输出窗口，也会取消尚在枚举中的打开请求。主软件退出前关闭输出；关闭副屏窗口不会关闭主窗口，主窗口的坐标、大小和 WindowState 不参与输出定位。关闭输出时解除 Scene 订阅、移除 HWND 钩子并释放窗口，共享时钟继续服务首页。受控的副屏绘制与定位异常记录日志后关闭副屏输出；时钟采样异常记录日志并保留上次有效值。进程级崩溃不属于可恢复窗口异常。

ClockScene 内容采用深色背景，居中绘制时间、日期、星期，场景边界仍保留 2 个物理像素的边框。没有逐帧动画；唯一采样源每秒更新同一个 Scene，两处视图观察同一份状态。

## 目标副屏

在设备卡片列表中选中任意非主显示器，再点击“设为目标副屏”。保存成功后显示“当前目标副屏”、名称、当前分辨率和“状态：已连接”。目标断开后保留名称及最近一次分辨率，显示“目标副屏未连接”，不会选择列表中的其他空闲显示器。“取消设为目标副屏”在离线时同样可用，取消后重新连接设备也不会自动恢复选择。

`ITargetDisplayService` 定义于 Core，`TargetDisplayService` 实现于 Display，持久化通过 Core 的 `ISettingsService` 接口完成，不让 Display 依赖 Infrastructure。提供所需的同步方法：

```csharp
DisplayInfo? GetTargetDisplay();
void SetTargetDisplay(DisplayInfo display);
DisplayInfo? ResolveSavedTargetDisplay();
```

`UpdateDisplays(snapshot)` 在每次枚举后提供最新快照并解析目标；`GetTargetDisplay()` 返回当前可用的非主显示器或 null。`SetTargetDisplay` 和 `ClearTargetDisplay` 更新会话选择，随后必须 `await SaveAsync()` 完成持久化；设备页命令已处理此完整流程。UI 线程串行执行这些操作，不能在保存进行中并发修改服务状态。保存失败会恢复上一次持久化选择并显示错误反馈。

匹配顺序：

1. DeviceId 不区分大小写精确匹配，不依赖数组顺序、桌面坐标、分辨率或 DeviceName。若对应设备现在是主显示器，则不作为副屏使用，也不继续寻找替代屏幕。
2. DeviceId 无法匹配时尝试唯一的 DeviceName。已保存有效友好名称时，还需友好名称一致且唯一，避免显示器序号被其他型号占用；没有有效友好名称时，不用 DeviceName 覆盖两个互相冲突的已知 DeviceId。
3. 最后尝试唯一的有效 FriendlyName。通用监视器名称、DeviceName 本身及多台同名设备不能用于此回退；不使用分辨率或排列位置来消除歧义。
4. 仍无法匹配则保持保存记录，返回 null，等待原目标重新连接或用户重新选择。

备用名称匹配属于尽力识别，不是硬件序列号验证；如果原设备标识改变，且现场只有一台名称相同的设备，仅靠这些字段无法证明它是原来的实体设备。同名多屏产生歧义时本实现保守地不匹配。代码没有任何 D92 专属分支或固定分辨率。

恢复成功后会更新保存的设备名称与最近一次分辨率；没有变化时不写文件。设备暂时缺少 DeviceId 时保留之前已知的标识。枚举失败会保留设备卡片和目标偏好，但将当前目标标记为不可用，避免把过期枚举结果当作已连接设备。点击设置前再次枚举，检查所选设备仍连接且不是主屏。关闭窗口时等待已经接受的设置 / 取消操作完成，再释放资源。

## 配置与异常处理

配置路径为 `%AppData%\DeskDisplay\settings.json`。首次启动自动创建：

```json
{
  "SchemaVersion": 1,
  "TargetDisplay": null
}
```

设置目标后，`TargetDisplay` 保存以下字段（数值仅为示例，不是设备规格）：

```json
{
  "SchemaVersion": 1,
  "TargetDisplay": {
    "DeviceId": "monitor-device-interface-path",
    "DeviceName": "\\\\.\\DISPLAY7",
    "FriendlyName": "D92",
    "LastWidth": 1234,
    "LastHeight": 567
  }
}
```

兼容早期配置中的 `TargetDisplayId`：设备接口路径按 ID 匹配，GDI 名称按 DeviceName 回退；成功保存时迁移到新字段，取消选择时两种记录都会清除。SchemaVersion 保持 1，新字段对早期无目标配置兼容。输出窗口仅在点击“测试副屏”后打开。

读取失败、JSON 损坏、空配置或不支持的版本均记录异常并使用内存默认值，保留原文件用于排查。默认文件创建失败同样不阻塞启动。保存先写同目录临时文件，再替换目标文件；服务内通过信号量串行化读写。保存失败返回 `false` 并记录日志，目标服务回滚未保存的选择。设置页面仍为占位，目标选择在设备页面完成。

日志路径：`%AppData%\DeskDisplay\logs\deskdisplay-YYYYMMDD.log`，每日滚动，最多保留 14 个文件。AppData 日志目录初始化失败时尝试 `%TEMP%\DeskDisplay\logs`；两处均不可写时退回诊断 Trace，此时无法保证日志落盘。

启动事件是 WPF 所需的 `async void`，其异步操作包含在 `try/catch` 内；配置操作返回 `Task` 并被等待。所有显式捕获的异常均记录日志。UI 未处理异常、进程未处理异常和未观察到的 Task 异常均有全局记录入口。无法安全恢复的 UI / 启动异常会提示并退出；配置异常局部恢复。进程被强制终止或底层崩溃等情况不能保证记录日志，全局处理也不能代替后续业务代码的局部异常处理。

## 验证范围

配置冒烟测试覆盖默认文件创建、保存回读、损坏 JSON、null、版本异常、字段类型异常、文件锁定、创建 / 保存失败和错误日志记录。测试使用独立临时目录，不修改用户配置。

显示器测试覆盖原生结构体内存布局、本机实际枚举、四屏模拟数据、负坐标、不同尺寸、缺失设备标识、刷新异常保留旧数据、通知合并、空列表、退出后的晚到结果及退订。无需额外测试框架；本机原生枚举测试需要交互式 Windows 桌面和至少一块活动显示器。

`TargetDisplayTests.cs` 使用独立临时 JSON 文件及模拟显示器，验证选择 D92 → 保存 → 创建新服务 / ViewModel → 恢复、重排、改名、分辨率变化、断开重连、主屏切换、名称歧义、备用匹配、取消后重启、旧配置迁移、保存失败回滚、枚举失败及立即关闭时等待保存完成。

本次目标功能验收：实际窗口验证了离线目标的名称 / 最近分辨率 / 状态、主屏禁选、“取消”后立即退出落盘及重新启动保持未设置。机器当前只有主显示器，实体 D92 的完整拔插和 Windows 重启验收尚未进行。

`RenderWindowTests.cs` 覆盖重复打开、断连、目标切换、主屏保护、异常关闭、渲染故障后重开、关闭时取消待完成打开请求及应用退出。原生测试创建 WPF HWND，验证窗口 / 客户区像素尺寸、负坐标、任意尺寸、Per-Monitor V2、主窗口位置不变及四边恰好 2 像素；同时验证实际预览和 RenderWindow 的 Scene / DataContext 引用相同，输出关闭后预览仍接收更新。

`SceneTests.cs` 验证跨午夜时间 / 日期 / 星期一致性、重复 Start 不重复采样、采样异常恢复、Dispose 后停止采样、超宽屏与竖屏比例、分辨率变化及断连比例保留。实际首页 UI Automation 已验证超宽和竖向预览边界、中文日期 / 星期及每秒变化的时间。

测试生成以下图片，位于 `tests/DeskDisplay.DisplayTests/bin/Release/net8.0-windows10.0.19041.0/`：`clock-scene-render.png`、`clock-preview-ultrawide.png`、`clock-preview-portrait.png`。图片使用固定时间方便比较，实际运行读取系统本地时间。

当前机器只有一块显示器，原生窗口测试通过内部测试入口绕过“必须副屏”的设备校验，短暂在现有屏幕显示画面，但使用真实窗口、DPI 和 Win32 定位；生产入口仍禁止主屏。真实“主屏 150% + 副屏 100%”、反向 DPI 组合、物理拔插及四屏布局仍需硬件验收。原生测试过程中窗口会短暂置顶，完成后自动关闭。

副屏输出实机验收：设置目标 → 测试副屏 → 检查四边均可见且主窗口不移动 → 重复点击只有一个窗口 → 修改目标屏缩放 / 分辨率后确认完整覆盖 → 拔出目标应关闭输出 → 重连后手动测试 → 关闭副屏后主窗口继续运行 → 再次测试并退出主程序，确认输出一起关闭。

实体 D92 验收步骤：连接主屏与 D92 → 设备页面选中 D92 → 点击“设为目标副屏”并确认保存提示 → 关闭重开应显示已连接 → 调整排列、分辨率后刷新确认仍为同一目标 → 拔出 D92 应显示未连接且保留名称 → 插回应恢复 → 取消后重开应保持未设置。

本次验证：Release 构建 0 警告 / 0 错误；本机单屏读出 `WM2700`、设备接口路径和 `1920 × 1080` 桌面尺寸；UI Automation 验证设备卡片、刷新按钮；运行时验证窗口 Per-Monitor V2；发送显示变化消息突发后观察到自动刷新与正常退出。三至四块实体屏、混合 DPI 跨屏移动、复制模式及物理拔插仍需在对应硬件上验收，模拟测试不能替代这些场景。

手动验收：启动主程序，确认标题及三项导航；关闭后检查退出日志。可在关闭程序并备份配置后将配置内容改为无效 JSON，再次启动应正常显示主窗口且记录错误。

## ImageScene：本地图片背景

首页点击“选择图片”，选择 JPG / JPEG / PNG 文件，在下拉框选择显示模式，确认预览后点击“应用到副屏”。必须先在设备页面设置已连接的非主显示器；没有可用目标时应用按钮不可用。也可点击“时钟场景”，再应用，切回同一个共享 ClockScene。

| 模式 | 行为 |
| --- | --- |
| 铺满（Fill） | 保持图片比例，铺满画布，居中裁剪超出部分 |
| 完整显示（Uniform） | 保持图片比例，完整显示，空白部分为深色背景 |
| 拉伸（Stretch） | 分别缩放宽高铺满画布，可能变形 |
| 居中（Center） | 按原始图片像素尺寸居中，不放大，超出画布部分裁剪 |

预览尺寸始终由目标显示器比例决定，Center 也按逻辑画布像素计算，不受缩略预览或图片内嵌 DPI 值影响。图片背景不叠加测试边框。选择图片和调整模式只更新预览草稿；应用后两个宿主引用**同一个 ImageScene 实例**。再次编辑会创建新的轻量草稿，保留当前副屏内容直到再次应用，不会复制图片 UI。修改模式复用已解码 BitmapSource，不会重复解码。

实现路径为 `ImageSceneLoader → ImageScene / ISceneImage → SceneView → PreviewHost / RenderWindow`。Core 中的模型不引用 WPF；Display 的 WpfSceneImage 包装冻结的 BitmapSource。实际图片绘制和四种模式计算只有 SceneView 中一份实现。图片不创建 Timer，也不持续刷新静态内容。原来的唯一时钟采样源保留，方便切回时钟。

成功选择图片后，将 `LastImagePath` 和 `ImageMode` 写入 `%AppData%\DeskDisplay\settings.json`；应用时再次保存当前模式。下次启动恢复最近图片到预览，默认不自动打开副屏，可在设置页开启启动自动显示。切到时钟预览不会清除“最近图片”记录。ImageMode 按枚举数字保存：0=Fill、1=Uniform、2=Stretch、3=Center；无效枚举恢复为 Fill。

`JsonSettingsService.UpdateAsync` 在同一信号量内读取、修改指定字段、原子写入。目标显示器保存也使用该方法，避免图片路径被显示器元数据刷新覆盖。退出时会等待已经接受的图片配置写入，取消未完成的加载结果，停止新操作。

### 解码与文件异常

- 后台线程使用 WPF/WIC 解码器验证真实格式，仅接受 PNG/JPEG；没有新增 NuGet 依赖。
- 使用 FileStream + BitmapImage.CacheOption=OnLoad，读取完成即关闭流，Freeze 后跨线程交付。不会长期锁定原文件，也不会通过 URI 缓存保留原图。
- 解码长边最多 4096 像素，总像素最多 800 万，不放大小图。内存占用随解码像素与像素格式而定，不保留完整原图像素。超大文件（超过 128 MB）或原始尺寸超过 2 亿像素会友好拒绝。解码期间仍可能存在编解码器临时内存，这不是进程内存的硬上限。
- Center 保留原始尺寸元数据；即使大图已经降低解码尺寸，显示位置与裁剪范围仍按原图像素计算。超高分辨率输出会放大有界解码图，可能牺牲细节以控制内存。
- 应用时重新读取文件，检测删除、替换或损坏。失败时显示友好错误并记录结构化日志，保留原有副屏画面。启动时最近图片已丢失也能正常启动并重新选图。
- 不监视文件系统；已显示图片删除后可继续使用内存中的有界缓存，下一次应用或重启才报告丢失。旧场景不再被预览或输出引用后，其托管图片资源可由运行时回收；没有图片历史缓存。

新增 ImageSceneTests 验证 PNG/JPEG、有界解码、独占重新打开源文件、删除后缓存仍可绘制、四种模式的几何和实际像素结果、缩略预览一致性、配置并发更新、同实例应用、草稿不影响现有输出、丢失/损坏图片、热插拔禁用及启动恢复。运行方式与原有测试相同：

```powershell
dotnet build DeskDisplay.sln -c Release
dotnet run --project tests/DeskDisplay.SmokeTests -c Release
dotnet run --project tests/DeskDisplay.DisplayTests -c Release
dotnet run --project src/DeskDisplay.App -c Release
```

## VideoScene：单播放源、本地 MP4/H.264

首页选择 30 FPS（默认）或 60 FPS，点击“选择视频”。完成准备后，进入 VideoScene 自动静音循环播放；点击“应用视频到副屏”，预览与副屏复用同一个 VideoScene、同一个播放器和同一时间线。修改 FPS 后点击“更新视频 FPS”。切到时钟、图片或性能场景会立即关闭旧视频输出、停止并关闭播放器；正在准备的视频也会取消。仅切换首页/设备导航不代表离开 Scene；关闭副屏后首页视频继续播放。

- Core 的 `IVideoPlaybackService` 定义播放会话边界；业务 ViewModel 不引用 MediaElement 或 MediaPlayer。
- Display 的 `WindowsVideoPlaybackService` 只创建一个 WPF MediaPlayer，SceneView 用 `DrawingContext.DrawVideo` 在两个宿主中复用此播放器。没有两套播放计时，没有逐帧截图、帧数组或第二个解码实例。
- 静音同时通过移除播放副本音轨和 IsMuted/Volume=0 实现；MediaEnded 将 Position 归零并继续播放。Alpha 循环可能有短暂接缝，不承诺无缝拼接。
- FPS 设置通过 Windows MediaTranscoder 在**播放前**生成相应帧率的 H.264 临时副本落实，并非只改一个 UI Timer。准备时请求硬件加速，播放由 Windows/WPF 媒体管线决定硬件解码可用性；不保证所有设备都能硬解。
- 每次选择或更新 FPS 都需要准备副本，会消耗时间、磁盘空间及短时编码资源。播放副本长边最多 1920 像素（保持比例，不放大小视频），码率为 30 FPS 时 8 Mbps、60 FPS 时 12 Mbps。此限制是 Alpha 的资源策略，不限制显示器分辨率；超高分辨率屏幕上会等比放大，可能损失细节。
- 60 FPS 不会把低帧率视频变成真实的高帧率拍摄内容；实际显示还受源视频、显示器刷新率及系统负载影响。多显示器共用同一内容时间线，但不同刷新率屏幕不承诺物理扫描同步。
- 临时副本保存在 `%TEMP%\DeskDisplay\Video`，停止、替换、取消、退出时清理；清理失败记录日志。源文件不修改。异常终止进程可能留下临时文件。视频路径/FPS 现已随场景配置保存，首次使用默认 30 FPS。
- 丢失、损坏、不支持或媒体组件不可用时显示友好错误；播放失败关闭播放器，不退出主程序。

视频相关的 WPF 工程目标框架升级为 `net8.0-windows10.0.19041.0`，由 .NET SDK 自动引入 Windows SDK 的 WinRT 投影依赖；未引入 FFmpeg、LibVLC 或额外媒体框架。参考：[VideoDrawing/MediaPlayer](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/graphics-multimedia/how-to-play-media-using-a-videodrawing)、[MediaTranscoder](https://learn.microsoft.com/en-us/uwp/api/windows.media.transcoding.mediatranscoder.preparefiletranscodeasync)。

`VideoTests` 使用 Windows 原生媒体合成生成红/蓝 H.264 测试视频，验证实际 30/60 FPS、静音、共享播放器、多次循环、关闭、取消及源文件释放。硬件解码使用情况与 D92 的实际显示仍应在目标机器验证。

## SensorService 与 PerformanceScene

新增独立项目 `DeskDisplay.Sensors`，仅该生产项目直接引用 [LibreHardwareMonitorLib 0.9.6](https://www.nuget.org/packages/LibreHardwareMonitorLib/)。它封装 Computer、IHardware、ISensor 的生命周期和数据选择，UI 与 Display 不访问第三方硬件类型。上游实现与许可见 [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)。该包必要的传递依赖由 NuGet 恢复。

```text
LibreHardwareMonitor + Windows CPU/RAM fallback
    ↓ 后台采样，默认 1000 ms
DeskDisplay.Sensors.SensorService
    ↓ ISensorService.Current：不可变 SensorSnapshot
PerformanceSceneDataProvider：独立 30 Hz 显示更新
    ↓ 时间相关指数平滑
PerformanceScene
    ↓ 同一份 SceneView
首页预览 + RenderWindow
```

`ISensorService` 位于 Core，提供 `SensorSnapshot Current`、`Task StartAsync()`、`Task StopAsync()`。重复启动不会增加采样循环；启动立即返回，首份数据在后台读取完成后发布，之前全部为 null。StopAsync 等待采样停止及 Computer.Close 完成，清空旧快照，支持重新启动。应用退出会等待停止，不留下后台硬件访问。

快照字段均为 `double?`：CpuLoad、CpuTemperature、GpuLoad、GpuTemperature、MemoryLoad。负载单位为百分比，温度为摄氏度。缺失、无效或非有限数值为 null，UI 统一显示 `--`，不会把缺失温度当作 0°C，也不会继续显示过期温度。

- CPU：优先 CPU Total，温度优先 CPU Package，再尝试常见 AMD/Core 汇总名称。无可用读数时 CPU Load 使用 GetSystemTimes 的相邻采样差值，首份差值尚未建立时可以为 null；此 Windows 备用接口在多处理器组机器上存在组范围限制。
- GPU：优先离散 GPU，再 Intel，同级按设备标识排序选择首个有数据设备。负载优先 GPU Core，再 D3D 3D；温度读取 GPU Core，不混用显存温度。Alpha 不聚合多 GPU，也未增加 GPU 选择设置。
- RAM：优先 LibreHardwareMonitor Memory Load，备用 GlobalMemoryStatusEx。
- 每块硬件更新及每个传感器读取分别捕获异常，保留其他可用数值。初始化不完全成功也尝试可用硬件和 Windows 备用接口。持续同类故障避免每秒重复刷日志。
- 温度可用性受硬件型号、驱动和运行权限影响；项目不自动安装驱动或要求提升权限。缺失温度不影响程序启动与其他采样。

首页点击“性能场景”，再点击“应用性能到副屏”。Preview 与副屏持有同一个 PerformanceScene，绘制 CPU / GPU / RAM 数值与负载条。平滑使用时间常数 350 ms 的指数插值，与硬件 1000 ms 采样分离；温度缺失立即清空，恢复后使用新有效值。数值稳定后不继续发出绘制更新，没有展示性能场景时跳过平滑计算；后台采样仍以每秒一次保持最新状态。

新增文件：

- `src/DeskDisplay.Core/Interfaces/ISensorService.cs`、`Models/SensorSnapshot.cs`、`Models/PerformanceScene.cs`
- `src/DeskDisplay.Sensors/SensorService.cs`、`LibreHardwareSensorReader.cs`
- `src/DeskDisplay.Display/DataProviders/PerformanceSceneDataProvider.cs`
- `src/DeskDisplay.Core/Interfaces/IVideoPlaybackService.cs`、`Models/VideoScene.cs`
- `src/DeskDisplay.Display/Services/WindowsVideoPlaybackService.cs`
- `src/DeskDisplay.App/ViewModels/VideoSceneViewModel.cs`

SensorTests 验证重复启动、异常后的恢复、停止与释放、重新启动、温度缺失、数值平滑、无效值及真实硬件采样；性能示例画面输出为测试目录中的 `performance-scene.png`。运行命令与上文一致。

## 当前版本体验：统一入口、启动恢复、运行诊断

首页现在使用统一的“时钟 / 图片 / 组合 / 视频 / 性能”场景选择器。图片/视频通过“选择文件”选择素材；图片模式和视频 FPS 仅在对应场景显示。所有场景共用“应用到副屏”和“停止显示”，设备页的“应用当前预览”也调用同一个命令。“当前预览”与“副屏正在显示”分开标记；预览尚未应用时明确提示，避免把旧副屏内容误认为已切换。切离视频仍会立即停止播放并关闭旧视频输出。

### 壁纸＋组件组合场景

“组合”场景把一张本地静态壁纸、共享时钟及可选的 CPU / GPU / RAM 数据绘制到同一画布。提供“时钟居左”“时钟居中”“时钟与性能”三种布局以及 Fill / Uniform / Stretch / Center 图片模式。预览、Windows 副屏和 D92 JPEG 都读取同一个 `DashboardScene`；时钟与传感器沿用现有采样服务，不创建第二套计时器或硬件读取线程。

资源库的静态壁纸详情可直接创建组合场景并显示到 D92。所选壁纸、布局和性能开关写入现有设置，重启恢复预览但不自动启动 USB。组合场景仍使用 D92 页保存的任意画布与旋转，视频不能作为组合背景。

### 场景设置恢复

新增设置：`LastScene`（时钟/图片/视频/性能）、`LastVideoPath`、`VideoFps`、`AutoDisplayOnStartup`。原有 `LastImagePath`、`ImageMode` 和目标屏信息保留。场景选择、模式、FPS 与启动开关变更自动保存；保存串行执行，关闭时等待已经接受的操作和最后一份配置写入。业务逻辑集中于 `WorkspaceViewModel`，MainWindow 后台代码仍只初始化视图。

设置页的“启动后自动显示到已保存的目标副屏”默认关闭。启动依次读取配置、枚举目标、恢复场景、检查媒体就绪，再决定自动显示。无效场景值回退到时钟，无效 FPS 回退到 30；已经明确选择的媒体文件丢失或损坏时禁止自动输出，不拿旧预览或另一种场景替代。目标屏缺失不会选择其他显示器，也不会在稍后插入时自动抢占输出，需要用户确认后应用。

停止显示会取消尚未完成的自动打开意图。视频准备阶段仍会花费转码时间，可用停止按钮取消。修改视频 FPS 后点击“更新视频 FPS”；当所选 FPS 与当前播放副本不一致时，统一应用按钮不可用，避免应用旧帧率视频。

### 诊断与睡眠唤醒

“设置”和“诊断”入口提供启动开关、当前输出、视频状态、五项传感器可用性、进程私有内存、句柄数量和运行时间。诊断每秒读取现有快照，不额外调用硬件库；“打开日志文件夹”使用实际初始化成功的日志目录（支持临时目录回退）。硬件解码未被实际检测时明确显示未确认。

收到 Windows 睡眠通知时，关闭副屏并停止视频；唤醒后重新枚举显示器并恢复所选预览，提示手动确认后应用，不静默恢复外部输出。窗口、播放器、电源事件订阅和诊断 Timer 在退出时释放；传感器仍由独立采样服务管理。

### 验证范围

`WorkspaceTests` 覆盖：统一应用、预览/输出区分、恢复性能场景并自动输出、视频路径/FPS 恢复、40 轮视频/性能切换后的活动会话归零、模拟睡眠/唤醒、配置串行保存和关闭等待、丢失媒体禁止自动输出、断屏时禁用应用、诊断缺失值。原有真实 H.264 循环/30/60 FPS、源文件释放、硬件采样及显示器测试继续运行。测试生成 `workspace-home.png` 供布局检查。

模拟电源通知不等于真实机器休眠。仍建议在实际 D92 和混合 DPI 环境完成：插拔屏幕、真实睡眠唤醒、连续数小时视频循环，并在诊断页面观察内存与句柄是否持续增长；本轮自动测试不声称完成数小时实机耐久验证。

运行方法不变：

```powershell
dotnet build DeskDisplay.sln -c Release
dotnet run --project tests/DeskDisplay.DisplayTests -c Release
dotnet run --project tests/DeskDisplay.SmokeTests -c Release
dotnet run --project src/DeskDisplay.App -c Release
```

## 壁纸资源库与新版界面

主界面采用顶部导航、分类标签、直接排列的三列圆角壁纸网格和底部轮播栏；分类页不再占用空间显示精选横幅。资源卡片的画面区域固定使用 D92 的 1920∶462 比例；静态图片从原图有界解码为最大 640 像素的等比缩略图，避开 Windows Shell 封面的预裁剪。卡片默认只显示画面，悬停后显示名称、收藏和“显示到 D92”。白色底色、黄色操作按钮及详情页布局参考用户提供的界面截图；品牌、图标和素材不沿用第三方产品。

当前入口：

1. **导入**：右上角“导入壁纸”，批量选择 JPG/JPEG/PNG/MP4。导入后切到“我的资源”，重复路径跳过，单个文件失败不影响其他文件。
2. **浏览**：顶部“动态壁纸 / 静态壁纸 / 我的资源”；分类区提供一级分类和随一级联动的二级分类，也可按“我的收藏”筛选；搜索名称、两级分类和标签。新导入的壁纸按导入时间显示在最前。首批数量根据当前可见区域计算，向下滚动最多连续加载三屏，随后显示分页栏；每页最多三屏，避免资源较多时持续创建卡片造成卡顿。
3. **详情**：点击卡片后使用原图生成最高 2048 像素的等比高质量预览，不再放大卡片的 640 像素缩略图；预览采用 OnLoad 解码，不锁定原文件，离开资源后释放。详情显示原始尺寸、类型、时长和标签。点击“预览壁纸”启用共享 SceneView，点击“应用到副屏”应用到已保存的目标屏。右侧按同分类优先列出本地资源，并非在线热度榜。
4. **视频控制**：详情提供暂停/播放、进度条和时长；这些操作影响共享播放器，因此已经应用的副屏也会同步暂停或跳转。FPS 仍通过准备播放副本生效。图片详情仅显示图片模式，视频详情显示 FPS 控制。尺寸标记来自真实源文件，不虚构 4K 清晰度；视频播放仍受现有 1920 长边资源策略约束。
5. **管理**：详情下方修改名称、一级分类、二级分类和标签；可收藏、加入/移出轮播、前移/后移播放顺序。移除资源只移除索引和缓存封面，不删除原文件。旧资源的一级分类保持不变，缺少二级分类时归入“未分类”。
6. **轮播**：底部选择 Windows 副屏或 D92 USB，设置间隔（秒）、顺序/随机，再开启轮播；支持上一张/下一张。“轮播列表”勾选后切到“我的资源”并按播放顺序展示已选项目；离开“我的资源”会自动取消该筛选，避免静态或动态分类页只显示轮播条目。Windows 目标屏不可用或所有文件均失效时停止；D92 断开时暂停切换并等待相同设备恢复。D92 只轮播静态图片，视频会跳过。“停止显示”停止轮播及外部输出，并取消未完成的应用意图。
7. **原有功能**：时钟/性能场景位于“小组件”；“设备”“设置”“诊断”保留原有目标选择、启动恢复和诊断功能。

新增 `DeskDisplay.Library` 项目，负责真实媒体元数据、Windows 缩略图读取和资源索引持久化。资源库位于 `%AppData%\DeskDisplay\Library\library.json`，封面位于 `Thumbnails` 子目录。原文件仅记录路径，移动/删除后需重新导入。首次创建资源库时会收录配置中最近使用的、仍存在的素材；已有资源库不会反复导入被用户移除的素材。

资源模型 `WallpaperAsset` 与运行中的 `Scene` 分离。卡片缩略图在后台限流解码，最多 640 像素，没有每卡一个播放器；离开当前三屏范围的缩略图释放托管引用。详情与外部显示继续使用同一个 Scene 模型及 SceneView。`IVideoTransport` 为业务层提供暂停、恢复、定位和时间查询，业务 ViewModel 不接触 MediaPlayer。返回资源库时，没有应用到副屏的纯视频预览会停止以释放资源。

“动态壁纸”“静态壁纸”“我的资源”分别保存当前筛选、页码、已加载屏数、滚动位置及详情对象。进入插件、设备等其他菜单后再返回，会恢复离开前的资源列表或壁纸详情；状态在本次程序运行期间有效。

收藏、标签、分类、轮播顺序、间隔、随机设置和最近播放项写入独立资源索引；重启恢复列表与设置，但不自动启动轮播。资源库初始化前关闭不会覆盖已有索引，索引损坏时保持程序可启动。导入、播放和存储异常记录结构化日志。

`LibraryTests` 覆盖批量导入、去重、损坏/丢失文件、异步缩略图、原文件解锁、新资源置顶、三屏加载与分页、菜单往返详情恢复、收藏与标签搜索、共享场景详情预览、目标输出、列表排序、手动切换、定时自动轮播、丢失文件跳过、断屏禁用、重启恢复和初始化前退出保护。`VideoTests` 增加真实 MP4 资源导入、暂停与定位验证。测试截图位于测试输出目录：`library-gallery.png`、`library-detail.png`、`library-devices.png`、`library-compact.png`。

当前也已接入 D92 Studio 的免费静态壁纸链路。“在线资源”页可连接 Studio API，按名称/描述/标签搜索，按两级分类和标签筛选并分页；列表读取 960×231 缩略图，详情地址使用高清预览。下载校验大小和 SHA-256 后加入本地资源库，并记录云端资源 ID 与版本；服务器发布新版本时显示“有更新”，重新下载会更新同一份本地资源。远端服务器必须使用 HTTPS，本机开发允许 HTTP localhost。付费授权、账号同步和自动后台更新仍属于后续阶段；“应用到副屏”仍是已有的独立全屏 RenderWindow。

## 内置插件管理与 A 股行情

顶部导航新增“插件”。第一版使用受控的内置插件目录，已将工作区 `craft-stocks` 0.10 的 A 股行情能力接入 DeskDisplay 原生场景：可启用/停用插件、编辑 1–20 个股票代码、保存设置、手动刷新、在软件中预览，并使用 USB 页保存的设备、1920×462 画布与旋转直接显示到 D92。

股票画面由 `StockScene` 和共享 `SceneView` 绘制，不加载 Craft、Qt WebChannel 或网页插件宿主。预览、Windows 渲染链路和 D92 JPEG 因而使用同一个 Scene。行情读取沿用旧插件的腾讯报价接口，默认 10 秒刷新；仅在插件页打开或股票场景正在预览/输出时轮询。网络失败时保留最后一次成功行情并显示连接状态，未取得行情时明确显示空状态。

当前股票原生版先实现四卡自选行情布局；旧插件中的大盘页、个股详情、分时曲线、轮播和价格提醒尚未迁入。插件页也暂不执行任意外部 DLL/压缩包，后续应先定义有版本、权限和生命周期约束的插件清单，再增加安装、更新与卸载。`StockSceneTests` 验证股票场景可生成原生 1920×462 D92 JPEG 及非空行情卡片画面。

同一插件页也已接入独立项目 `E:\Codex\MLB\d92-retro-camera-clock` 的“复古相机翻页时钟”。原版 1981×794 摄影底图作为 DeskDisplay 资源打包，五块翻牌继续使用原项目测量的坐标；画面按 1920×462 和可调 `CropY` 裁切。设置包含启用、翻页动画、260/440/760 毫秒速度、12 小时制和纵向位置，并提供演示翻页、软件预览与 D92 输出。

软件预览用约 30 FPS 的场景更新绘制翻页中间帧。D92 继续使用当前完整 JPEG 的 USB 输出链路，约每秒一帧，因此能显示持续变化的时间，但实体屏上的翻页动画流畅度受传输帧率限制。`RetroCameraClockTests` 验证原始摄影素材进入 1920×462 JPEG，且翻页开始和中间阶段产生不同图像。
