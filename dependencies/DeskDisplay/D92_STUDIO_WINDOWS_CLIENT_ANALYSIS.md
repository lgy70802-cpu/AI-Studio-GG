# D92 Studio Windows 客户端现状分析报告

## 1. 总体结论

当前 Windows 客户端实际项目名仍为 **DeskDisplay**，是一套基于 **C#、.NET 8、WPF、MVVM** 的原生 Windows 应用。

核心架构已经形成清晰的数据和渲染链路：

```text
DataProvider
    ↓
Scene
    ↓
SceneView Renderer
    ↓
PreviewHost / Windows RenderWindow / D92 USB JPEG
```

主要特点：

- 桌面预览、Windows 副屏和 D92 USB 输出尽量共用同一个 `Scene`。
- 壁纸、时钟、性能组件、股票组件和复古相机时钟都进入统一渲染体系。
- D92 USB 已实现原生 HID 通信，不依赖 Craft 执行画面输出。
- 本地壁纸库和 D92 Studio Web 在线资源已经建立初步连接。
- 当前“插件”属于内置场景模块管理，还不是支持第三方安装包的通用插件平台。
- UI 已接近完整产品，但页面导航、样式资源和渲染分派仍较集中，后续扩展时需要进一步拆分。

## 2. 技术栈

### 2.1 基础平台

| 项目 | 当前实现 |
|---|---|
| 开发语言 | C# |
| 运行时 | .NET 8 |
| UI 框架 | WPF |
| 目标系统 | Windows 10 19041 及以上 |
| UI 架构 | MVVM 为主，少量 Code-behind |
| 异步模型 | `Task`、`CancellationToken`、WPF Dispatcher |
| 日志 | Serilog + 文件日志 |
| 配置持久化 | JSON |
| 硬件监控 | LibreHardwareMonitor |
| 视频播放 | WPF `MediaPlayer` |
| USB 通信 | Win32 HID、SetupAPI、FileStream |
| Windows 显示器通信 | DisplayConfig、GDI、Win32 窗口 API |
| 在线接口 | `HttpClient` + REST API |
| 测试 | 自定义 Console Test Harness |

项目全局启用了：

- Nullable Reference Types
- Implicit Usings
- TreatWarningsAsErrors
- Per Monitor V2 DPI 感知

### 2.2 依赖控制

客户端整体第三方依赖较少：

- `Serilog`
- `Serilog.Sinks.File`
- `LibreHardwareMonitorLib`

D92 HID 没有依赖 HidSharp，而是直接通过 `hid.dll`、`setupapi.dll` 和 `kernel32.dll` 进行 P/Invoke 调用。这减少了 HID 中间层依赖，但使设备通信模块与 Windows 平台强绑定。

## 3. 项目目录和模块边界

```text
MLB-DeskDisplay/
├─ src/
│  ├─ DeskDisplay.App/
│  ├─ DeskDisplay.Core/
│  ├─ DeskDisplay.Display/
│  ├─ DeskDisplay.Infrastructure/
│  ├─ DeskDisplay.Library/
│  └─ DeskDisplay.Sensors/
├─ tests/
│  ├─ DeskDisplay.SmokeTests/
│  └─ DeskDisplay.DisplayTests/
├─ tools/
│  ├─ DeskDisplay.UsbDemo/
│  └─ D92.ProtocolProbe/
└─ DeskDisplay.sln
```

### 3.1 DeskDisplay.App

负责应用启动、依赖组装、主窗口和业务 ViewModel。

```text
DeskDisplay.App/
├─ App.xaml.cs
├─ ViewModels/
│  ├─ MainViewModel.cs
│  ├─ LibraryViewModel.cs
│  ├─ CloudLibraryViewModel.cs
│  ├─ PluginManagerViewModel.cs
│  ├─ UsbOutputViewModel.cs
│  ├─ DevicesViewModel.cs
│  ├─ WorkspaceViewModel.cs
│  ├─ ImageSceneViewModel.cs
│  └─ VideoSceneViewModel.cs
└─ Views/
   ├─ MainWindow.xaml
   ├─ WorkspacePanel.xaml
   ├─ DevicesView.xaml
   └─ UsbOutputView.xaml
```

`App.xaml.cs` 是手工依赖组装入口。当前没有使用 Microsoft.Extensions.DependencyInjection 容器。

它负责创建和连接：

- 设置服务
- 日志
- 显示器管理器
- 时钟数据源
- 传感器服务
- 场景
- 本地资源库
- 在线资源库
- 插件管理
- USB 输出
- 主窗口

这种方式目前直观可控，但随着账号、授权、更新、插件包、下载队列等模块增加，启动类会继续膨胀。

### 3.2 DeskDisplay.Core

纯领域模型和接口层，不直接依赖 WPF。

主要模型：

- `Scene`
- `ClockScene`
- `ImageScene`
- `VideoScene`
- `DashboardScene`
- `PerformanceScene`
- `StockScene`
- `RetroCameraClockScene`
- `WallpaperAsset`
- `AppSettings`
- `DisplayInfo`
- `UsbOutputSettings`

主要接口：

- `IDisplayManager`
- `IRenderWindowService`
- `ISensorService`
- `ISettingsService`
- `IVideoPlaybackService`
- `IVideoTransport`

这是当前架构中边界最清晰的一层。`Scene` 只维护逻辑画布大小、宽高比和属性通知，不知道具体窗口或硬件。

### 3.3 DeskDisplay.Display

负责画面渲染和所有输出设备相关功能：

```text
DeskDisplay.Display/
├─ DataProviders/
├─ Interop/
├─ Rendering/
├─ Services/
├─ Usb/
└─ Views/
```

它同时承担：

- WPF 场景渲染
- 图片解码
- 视频播放
- 外接显示器枚举
- 全屏输出窗口
- D92 HID 通信
- JPEG 帧编码

这一层逻辑完整，但职责跨度较大。未来设备类型增多时，可以再分成 `Rendering`、`WindowsDisplay`、`D92Usb` 等独立项目。

### 3.4 DeskDisplay.Library

负责本地和在线资源：

- 本地壁纸索引
- 媒体元数据读取
- Windows Shell 缩略图
- D92 Studio REST API
- 云端文件下载和 SHA-256 校验

### 3.5 DeskDisplay.Infrastructure

负责基础设施：

- `%AppData%\DeskDisplay` 数据路径
- `settings.json`
- JSON 原子保存
- Serilog 配置

### 3.6 DeskDisplay.Sensors

封装 LibreHardwareMonitor，向上层提供 CPU、GPU、内存等采样数据。UI 不直接调用硬件监控库。

### 3.7 tests 和 tools

测试目前不是 xUnit/NUnit 项目，而是可执行程序式测试：

- `DeskDisplay.SmokeTests`
- `DeskDisplay.DisplayTests`

辅助工具包括：

- `DeskDisplay.UsbDemo`：独立验证共享场景到 USB 的输出。
- `D92.ProtocolProbe`：协议诊断工具。

## 4. UI 页面组织方式

### 4.1 主窗口结构

主窗口整体分为三层：

```text
┌─────────────────────────────────────┐
│ Logo / 主导航 / 搜索 / 窗口按钮      │
├─────────────────────────────────────┤
│ 当前功能页面                         │
├─────────────────────────────────────┤
│ 轮播控制 / 输出状态 / 播放列表        │
└─────────────────────────────────────┘
```

窗口使用：

- 无系统边框 `WindowStyle=None`
- `WindowChrome` 自定义标题栏
- 圆角边框
- 自定义最小化、最大化和关闭按钮
- Per Monitor DPI

### 4.2 导航方式

主导航项定义在 `LibraryViewModel.Tabs`：

- 动态壁纸
- 静态壁纸
- 在线资源
- 小组件
- 插件
- 我的资源
- USB屏
- 设备
- 设置
- 诊断

导航没有使用 WPF `Frame`、`Page` 或路由服务，而是：

1. `SelectedTab` 转换成 `MainViewModel.SelectedNavigation`。
2. 主窗口和 `WorkspacePanel` 中的多个区域始终存在。
3. 通过 `DataTrigger` 切换 `Visibility`。

例如：

- 资源库列表和详情由 `Library.IsGallery`、`Library.IsDetails` 控制。
- 在线资源页由 `SelectedNavigation == "在线资源"` 控制。
- 其他功能交给 `WorkspacePanel`。
- `WorkspacePanel` 内再根据导航字符串显示首页、插件、设置、诊断、设备或 USB 页。

优点是切换时 ViewModel 和控件状态容易保留，所以壁纸详情页在切换菜单后仍能恢复。

限制是：

- 页面名称是中文字符串。
- 页面之间存在较多字符串耦合。
- 所有页面可能同时保留在可视树附近。
- `MainWindow.xaml` 和 `WorkspacePanel.xaml` 越来越长。
- 后续加入账号、商城、下载管理等页面时维护成本会明显增加。

### 4.3 MVVM 与 Code-behind

大部分交互由 ViewModel 和自定义命令完成：

- `ActionCommand`
- `AsyncCommand`
- `ObservableObject`

Code-behind 目前主要处理 WPF 界面事件：

- 窗口系统按钮
- 资源库滚动位置恢复
- 滚动到底部触发继续加载
- 订阅和解除界面事件

Code-behind 没有直接执行资源业务或 USB 操作，因此仍基本符合 MVVM 边界。

### 4.4 资源库页面

资源库列表直接写在 `MainWindow.xaml` 中，包含：

- 一级分类
- 二级分类
- 搜索
- 批量导入
- 三列壁纸网格
- 三屏渐进加载
- 分页栏
- 轮播控制
- 悬停操作层

缩略图容器固定为：

```text
1920 ÷ 462 ≈ 4.155844
```

图片使用 `Stretch="Uniform"`，因此保持比例完整显示；图片比例不一致时会保留留白。

详情页也在同一个 XAML 中，通过状态切换显示，包含：

- 大图或动态场景预览
- 收藏
- 预览
- D92 输出
- Windows 副屏输出
- 壁纸与组件组合输出
- 显示方式或视频 FPS
- 轮播管理
- 名称、一级分类、二级分类、标签编辑
- 相关壁纸

### 4.5 在线资源页面

在线资源目前是独立区域，但仍位于 `MainWindow.xaml` 内。

功能包括：

- 配置 Studio Server 地址
- 获取已发布资源
- 搜索
- 分类筛选
- 标签筛选
- 服务端分页
- 下载到本地资源库
- 下载并显示到 D92
- 本地版本更新提示

当前接口只接受 `STATIC_WALLPAPER`，还没有覆盖动态壁纸、主题和插件包。

## 5. 样式系统

### 5.1 当前组织方式

`App.xaml` 的全局资源目前为空。

主要样式直接定义在 `MainWindow.Resources`：

- 默认 Button
- `Yellow` 按钮
- `Dark` 按钮
- TextBox
- ComboBox
- `PillList`
- `WallpaperCard`
- `BooleanToVisibilityConverter`

其他视图也有自己的局部样式和模板。

当前视觉语言为：

- 白色主背景
- 黄色 `#FFD21C` 主强调色
- 深色主要操作按钮
- 药丸形导航和按钮
- 大圆角卡片
- `Segoe UI, Microsoft YaHei`
- 浅灰输入框与分隔线
- 壁纸悬停时显示深色渐变及操作按钮

### 5.2 优点

- 不依赖第三方 UI 框架。
- 视觉风格统一度已经较高。
- 样式和业务界面放在一起，早期迭代速度快。
- 控件模板可以完全控制。

### 5.3 当前限制

还没有形成正式设计系统：

- 没有独立 `Themes` 或 ResourceDictionary。
- 颜色值大量直接写在 XAML 中。
- 字号、间距、圆角没有语义化资源。
- `MainWindow.xaml` 同时承担布局、样式、模板和页面内容。
- 相同控件在不同视图中可能产生细微差异。
- 暂无深色主题、主题切换或系统颜色适配。
- 中文字符串同时承担显示文本和导航状态值。

下一阶段如果扩展 Web 账号、VIP、商城和下载管理，建议先抽取：

```text
Themes/
├─ Colors.xaml
├─ Typography.xaml
├─ Controls.xaml
├─ Cards.xaml
└─ DataTemplates.xaml
```

## 6. 壁纸模块实现

### 6.1 数据模型与存储

本地资源模型使用 `WallpaperAsset`，资源库文档存放在：

```text
%AppData%\DeskDisplay\Library\library.json
```

缩略图存放在：

```text
%AppData%\DeskDisplay\Library\Thumbnails\
```

原始壁纸不会复制进资源库，只保存原文件路径。删除资源库条目也不会删除原文件。

壁纸字段包括：

- ID
- 名称
- 文件路径
- 图片或视频类型
- 宽高
- 视频时长
- 一级分类
- 二级分类
- 标签
- 收藏状态
- 添加时间
- 云端资源和版本信息

新导入资源按照 `AddedAt` 倒序显示。

### 6.2 导入流程

`WallpaperCatalog.ImportAsync` 支持 JPG、JPEG、PNG 和 MP4。

导入时：

1. 将路径转换为完整路径。
2. 通过 Windows Storage API 读取媒体属性。
3. 读取图片宽高或视频宽高、时长。
4. 请求最大 640 像素的 Shell 缩略图。
5. 保存缩略图。
6. 将元数据加入 `library.json`。

当前不复制原文件意味着资源库较轻，但用户移动原文件后资源会失效。

### 6.3 缩略图和性能控制

`WallpaperItemViewModel` 实现了：

- 后台加载缩略图
- 最大 4 个缩略图并行加载
- 离开当前可见范围时释放托管图片引用
- 详情图和缩略图分开加载

`LibraryViewModel` 实现了：

- 根据窗口大小计算每屏项目数
- 默认加载一屏
- 下滚逐屏追加
- 连续加载三屏后显示分页
- 分类、搜索、分页状态分别记忆
- 返回资源页后恢复滚动位置
- 新资源优先显示

这种方式已针对几十到数百张壁纸做了基础内存控制，但 `UniformGrid` 并不是真正的虚拟化面板。资源量达到数千时，仍需要换成虚拟化列表或增量数据源。

### 6.4 静态图片渲染

静态图片经 `ImageSceneLoader` 有界解码，然后形成 `ImageScene`。

支持四种显示模式：

- 填充
- 等比完整
- 拉伸
- 居中

实际绘制由 `SceneView.ImageDestination` 计算目标矩形。

同一个 `ImageScene` 可用于：

- 主窗口预览
- Windows 外接显示器
- D92 USB JPEG

### 6.5 动态壁纸

动态壁纸使用：

- `VideoScene`
- `IVideoPlaybackService`
- `WindowsVideoPlaybackService`
- WPF `MediaPlayer`
- `DrawingContext.DrawVideo`

同一个播放器被场景预览和输出视图复用，没有逐帧截图缓存，也没有同时创建两套视频解码器。

当前 D92 USB 输出明确不支持视频。动态壁纸目前主要输出到 Windows 扩展显示器。

### 6.6 在线壁纸

`StudioApiClient` 当前访问：

```text
GET /api/v1/client/resources
GET /api/v1/client/resources/catalog/options
```

下载过程包含：

- 仅允许 HTTPS，localhost 可使用 HTTP
- 最大 25 MB
- MIME 类型检查
- 文件大小检查
- SHA-256 校验
- 临时文件下载
- 校验通过后原子替换
- 本地缓存
- 下载后导入本地资源库

目前是“公开免费静态壁纸”链路，还没有用户登录、权益令牌、VIP 校验和付费资源授权。

## 7. 时钟和组件模块

### 7.1 基础时钟

时钟链路为：

```text
TimeProvider
    ↓
ClockSceneDataProvider
    ↓
ClockScene
    ↓
SceneView
```

`ClockSceneDataProvider` 是应用级单一时间采样源，每秒更新一次 `ClockScene`。

`ClockScene` 一次性提交完整时间戳，再通知时间、日期和星期。这避免了三个字段来自不同采样时刻。

### 7.2 组合场景

`DashboardScene` 组合：

- 静态背景图
- `ClockScene`
- `PerformanceScene`

布局支持：

- 时钟左侧
- 时钟居中
- 分栏布局

它监听子场景变化，然后通知同一个 `SceneView` 重绘。

### 7.3 性能组件

硬件数据链路为：

```text
LibreHardwareMonitor
    ↓
SensorService
    ↓
PerformanceSceneDataProvider
    ↓
PerformanceScene
    ↓
SceneView
```

传感器采样和 UI 刷新分离。数据只有在首页、输出窗口或 USB 正在使用相关场景时才需要高频更新。

### 7.4 复古相机翻页时钟

该时钟目前实现为内置原生场景：

- `RetroCameraClockScene`
- `SceneView.DrawRetroCameraClock`
- 内置 `retro-camera-blank.png`
- 33ms 动画定时器
- 12/24 小时制
- 翻页动画开关
- 动画持续时间
- 垂直裁切设置
- 演示翻页命令

它不是 HTML 页面，也没有嵌入 Craft 或 Qt WebChannel。

## 8. 插件模块实现

插件入口由 `PluginManagerViewModel` 管理。

当前内置两个插件：

1. 股票行情
2. 复古相机翻页时钟

### 8.1 股票插件

实现链路：

```text
腾讯行情 HTTP 接口
    ↓
StockSceneDataProvider
    ↓
StockScene
    ↓
SceneView.DrawStocks
```

支持：

- 沪、深、北交所代码规范化
- 最多约 20 个代码配置
- 定时刷新
- 手动刷新
- 加载状态
- 预览
- 显示到 D92
- 设置持久化

画面采用 1920×462 的超宽设计坐标，最多突出显示四只股票。

### 8.2 当前插件系统的真实边界

目前所谓“插件管理”实际是：

- 内置场景启用或停用
- 内置插件参数配置
- 选择活动插件
- 预览
- 输出到 D92

当前没有：

- 插件目录扫描
- ZIP 安装
- DLL 动态加载
- 独立进程沙箱
- 插件签名验证
- 权限清单
- 插件版本升级
- 卸载生命周期
- Web 插件宿主
- 第三方插件 SDK

因此它现在更接近“内置功能模块管理器”，尚不能作为云端插件商城的执行基础。

## 9. 设备通信模块

设备通信分为两条完全不同的输出链路。

### 9.1 Windows 外接显示器

主要组件：

- `WindowsDisplayManager`
- `DisplaySnapshotReader`
- `TargetDisplayService`
- `RenderWindowService`
- `RenderWindow`

流程：

```text
DisplayConfig / GDI 枚举显示器
    ↓
用户选择目标副屏
    ↓
保存稳定设备标识
    ↓
RenderWindowService
    ↓
无边框顶层 RenderWindow
    ↓
SceneView 渲染
```

显示器枚举优先使用 Windows DisplayConfig，失败后退回 GDI 枚举。

设备匹配优先使用稳定 `DeviceId`，找不到时才使用设备名称和友好名称组合。

`WindowsDisplayManager` 创建隐藏顶层窗口监听显示配置变化和设备插拔变化，通知经过 400ms 防抖。

输出窗口会验证：

- 目标不是主显示器
- 宽高有效
- 目标设备仍然存在

然后通过 Win32 API 精确放置到目标显示器物理边界。

### 9.2 D92 USB HID

设备识别条件包括：

- VID `0x5548`
- PID `0x1011`
- Usage Page `0xFFA0`
- Usage `1`
- 有效序列号
- 足够的 HID Output Report 长度

用户必须选择具体序列号，程序不会自动切换到另一台 D92。

### 9.3 USB 帧发送流程

```text
当前 Scene
    ↓
SceneJpegRenderer 离屏渲染
    ↓
旋转 0 / 90 / 180 / 270
    ↓
JPEG
    ↓
CRT/DRA 协议头
    ↓
按 HID ReportLength 分包
    ↓
HID FileStream
    ↓
D92
```

会话开始时发送：

```text
CRT\0\0DIS
```

运行中约每 10 秒发送：

```text
CRT\0\0CONNECT
```

JPEG 数据使用 `CRT\0\0DRA` 帧头，长度和几何字段使用大端序。

控制包和 JPEG 包通过同一个串行写入链路发送，避免心跳插入一帧 JPEG 中间。

### 9.4 USB 生命周期

`D92HidRenderTarget` 支持：

- 同一时间只运行一个输出任务
- 每次只渲染并发送一帧
- 不堆积过期帧
- 设备拔出后等待
- 按原序列号自动重连
- 发送中的 JPEG 尽量完整结束后停止
- 单次设备写入有独立超时
- Craft 正在运行时阻止并发占用设备

当前刷新间隔约为 1 秒，因此更适合时钟、静态壁纸、性能数据、股票和低频动画，不适合视频和高帧率动效。

### 9.5 当前 USB 支持场景

支持：

- `ClockScene`
- `ImageScene`
- `PerformanceScene`
- `DashboardScene`
- `StockScene`
- `RetroCameraClockScene`

不支持：

- `VideoScene`

D92 分辨率和旋转由设备设置保存，不在渲染核心中固定写死。股票和复古相机场景使用 1920×462 作为设计画布，但最终仍可按输出画布缩放。

## 10. 状态与配置持久化

设置文件位于：

```text
%AppData%\DeskDisplay\settings.json
```

主要保存：

- Windows 目标副屏
- D92 USB 序列号
- USB 画布尺寸
- USB 旋转方向
- 图片显示方式
- 视频设置
- Studio Server 地址
- 股票插件启用状态和股票代码
- 复古相机时钟配置
- 工作区设置

壁纸资源和播放列表单独存放在 `Library/library.json`。

保存采用临时文件加替换方式，降低写入中断破坏原配置的风险。应用退出时也会等待各 ViewModel 完成必要保存和停止输出。

## 11. 架构评价

### 11.1 已经做得较好的部分

- `Core` 不依赖 WPF，领域模型边界清楚。
- 预览、Windows 副屏和 USB 输出共用场景模型。
- UI 不直接访问硬件传感器。
- USB 不自动选择其他设备，设备身份安全。
- 设备热插拔、取消和退出流程已有专门处理。
- 壁纸原文件、缩略图和元数据分离。
- 云端下载包含大小与 SHA-256 校验。
- 动态视频没有重复创建解码器。
- 日志和异常处理覆盖应用、设备和后台任务。
- 当前实现与已经验证的 D92 协议边界一致，没有重新引入未经验证的协议分支。

### 11.2 主要技术债务

1. **页面结构过于集中**

   `MainWindow.xaml` 同时包含导航、资源列表、详情页、在线资源和底部播放控制，继续扩展商城、账号和下载中心会变得困难。

2. **导航使用中文字符串**

   页面状态依赖“插件”“资源库”“USB屏”等字符串，重命名、国际化和类型检查都比较困难。

3. **样式资源没有模块化**

   全局 `App.xaml` 为空，颜色、圆角、间距和模板主要散落在窗口级资源中。

4. **SceneView 使用类型分支**

   `SceneView.OnRender` 通过判断具体 `Scene` 类型选择绘制函数。每新增一种场景都要修改中央渲染器。

5. **插件系统还不具备扩展性**

   内置插件可以正常工作，但无法直接承接 Web 后台未来分发的第三方插件安装包。

6. **本地图库不是真正虚拟化**

   当前三屏加载和缩略图释放能支持一般资源量，但数千条资源时仍会受 `ItemsControl + UniformGrid` 限制。

7. **在线资源协议范围较窄**

   目前只支持免费静态壁纸，没有账号令牌、授权、VIP、动态资源、插件包和增量更新流程。

8. **测试框架较特殊**

   现有可执行测试能验证核心功能，但不具备标准测试框架的筛选、报告和 CI 集成体验。

## 12. 当前成熟度判断

| 模块 | 当前成熟度 |
|---|---|
| WPF 应用骨架 | 较成熟 |
| 本地静态壁纸 | 可用 |
| 本地动态壁纸 | 基本可用 |
| 分类、二级分类、标签 | 可用 |
| 大量壁纸浏览 | 中等规模可用 |
| 时钟和性能组件 | 可用 |
| Windows 副屏输出 | 可用 |
| D92 USB 静态输出 | 已验证可用 |
| D92 USB 实时时钟 | 已验证可用 |
| USB 热插拔恢复 | 已实现 |
| 股票插件 | 内置功能可用 |
| 复古相机时钟 | 内置功能可用 |
| 通用插件安装系统 | 尚未实现 |
| Studio 在线静态资源 | 第一版已接通 |
| 用户登录和授权 | 尚未接入 |
| VIP 和付费资源 | 尚未接入 |
| 在线插件分发和执行 | 尚未实现 |

---

本报告基于当前仓库源码和项目结构生成。分析过程中未修改客户端代码或现有配置。
