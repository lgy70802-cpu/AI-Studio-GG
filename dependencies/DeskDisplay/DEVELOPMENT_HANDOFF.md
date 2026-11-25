# DeskDisplay 开发交接（2026-10-08）

## 2026-10-13：横竖资源浏览体验修复

- Studio 的封面生成改为按方向选择 960×231 或 231×960 边界，inside 等比缩放且不放大小图，不把竖图放进横屏留白画布。新增 wallpaper-preview.ts 和方向单元测试。
- 使用 API 工具 refresh-portrait-covers 重建本地已有 4 份错误方向的竖屏封面，原始图片、版本、权益不变，旧封面资产保留；资源更新时间改变以刷新客户端缓存。
- Windows 缩略图缓存区分服务器 origin、ID、版本和 updatedAt；本地解码不会把 231px 竖屏缩略图放大到 960px 宽，缓存文件仍 OnLoad 后释放。
- 在线卡片名称、尺寸、下载状态分行，标题可悬停查看完整名称；列表增加状态和加载提示。正常筛选空结果显示真实空状态，只有服务器失败且无资源的全部资源页保留演示；未登录收藏/授权页提示去个人中心登录。
- 资源清单在相关查询完成并检查取消 token 后才提交，减少旧请求覆盖新筛选。保存并连接服务器会清除旧分类/标签/搜索/详情状态并重新读取目录。
- Release 构建 0 警告/0 错误；真实 API 的完整客户端回归 290 项通过；后台 6 项单元/HTTP测试与 PostgreSQL integration 通过。截图 online-portrait-optimized.png、online-empty-optimized.png；日志 artifacts/resource-experience-20261013.log。
- 新测试包版本 0.1.0-preview.20261013.1，位置 artifacts/releases。未进行 D92 实体画面验收。

## 2026-10-13：Windows x64 自包含测试发布包

- tools/Publish-WindowsTest.ps1 生成版本 0.1.0-preview.20261013 的 win-x64 自包含发布目录和 ZIP；不启用裁剪，包含 WPF/.NET 8.0.25 运行时。包约 79MB、488 份应用文件，files.json 包含每份文件 SHA-256。
- tools/packaging/Install.ps1 支持每用户安装到 LOCALAPPDATA/D92Studio/app，校验发布文件哈希，默认创建桌面快捷方式；无需管理员安装。Uninstall.ps1 只删除带正确产品标记的 app 目录，并只移除目标匹配的快捷方式，保留 APPDATA/DeskDisplay 配置、图库和缓存。
- 在 artifacts/install-test-* 隔离配置目录验收：安装成功、已安装 EXE 启动且窗口响应、正常关闭、卸载成功、测试用户数据保留；真实 APPDATA 设置前后哈希相同。
- 发布包未包含服务器、数据库、个人设置、资源索引或 .env。在线页默认 localhost 仅适合同机开发，其他电脑需配置实际 HTTPS API 地址。
- 包位置 artifacts/releases/D92Studio-0.1.0-preview.20261013-win-x64.zip；这是脚本安装/解压运行测试包，不是签名的 Setup.exe 或 MSI，也没有加入 Windows“应用和功能”注册。完全干净 Windows 机器、厂商环境卸载后的 D92 能力尚未实测。

## 2026-10-13：详情、下载任务与大图库收尾

- 在线详情增加分类、标签、版本与下载状态。云端版本替换保留本地名称、分类、标签、收藏、AddedAt、资源 ID 及轮播成员/顺序；替换内容后重新读取图片尺寸并释放旧缩略图与详情缓存。
- 在线筛选区新增可展开“下载管理”，会话内最多保留 50 条任务，显示进度、成功/失败/取消状态及重试入口；任务记录实际资源和服务器，跨服务器不允许重试旧任务。下载错误展示校验/权限/网络或导入失败类别。
- 当前页批量更新逐项执行并汇总成功、失败和未完成数量；单项失败不会终止其他项目，取消会停止后续更新。
- 在线页的视口尺寸计算忽略 32px 以下变化，避免滚动条导致反复重载。首页和在线页本地缩略图使用 OnLoad 解码，立即释放缓存文件句柄，回归确认窗口截图后仍能清理隔离测试目录。
- Release 构建 0 警告/0 错误；连接真实本地 Studio 的完整 DisplayTests 共 287 项通过。500 份隔离图库的初始化加连续筛选测试约 20ms，当前显示 12 张，最多展开三屏后分页。这个结果是测试数据下的自动化检查，不等同于所有真实视频/超大图片的内存和长时间性能验收。
- 测试日志 artifacts/download-management-20261013.log；截图 DisplayTests 输出目录 online-download-management.png。没有修改真实用户资源索引，未发送 D92 HID 命令。

## 2026-10-13：真实测试资源与下载管理验收

- Studio 本地后台已发布 8 份明确标记 `[验收测试]` 的生成素材：横屏/竖屏各 4 份，覆盖 FREE、VIP、PURCHASE、PURCHASE_OR_VIP，归入“验收测试 / 横竖屏测试”。重运行后台 scripts/publish-test-resources.mjs 不覆盖已有资源。
- 后台单项和批量发布不再受旧 FREE-only 限制；所有资源仍需要已上传版本，非免费原包下载由服务端身份和权益校验保护。真实支付不在本轮范围。
- 在线详情支持取消下载、失败后重试；列表显示已下载/有更新，详情提供更新入口，顶部可更新当前页已下载且有权限的资源（不是跨全部服务器目录的批量更新）。导入未完成时不会误报下载成功。
- 明确设置 DESKDISPLAY_STUDIO_TEST_URL=http://127.0.0.1:3000 后，DisplayTests 会连接真实 Studio：检查横竖清单、权限状态、完整图片尺寸/哈希，使用隔离临时本地库检查重复导入和版本替换。测试不写入用户的资源库或账号会话。
- Release 构建 0 警告/0 错误；282 项客户端检查通过，真实 PostgreSQL 接口测试通过。日志 artifacts/cloud-live-acceptance-20261013.log。
- 本轮只读 HID 枚举为 []，未发送实体 HID 图像，D92 可见画面验收待设备重新连接。软件重新启动后可直接浏览本地后台测试资源。

## 2026-10-13：账号链路修复与在线资源筛选分页

### 同日数据库恢复与真实接口验收

- 已找到 Studio 项目既有 `scripts/local-database.mjs`，用它恢复 `.test-postgres/local-dev-data` 中便携 PostgreSQL，未覆盖 `.env`、密码或已有资源存储。
- Prisma deploy 确认 4 个迁移均已部署、无待执行项；尺寸回填 scanned=0、failed=0。
- 增强 PostgreSQL integration 测试并通过：真实普通用户注册/身份查询、Bearer 不能访问管理员接口、收藏与取消收藏、匿名/无权益下载拒绝、有效 VIP 下载、VIP 过期拒绝、单购有效下载、权益撤销拒绝、退出后旧 token 失效。测试仅创建并清理随机夹具，不模拟真实支付。
- Studio API 和 Vite 通过 `npm run dev` 运行，健康检查 database=up；后台 http://127.0.0.1:5173 返回 200。本地库当前没有已发布横屏资源（total=0），在线空态是实际数据结果。
- 软件进程检查响应正常。本轮未发送真实 HID 命令，也未重新验证 D92 实体画面。短时授权链接、付费离线许可及真实支付仍未实现。

- 修复个人中心凭据编辑后命令可用状态、独立主题资源加载、重复 Dispose 和离线退出显示；服务器地址修改后清空原账号显示。
- Windows DPAPI 会话按规范化服务器地址分开保存；不继续使用旧无服务器归属的令牌。测试使用独立临时会话文件，不访问真实用户令牌。
- 在线页新增“全部资源 / 云端收藏 / 已授权资源”，请求带 Bearer 身份；服务端筛选收藏和有效的单资源授权。资源库“已购买”显示已下载且有服务器资源授权的项目；不表示所有 VIP 可用内容或完整订单记录。
- 在线页根据窗口资源区估算首屏，滚动逐步展开到三屏后显示分页；服务端按三屏容量分页。离线合法下载资源仍保留。
- Studio API 修复无 Origin 的原生客户端写请求被阻止问题；浏览器外来 Origin 和管理员写请求仍受原规则限制，新增原生请求、管理员隔离和有效权益查询测试。
- Release 构建 0 警告/0 错误；配置 SmokeTests、完整 DisplayTests 和 API 5 项单元/HTTP 测试通过。个人中心截图在 DisplayTests 输出目录 studio-account.png。
- 当前 Studio .env 已存在，指向 localhost:5432/d92；Prisma deploy 连接失败。本轮未部署迁移、回填宽高或完成真实 PostgreSQL 账号/收藏/付费下载集成验收。已向用户询问数据库运行方式。
- 短时授权下载链接、离线付费授权凭证、完整订单同步和批量更新尚未实现；当前下载由服务端 Bearer 会话实时校验权益，不可宣称商业化闭环已全部完成。

给接手的 Codex：先读 AGENTS.md、本文、README.md，再读 artifacts/d92-device-investigation.md 和 artifacts/d92-wake-heartbeat-result.json。请接续当前实现，不重新从零研究 D92，也不要把历史失败当成最终结论。本文是开发上下文交接，不是聊天逐字导出。

## 目标与范围

用户希望开发自己的 D92 管理软件，减少对 MiraBox Craft 的依赖。主项目是 C# / .NET 8 / WPF / MVVM，架构为 DataProvider → Scene → Renderer → RenderTarget。共享 Scene 用于预览、Windows 外接显示器、D92 USB 输出。

当前实现：Windows 显示器识别和目标保存；时钟、图片、视频、性能场景；本地壁纸资源库；主窗口“USB屏”页的设备选择、画布尺寸、旋转、启动/停止和状态。USB 支持时钟/图片/性能，约每秒一帧，暂不支持视频。USB 直连不会创建 Windows 扩展桌面，不代表已经替代 Craft 的虚拟显示驱动。

工作区还有 craft-hello、craft-aquarium、craft-lyrics、craft-stocks 历史 Craft 插件项目，本次一起保留，但不是 DeskDisplay 主程序的构建依赖。依赖与构建看各目录 package.json/README/锁文件。

## 最重要的硬件结论

- 2026-09-30 用户确认：Craft 主进程不运行，DeskDisplay 主程序连续约 6 分钟显示正确方向的时钟，秒数持续更新。
- 此前大量“写入成功但黑屏”的关键问题是缺少唤醒/心跳。仅发送唤醒命令、没有断电或重发 JPEG，用户就确认屏幕亮起且有画面。
- 生产实现每次连接发送唤醒 CRT\0\0DIS，运行时每 10 秒发送 CRT\0\0CONNECT 心跳。控制报文和 JPEG 必须通过同一个写入流程串行发送，不能插进一帧中间。
- 正常停止先完成正在发送的帧，再关闭连接；单次传输保留独立 5 秒超时。不应直接用用户停止 token 截断 JPEG。
- JPEG 默认质量 90 已显示通过。早期质量 85 失败受到待机状态干扰，**没有证据认定质量 85 不兼容**，不要再次把黑屏简单归因于 JPEG 质量。
- 实测断电冷启动、相同序列号重连；部分早期热插拔仅有传输恢复日志，没有实体视觉确认。不能把 USB 写入成功当作画面验收。
- 原电脑仍装有厂商驱动与 MiraboxCraftPersistentService。尚未验证完全卸载厂商环境的新电脑，不能承诺只装我们的程序就已实测通过。

## 本次设备档案（配置数据，不是通用代码常量）

- VID/PID：5548:1011；序列号：C511D3782B05。
- HID UsagePage FFA0 / Usage 1；输出 report 长度 1025，Report ID 0；实际连接必须重新枚举并读取能力。
- 用户已确认的逻辑画布 1920×462，输出像素旋转 90°，线上 JPEG 为 462×1920。
- 不假设 Display 2；不硬编码 D92 分辨率；不沿用旧 HID 路径；更换实体设备需选择其新序列号。
- CRT/DRA 完整帧头 32 字节，JPEG 长度+32 为大端；偏移12实测B1，全屏几何字段为0。Report ID 后每份报告承载最多1024字节头/数据，尾部补零。详见代码和 fixtures。
- 唤醒/心跳来源为本机 SDLibrary1.dll 离线反汇编，SHA256 533926144e928d5c3e2c0012b52b086922bcd9c884f14228042fb3208782c397。RVA 唤醒0x31310、心跳0x22830。库版本变化不可沿用偏移。

## 关键代码

- src/DeskDisplay.Display/Usb/：D92FrameEncoder、D92HidDevice、D92HidRenderTarget、SceneJpegRenderer。
- src/DeskDisplay.App/ViewModels/UsbOutputViewModel.cs：UI dispatcher 渲染、后台传输、设置和退出生命周期。
- src/DeskDisplay.App/Views/UsbOutputView.xaml：USB 页面。
- App.xaml.cs 组合服务；MainViewModel 协调共享场景和画布；DevicesViewModel 在打开 Windows 输出前等待 USB 停止；WorkspaceViewModel 的全局停止/休眠取消 USB。
- AppSettings.UsbOutput 保存设备序列号、宽高、旋转。使用 ISettingsService.UpdateAsync，避免覆盖其他设置。USB 启动不自动输出。
- tools/DeskDisplay.UsbDemo：--list 只枚举；--self-test 离线；--snapshot plan.json output.jpg 离线；--send-static plan.json image.jpg 实机；--wake serial 实机；单个 plan 参数打开实时钟诊断窗口。
- DESKDISPLAY_USB_CAPTURE_DIRECTORY 是显式诊断环境变量：每次会话最多保存前三张真实输出 JPEG。默认关闭，不要在日常运行中随意启用。

## 已有验证与下一步

2026-09-30 Release 构建 0 警告/0 错误，207 项显示回归检查通过。日志在 artifacts/d92-wake-heartbeat-regression.txt。测试必须运行 DeskDisplay.DisplayTests.exe，不能只 dotnet 加载测试 DLL，否则 Per-Monitor V2 清单不生效。

最后一次实体结果见 artifacts/d92-wake-heartbeat-result.json，持续发送日志 d92-heartbeat-main-live.log。6分钟不代表长期稳定性通过。下一步先在新电脑复现构建和离线检查，再视 D92 是否接在新电脑上进行唤醒、实时显示、停止/重启和热插拔验证；功能扩展由用户决定。

## 环境与迁移注意

- 原项目路径 C:\Users\Administrator\Documents\ChatGPT\MLB；新电脑可用其他路径。不要复制代码中的机器路径作为新机器配置。
- 原 SDK 8.0.425，位于 %LOCALAPPDATA%\DeskDisplayBuild\dotnet。新电脑安装 Windows .NET 8 SDK，优先使用 PATH 中的 dotnet。主项目不需要 Python、Frida、Node 才能构建。
- 旧调查脚本可能依赖原 SDK/Python 路径及 D:\Program Files (x86)\D92\MiraBoxCraft。先检查脚本参数和路径再执行。本机旧 DLL 不在包内，普通构建无需它。
- **不要再次使用 Frida 附加 Craft**：此前发生过访问冲突，不能排除观测影响。现有原生 HID 实现不加载厂商 DLL。
- 配置在 %APPDATA%\DeskDisplay，迁移包附有独立副本，不自动覆盖目标电脑配置。当前没有图片/视频文件路径和资源库索引；以后引入外部媒体时要单独迁移。
- 打包前仓库没有任何 Git 提交，文件主要为 untracked；仅 git clone/push 空仓库会遗漏成果。迁移包直接包含未提交文件，不包含空 .git、bin/obj/node_modules/缓存或 Codex 登录凭据。
- 用户尚未提供目标机器的文件系统访问；本次交接包已在源机准备，不能宣称已在目标电脑安装或验收。

## 用户协作偏好

用中文，直接推进已授权工作，常规可逆操作不要反复要求许可。物理显示结果需要用户确认。不要声称当前已连接硬件、程序仍在运行或测试仍通过：先检查新机器的当前状态。

## 2026-10-08：D92 一键应用与静态壁纸轮播

- 小组件页增加“显示到 D92”，支持时钟、图片和性能；资源库静态壁纸详情增加相同入口。入口复用 USB屏 页保存的序列号、任意画布尺寸及旋转，缺少配置时跳转 USB屏，不自动选择第一台设备。
- D92 已输出时切换场景或壁纸复用同一个 HID 会话，下一帧读取共享 Scene；不会为了每张壁纸断开重连。Windows 副屏入口继续保留。
- 资源库底栏增加轮播目标：Windows 副屏 / D92 USB。D92 支持顺序、随机、间隔、上一张和下一张，仅处理静态 JPG/PNG；视频、丢失文件与解码失败图片跳过。
- D92 断开后 USB 目标继续等待同一序列号，轮播暂停切换；收到恢复状态后可继续。停止显示、USB页停止、休眠、退出会停止轮播并释放 USB。唤醒不自动恢复轮播。
- 轮播目标写入 WallpaperLibrary.RotateToUsb；仅保存偏好，不自动启动。
- 新增 UsbLibraryTests，覆盖直接应用、无 Windows 副屏、任意画布、旋转、连接复用、视频排除、顺序/随机、无效文件、定时切换、断连恢复、停止/休眠与持久化。Release 构建 0 警告/0 错误；19 项配置检查和 229 项显示回归通过。
- 测试截图在 DisplayTests 输出目录：usb-library-detail.png、usb-library-compact.png。当前主程序已启动供用户检查；本轮自动测试使用模拟 USB 目标，D92 实体壁纸和轮播画面仍需用户观察确认。

## 2026-10-09：壁纸＋组件组合场景

- 新增共享 `DashboardScene`，把有界静态图片、现有 `ClockScene` 和现有 `PerformanceScene` 组合到一个画布；没有新增时钟 Timer 或传感器采样线程。
- 小组件页新增“组合”，支持“时钟居左”“时钟居中”“时钟与性能”三种布局、四种图片模式和性能数据显示开关。资源库静态壁纸详情新增“壁纸＋组件到 D92”。
- 预览、Windows 副屏和 D92 USB 使用同一个组合 Scene；D92 继续复用已保存的序列号、任意逻辑画布、旋转和正在运行的 HID 会话。
- `DashboardImagePath`、`DashboardLayout`、`DashboardShowPerformance` 随现有设置保存；重启仅恢复预览，不自动启动 D92。
- 新增 DashboardTests 并扩展 UsbLibraryTests，覆盖恢复、三种布局、可选性能数据、时钟刷新、共享 Windows 输出、D92 旋转 JPEG、持久化和资源库直接应用。截图：dashboard-workspace.png、dashboard-clock-left.jpg、dashboard-split.jpg。
- 2026-10-09 Release 构建 0 警告/0 错误；19 项配置检查及 240 项显示回归通过。组合场景自动测试使用模拟 USB 目标，尚未进行 D92 实体画面确认。

## 2026-10-10：资源库加载、分页与页面状态

- 本地壁纸新增 `AddedAt`，新导入资源按时间倒序显示；旧版索引缺少时间时按原有追加顺序生成稳定时间，不覆盖现有名称、分类、收藏、轮播或设备配置。
- 缩略图从 UI 属性同步解码改为后台解码，最多 4 个并发任务；静态图仍从原图等比解码至 640 像素，不锁定源文件。页面只保留当前最多三屏卡片的缩略图引用。
- 首屏数量根据资源区宽高和三列 1920∶462 卡片计算。滚动触底依次加载第二、第三屏，三屏加载完后显示分页栏；切页回到顶部。
- “动态壁纸”“静态壁纸”“我的资源”分别保存筛选、页码、加载屏数、滚动位置、列表/详情状态和当前壁纸。离开到插件、设备等菜单后返回，可恢复原详情页。
- `LibraryTests` 新增新资源置顶、异步缩略图、三屏后分页、下一页和菜单往返详情恢复检查。2026-10-10 Release 构建 0 警告/0 错误，255 项显示回归及 19 项配置检查通过。

## 2026-10-09：插件管理第一版与股票插件接入

- 主导航新增“插件”，第一版采用内置插件目录；A 股行情支持启用/停用、自选代码、保存、刷新、预览和“显示到 D92”。设置键为 `StockPluginEnabled` 与 `StockPluginSymbols`，通过 `ISettingsService.UpdateAsync` 合并写入，不覆盖已有设备、壁纸或资源库配置。
- 复用了 `craft-stocks` 0.10 的代码规范、数据接口和 D92 超宽视觉规则，但没有嵌入其 Craft/Qt WebChannel 网页宿主。新增原生 `StockScene`、`StockSceneDataProvider` 和 `SceneView.DrawStocks`；同一 Scene 可用于预览及 USB JPEG。
- 数据提供器最多接受 20 个 sh/sz/bj 代码，六位 6 开头映射上海、0/3 开头映射深圳；请求 12 秒超时，防止重叠刷新。网络异常保留上次数据并更新状态，插件停用时关闭定时刷新。
- 当前只迁入四卡自选行情。大盘、详情、真实时间分时、轮播、价格提醒以及外部插件包安装/更新/卸载仍是下一阶段边界。
- Release 构建 0 警告/0 错误；完整 DisplayTests 与 SmokeTests 通过。新增回归验证股票画面生成 1920×462 JPEG。运行时可访问性树已确认主窗口和“插件”导航入口；Windows 图形捕获通道超时，因此没有把截图捕获当作自动验证结果。当前程序已启动。D92 实体股票画面仍需用户观察确认。

## 2026-10-09：复古相机翻页时钟接入

- 源项目位于 `E:\Codex\MLB\d92-retro-camera-clock`。接入的是该独立插件，而不是 `craft-hello` 中外观相近的木纹日历翻页时钟。
- 将原始 `retro-camera-blank.png` 作为 `DeskDisplay.Display` WPF Resource 打包；`RetroCameraClockScene` 和 `SceneView` 使用原项目 1981×794 源尺寸、五块牌测量坐标及 1920×462 裁切关系。没有运行 Craft、Qt WebChannel 或网页插件宿主。
- 插件页现在列出复古相机时钟和 A 股行情。相机设置包含启用、动画、260/440/760 ms、12 小时制、CropY、演示、预览和 D92 输出；设置通过现有合并写入保存。
- 软件预览 DispatcherTimer 约 30 FPS，仅在插件页可见或该场景正在预览/输出时更新。D92 仍按完整 JPEG 约 1 FPS 发送，不能把软件中的平滑翻页视为实体屏已具备同样帧率。
- Release 构建 0 警告/0 错误；DisplayTests 共 251 项通过。新增测试确认摄影底图进入 1920×462 JPEG，且翻页中间帧确实变化；输出检查图为 DisplayTests 目录的 `retro-camera-clock.jpg`。D92 实体画面和长时间运行仍需用户确认。

## 2026-10-10：D92 Studio 免费静态壁纸接入

- 主导航新增“在线资源”，服务器地址默认 `http://127.0.0.1:3000`，可保存到现有 AppSettings；仅本机开发允许 HTTP，远端必须使用 HTTPS。进入在线资源页才联网，服务器不可用不会拖慢程序启动。
- 客户端读取 Studio 已发布免费静态壁纸清单，预览后可“下载到我的资源”或“下载并显示到 D92”。下载限制 25MB，按清单验证文件大小与 SHA-256，通过后才原子写入 `%APPDATA%\DeskDisplay\CloudCache`；损坏缓存自动重新下载，失败临时文件会清理。
- 下载后复用现有 WallpaperCatalog，归类为“云端资源 / 官方”，并继续使用既有详情、收藏、轮播、组合场景和 USB 输出链路，不另建资源库或 USB 协议实现。
- Studio 管理后台已实现真实 PNG/JPEG 上传、私有 StorageProvider 存储、版本、免费发布、公开列表/预览/下载。真实 PostgreSQL 16.14、迁移、seed、API 集成及浏览器上传发布下载流程通过。
- DeskDisplay Release 构建 0 警告/0 错误；262 项显示回归和 19 项配置检查通过。新增 6 项 Studio 客户端检查：清单、大小/SHA 校验、缓存命中、损坏缓存替换、远端明文 HTTP 拒绝、失败临时文件清理。
- 2026-10-10 完成真实端到端验收：便携 PostgreSQL 上的 Studio 后台发布 `D92 云端验收·水族馆`（2557×615 PNG，2,005,984 字节），公开客户端下载内容与 SHA-256 清单一致；用户确认 DeskDisplay 下载并输出后，D92 实体屏正常显示水族馆画面且方向正确。
- 同日继续完成云端资源库正式版基础：Studio 增加两级分类、标签、搜索、推荐排序、资源编辑/下架和版本更新；上传时用 sharp 0.35.5 生成 960×231 JPEG 缩略图及最大 2048 像素高清预览，原图仍存私有存储用于校验下载。DeskDisplay 增加搜索、分类/标签筛选、24项分页、缩略图列表、本地云版本记录及“有更新”提示。第二个 Prisma 迁移已在真实 PostgreSQL 16.14 部署通过；完整 262 项显示回归、19 项配置检查、浏览器流程和 0 漏洞依赖审计通过。

## 2026-10-11：UI V3 第三阶段在线资源、插件页与性能回归

- 在线资源下载现在显示真实字节进度，并继续在写入缓存前校验大小与 SHA-256；下载成功明确加入“我的资源库”。清单可选读取 `minimumClientVersion` 与 `supportedDevices`，旧服务器缺少字段时保持兼容；不兼容资源禁止下载并显示原因。
- 股票行情与复古相机翻页时钟从旧 WorkspacePanel 抽离到独立 PluginManagementView，保留启停、设置、预览、刷新/演示和 D92 输出，预览与 USB 继续共享原 Scene。
- 本地资源仍采用每页最多三屏、缩略图最多四路后台解码、离屏释放引用；新增批量分类/标签与清空选择回归。在线列表启用回收虚拟化提示，列表缩略图继续使用服务端 960×231 文件。
- Release 构建 0 警告/0 错误；SmokeTests 与完整 DisplayTests 通过。新增云端兼容性/下载进度及资源批量更新检查。实际 WPF 启动检查修复 ProgressBar 只读属性绑定方向后通过，窗口保持响应。
- 当前本机 Studio Server 未运行，在线页会显示连接失败而不影响本地资源、插件或 D92。实体 D92 本轮未重新发送画面，不新增实体显示结论。

## 2026-10-11：首页、资源库与在线资源演示空状态

- 首页、我的资源库和在线资源页增加 6 组纯 UI 演示案例，全部明确标记“演示案例”或“不可下载”，不写入资源索引、不伪装成云端响应，也不能执行下载、预览或 D92 输出。
- 首页在没有真实最近使用或云端推荐时显示演示卡；真实数据出现后自动替换。资源库仅在本地索引为空时显示演示网格。在线服务器离线或无内容时显示演示网格及说明。
- 资源库批量编辑栏只在选中项目后显示，减少默认页面拥挤；真实卡片、分类、分页与详情逻辑不变。
- Release 构建 0 警告/0 错误，SmokeTests 与完整 DisplayTests 通过；真实 WPF 启动验证通过。当前 Studio Server 未运行，在线页按设计进入演示离线状态。

## 2026-10-11：在线资源两级分类导航

- 在线资源筛选按 UI 参考图改为横向胶囊导航：顶部资源类型栏当前启用“壁纸”，时钟、插件、主题明确禁用；第一行显示 Studio 根分类，第二行按 ParentId 显示所选根分类的子分类。
- 一级“全部”清除一级及二级筛选；二级“全部”仅清除子分类。API 请求优先发送所选子分类 ID，否则发送一级分类 ID。搜索、标签和服务器地址保留在同一筛选卡内。
- Studio 离线时显示两行不可操作的演示分类及演示资源，真实分类加载成功后自动替换。演示内容不进入下载或 D92 输出链路。
- Release 构建 0 警告/0 错误，SmokeTests 与完整 DisplayTests 通过，回归日志为 artifacts/ui-v3-online-category-regression.log；实际 WPF 启动通过。

## 2026-10-11：UI V3 首页、资源库与在线资源视觉对齐

- 首页主预览增加轻量设备画框，继续直接承载真实共享 Scene；设备状态、横竖模式、最近记录和云端推荐的数据来源均未改变。
- 我的资源库按参考稿重排为标题操作区、资源类型、搜索、两级分类和三列超宽网格。卡片固定为 1920∶462 比例并使用 `Uniform` 完整显示；名称常驻，收藏和“显示到 D92”仅悬停出现。批量勾选与批量工具栏仅在批量管理模式显示。
- 在线资源采用资源类型、一级分类、二级分类、搜索/标签及排序的分层布局；支持“推荐/最新”排序。“热门”没有服务端热度数据，因此未伪造。服务器地址收纳到“连接设置”，时钟、插件、主题保持禁用状态，避免虚构尚未接通的云端能力。
- 新增标准 1320×900 与紧凑 1100×740 的 WPF 离屏截图回归，覆盖资源库与在线资源；截图输出在 DisplayTests 的 Release 目录。演示素材仍明确标识为演示，不写入资源库或云端数据。
- 实机启动后发现资源库 `ScrollChanged` 与自适应每屏数量在滚动条宽度变化时形成布局反馈循环，导致 UI 线程持续 `Refresh` 并被 Windows 标记为未响应。视口计算现忽略小于 32px 的布局抖动，同时保留真正窗口缩放时的重新计算；新增滚动条宽度回归检查。修复后进程连续监测 20 秒均响应，空闲 8 秒仅增加 0.02 秒 CPU。
- 首页设备栏在“设备设置”左侧新增“轮播管理”入口，并增加独立 CarouselManagementView。专页复用 LibraryViewModel 的真实播放列表、顺序、输出目标、间隔、随机模式和启停命令，可查看/移出壁纸及调整顺序；空列表可直接跳转资源库添加。没有复制轮播状态或新建输出链路。
- 首页“最近使用”和“为你推荐”统一为两列三行、每区最多 6 项，缩略图容器固定 4∶1。最近使用仍取真实历史；推荐按 Studio 资源 `UpdatedAt` 倒序取最新 6 项，网络不可用时继续显示明确标注的 6 个演示案例。
- 轮播管理列表改为两列卡片网格：卡片上方是 4∶1 素材预览，下方左侧显示标题及“资源类型 · 二级分类”，右侧为“移出轮播”。当前播放列表模型只接收壁纸，因此现有条目类型显示“壁纸”；布局字段已独立，后续时钟、插件、主题进入统一播放项模型后可复用。
- 我的资源库升级为三层资源中心：资源状态（全部、收藏、历史、下载、购买、创作）、资源类型（全部、壁纸、时钟、插件、主题）和壁纸专属两级分类。收藏、历史、云端下载与本地创作使用真实本地字段；已购买在账号权益未接通时显示明确空状态。列表为两列，横图预览 4∶1，竖图预览 1∶4并保持完整显示；两列布局的每屏数量和三屏分页容量同步重算。
# 2026-10-11 在线资源横竖版式与服务端筛选

- 在线资源一级导航右侧新增“显示比例”，支持“横屏超宽 / 竖屏超窄”。
- 横屏资源使用两列 4:1 卡片；竖屏资源使用六列 1:4 卡片，图片保持 `Uniform` 完整显示。
- 客户端请求新增 `orientation=landscape|portrait` 与 `sort=recommended|newest`，切换条件会从第一页重新请求。
- D92 Studio API 的资源响应新增 `width`、`height`；`ResourceAsset` 新增可空尺寸字段和迁移 `202610110001_resource_asset_dimensions`，新上传的静态壁纸会保存原图尺寸。
- 旧资源没有尺寸数据时继续归入横屏结果，避免迁移后从现有资源列表消失；旧竖屏资源需要重新上传或后续执行尺寸回填后才能进入竖屏筛选。
- 验证：DeskDisplay Release 构建 0 警告/0 错误；SmokeTests、DisplayTests（含横竖在线资源截图）通过；D92 Studio Admin 构建及 API 单元测试通过。
- 当前机器未配置 `d92-studio-admin/.env` / `DATABASE_URL`，因此数据库迁移与接口集成测试未执行。配置现有环境后运行 `npm run db:deploy` 和 `npm run test:integration`，不要用示例文件覆盖既有生产配置。

# 2026-10-11 在线资源完整浏览链路与权益展示

- 在线资源保留“壁纸 / 时钟 / 插件 / 主题”一级导航，并把“横屏超宽 / 竖屏超窄”固定在同一行右侧。一级、二级分类继续由 Studio API 动态加载和联动。
- 在线详情页显示高清预览、尺寸、兼容性、资源说明、版本更新说明及免费/VIP/付费状态；免费资源可下载到我的资源库并继续复用现有 D92 输出链路。
- Studio 目录现在允许浏览所有已发布静态壁纸，返回 `accessPolicy`、最低客户端版本、支持设备及最低有效商品价格。非免费资源的预览可访问，但原始包下载仍由服务端拒绝；客户端不会信任本地 VIP 标志，也不会误授予下载权限。
- 在线筛选改为可取消请求：连续切换横竖屏、分类、标签或排序时取消旧请求并执行最后一次选择。列表保存滚动位置，详情页在切换到其他菜单再返回后仍保留。
- 在线缩略图最多四路并发下载并缓存到 CloudCache/Thumbnails；后续加载复用按资源版本区分的本地缓存。失败时保留远端 URI和空态，不阻塞整个列表。
- 服务端新增 `backfill:dimensions` 工具，通过配置的私有 StorageProvider 读取历史 PACKAGE、解析真实尺寸并回写；可重复执行，只处理缺少宽高的资源。
- 验证：DeskDisplay Release 构建 0 警告/0 错误，SmokeTests 与完整 DisplayTests 通过；Studio Admin 全构建和 API 单元测试通过。当前工作区没有服务器 `.env`/`DATABASE_URL`，迁移、历史回填及 PostgreSQL 集成测试尚未在本机执行。


# 2026-10-13 轮播管理：编辑、播放状态、多套方案

- 轮播卡片保持两列；支持拖动到目标卡片位置排序，Ctrl/Shift 多选后批量移出。展开“批量添加本地壁纸”可多选加入，忽略重复、未知和丢失文件。移出仅修改方案，不删除原文件。
- 显示当前/最近播放、下一项、倒计时、运行/暂停/停止/等待重连状态。随机模式不预测下一项。暂停只暂停切换，保留输出；继续使用剩余时间。
- 新增 CarouselPreset，保留旧 Playlist 等字段兼容 Version=1 数据。默认方案来自旧列表；另存为新方案复制当前列表、间隔、随机、输出目标，支持重命名和停止时切换。重启不自动播放。方案随现有原子 catalog 保存流程持久化；删除资源会清理各方案引用。
- 验证：Release 构建 0 警告/错误；SmokeTests 通过；DisplayTests（包含本地 Studio API）297 项通过。新增拖动重排、批量去重、方案独立性、暂停保持画面、恢复倒计时及重启持久化检查。原有 USB 假设备断线恢复/无效图像跳过检查通过。
- WPF 自动化截图：tests/DeskDisplay.DisplayTests/bin/Release/net8.0-windows10.0.19041.0/carousel-management.png（1320×900）。真实鼠标拖动与实体 D92 尚待人工验收；本轮没有发送真实 HID 命令。轮播内容仍为现有壁纸，不虚构插件/时钟轮播或 USB 视频支持。

# 2026-10-13 菜单与本地分类切换性能修复

- 定位：Main.SelectedNavigation 原先触发 Library.Refresh 整表重建，刷新重复通知分类/卡片 ItemsSource，且每次释放筛选外全部缩略图；NotifyCarousel 还在忙碌/设备通知中枚举所有文件存在性。DetailImage 原先在 WPF 绑定 getter 中同步读取解码。
- 修复：导航仅通知页面可见性，Scene 仅通知预览状态；VisibleItems/Categories/Subcategories 保持稳定引用，仅序列变化时发送通知；不再逐张发无关元数据通知。候选列表只在实际 Refresh 时更新，文件有效性在加入/播放时校验。保留最多72项近期缩略图缓存并保护当前页、最近使用、详情资源。
- 详情图片使用后台解码、四路共用并发门限、Freeze 后在 Dispatcher.Background 发布，加载期间显示缩略图；代际校验防止旧请求在释放后回写。原文件流完成解码即关闭。
- Release 构建0警告/错误；完整 DisplayTests 300项通过（含本地 Studio API）；SmokeTests通过。新增验证：菜单切换不更换资源/分类集合引用、分类返回复用缩略图、详情解码释放 UI 调度线程；原有分页、详情保留、轮播和模拟 USB 重连测试通过。没有发送真实 HID 命令。
- 尚未量化真实用户图库点击延迟；在线 CachedImageConverter 首次缓存解码仍为同步，后续若在线页首屏仍卡需单独测量优化。

# 2026-10-13 在线资源筛选区排版整理

- 资源类型导航及同一行右侧横竖比例保留；资源范围改为轻量下划线标签；类型/排序与搜索对齐，一级和二级分类分行显示。选中状态统一浅蓝底/蓝字，未选项透明背景，分类和标签支持换行。
- 删除空白标签下拉框，真实标签采用具名列表，无选项整行隐藏；修复服务器首次加载标签后的属性通知。分类“全部”随实际选中状态高亮。未开放类型仍不可操作，连接设置保持折叠。
- Release 构建0警告/错误；完整300项DisplayTests通过，含在线详情状态保留、标准/1100px紧凑尺寸、竖屏布局、下载与权限回归。已查看online-resources-compact.png截图确认对齐。

# 2026-10-18 创作中心 V1 首版

- 已先核对实际仓库：Windows .NET8/WPF/MVVM，未发现 macOS/Avalonia 客户端；查看 ui-reference/07-editor.png。原创作入口之前为插件管理，现增加 CreatorView，旧插件管理通过独立 AppPage.Plugins 保留。
- 新增纯 .NET ThemeProject/ThemeLayout/ThemeLayer/ThemeEditSession 和 ThemeScene。横竖设计坐标分开，80步撤销、一次拖动一个事务、100图层上限；素材继续引用 WallpaperCatalog 的现有 ID 与路径。ThemeProjectStore 仅保存项目源与真实渲染封面，没有复制资产数据库。
- 首页、横/竖共用编辑器、预览应用页可运行；支持本地/已下载在线壁纸、数字时钟/日期/星期、文字、图片、矩形/椭圆，以及已启用的股票/复古相机组件。支持点选、拖动、四角缩放、旋转、图层拖动排序、锁定/隐藏、复制/粘贴、键盘微调、缩放/平移、折叠侧栏和动态属性。
- SceneView 增加组合绘制分支；主题预览、真实封面、Windows 副屏和 USB JPEG 共用 ThemeScene。USB 仅扩展可支持场景类型检查；包含视频的主题明确拒绝。未修改 D92HidDevice/D92FrameEncoder/D92HidRenderTarget 协议实现，未发送真实 HID 命令。
- 基线：已有288项 DisplayTests通过；最初构建与测试并发造成文件占用，测试退出后重建恢复。最终 Release 构建0警告/错误，SmokeTests通过，完整DisplayTests312项通过（其中24项创作专项）。本轮未启用真实Studio API集成测试环境变量，没有变更API契约。
- 关键回归：真实图片引用与裁剪保存、独立横竖位置、拖动事务撤销、锁定、复制粘贴、图层顺序、时钟不同时间输出不同JPEG、设计尺寸不被设备输出尺寸覆盖、Windows输出复用同一Scene、USB视频拒绝、缺失素材检查、损坏源项目保留、竖屏封面在横屏物理输出后保持正确比例。
- WPF截图：DisplayTests Release目录 creator-home/landscape/portrait/compact/preview.png；标准1320×900、紧凑1100×740已检查。测试源项目位于独立临时目录，不写入真实用户项目库。
- 使用与边界见 CREATOR_CENTER_V1.md。模糊未实现；插件沿用全局配置；未下载云端资源先经既有权益下载流程。真实D92组合输出/长时间稳定性、鼠标交互与125%/150%系统DPI、macOS实现尚未验收，不推导为支持视频或自动重连已经跨平台验证。

# 2026-10-20 创作中心第一轮六项体验优化

- 已执行：素材图标分类、搜索、真实空态与静态/动态标识；图层行内重命名、隐藏/锁定按钮和拖动落点高亮；属性按对象分组且锁定禁用输入；缩放百分比、居中、边缘/中心吸附与旋转四角缩放；横竖模式高亮/独立布局提示及跨布局复制；未保存/保存中/成功/失败反馈。
- 新增纯.NET ThemeGeometry（旋转锚点与吸附）和 CreatorExperience（WPF体验适配），沿用原ThemeProject源模型与资源库，不修改HID协议、不添加USB视频能力。
- 重要修复：名称文本框点击同步选中图层；Ctrl+S先提交名称输入。图层锁定时WPF属性控件实际禁用；旋转缩放对角锚点保持不变。拖动仅在布局真正变化时通知画布比例，避免重复排版调度。跨布局复制以新ID等比尺寸/相对中心定位，原目标内容不覆盖，复制一次撤销。
- 保存源项目失败阻止离开；封面失败不误报源项目丢失，原子替换封面。程序关闭先完成创作源保存，再关闭其它服务；保存失败保留可编辑窗口，时钟恢复，允许重试。
- 验证：Release构建0警告/错误；SmokeTests通过；完整DisplayTests332项通过（创作专项44项）。新增旋转四角多角度锚点、吸附阈值、搜索空态、重命名/选中、WPF锁定/解锁、跨布局复制撤销、保存状态、封面锁定、源保存失败与关闭重试检查。测试使用临时独立项目/素材，没有发送真实HID命令。
- 标准1320×900与紧凑1100×740截图已查看。日志：artifacts/creator-polish-build.log、creator-polish-smoke.log、creator-polish-regression.log。真实Studio API可选集成未启用；真实D92、真实鼠标长时间交互与125%/150%系统DPI仍需验收。
- 使用说明增量见 CREATOR_CENTER_V1.md；自动恢复草稿、缺失素材修复等第二轮工作没有在本轮扩展。

# 2026-10-20 创作中心第二轮 1/2/3/4 项

- 独立草稿：15秒后台检查、修改版本去重、原子存储；新会话首页恢复提示；恢复为新ID副本、先保存恢复副本草稿再清理旧草稿；正式保存清理草稿；坏草稿保留。自动保存不覆盖正式主题。
- 素材修复：横竖布局合并检查、PNG/JPEG真实解码、同类型重新定位；复用现有WallpaperCatalog记录/导入；共享旧引用一次更新、一次撤销；云端来源标记不被修复降为本地。没有建立第二套资产数据库。
- 源项目/.d92theme包导入导出：可识别本地素材按SHA256去重；云端/未知/缺失文件不打包。白名单、单文件/总量/元数据限制、路径/重复条目/符号链接/可执行条目拒绝、哈希校验与失败清理。导入生成新项目/素材目录并复用原资源库，先保留为恢复草稿，不自动覆盖或发布正式主题。
- 六个真实可编辑模板：时钟日期、文字、图片标题各有横竖两种；独立Scene动态预览，图片槽明确需要用户选择。
- 新文件：ThemeDraftStore、ThemeTransferService、ThemeTemplates、CreatorProjectManagement、CreatorManagementTests。ThemeProject新增可选AssetOrigin/CloudResourceId，保持Version1旧模型默认兼容；没有改动HID协议实现。
- Release构建0警告/错误、SmokeTests通过、完整DisplayTests363项通过。实际15秒自动保存、恢复/修复/迁移安全/模板检查均包含；可选真实Studio服务检查未启用。截图已查看：creator-draft-recovery/creator-templates/creator-missing-assets/creator-compact.png。
- 说明和限制见CREATOR_CENTER_V1.md末尾。本轮没有发送真实HID命令，实体D92、macOS、真实DPI和大项目长期运行仍待验收。


# 2026-10-20 创作中心第三轮 1/2/3/4/5/6 项

- 完整流程增加真实本地图片+实时时钟、独立横竖编辑、保存重开、打包与实际VM导入、模拟Windows输出和未连接D92拒绝检查。
- 多选/包含式框选/图层勾选、组移动/复制/粘贴/删除、六种对齐和两种等距分布，锁定成员保持不变；旋转视觉包围盒，Shift单元素等比缩放；一次手势一次撤销。
- 素材复用现有库，按名称/分类/子分类文本/标签搜索，方向/来源/分类组合筛选；紧凑窗口筛选收纳为Expander。未下载云端内容仍走在线权益流程。
- 系统字体搜索、预设/自定义颜色和真实时钟数字固定推进宽度；滑块手势合并撤销；图片填充选项中文。项目支持真实重命名/复制/删除、名称搜索及排序。
- 同文件图层共用解码，128MiB有界缓存；键包括路径/长度/修改时间。100图层、30共享图片图层、20轮撤销、100帧渲染+切页和8个编辑器释放GC检查通过。复杂帧约150ms仅为本机样本，不声称高帧率或长期稳定认证。
- 构建0警告/错误、SmokeTests通过、完整DisplayTests401项通过；最终标签调整后再次执行构建和创作专项。日志creator-workflow-build/smoke/regression/focused.log；截图creator-multiselect/workflow-compact.png。
- 素材原件和配置保留。首次不存在Drafts目录时的保存清理/项目删除边界已修复。没有真实HID发送；D92/macOS/实际DPI/长时间及多张不同大图仍待人工验收。详细记录见CREATOR_CENTER_ACCEPTANCE.md。


# 2026-10-20 创作中心第四轮 1/2/3/4/5/6 项

- 平级组合GroupId、取消组合、组ID粘贴隔离、整体等比缩放（含字号/间距）、旋转成员包围盒与一次撤销；画布右键菜单。跨布局复制单元素清理组关系。不实现嵌套组和组旋转。
- 独立可调整大小素材窗口，真实资源库12项分页/两列/横4:1竖1:4、搜索、来源/方向/两级分类；素材替换保留布局样式并可撤销。RGB/Alpha颜色选择器、字体样例、数值步进、宽高锁定、首页方向筛选及不覆盖原正式源的另存为。
- ThemeProject/Layout/Layer快照独立对象复制，所有现有可变对象分离；未来新增可变引用字段需扩展Clone。有界512项文字排版缓存键包含DPI/文化/颜色/字体。3张独立4096x1024图片后台加载、UI调度和切换后GC释放通过；原100图层/100帧/释放检查继续通过。
- Release0警告/错误，SmokeTests通过，完整DisplayTests428项通过；最终样式/另存为修正后创作专项140项通过。日志creator-advanced-build/smoke/regression/focused.log；截图creator-group-editor/material-browser/color-picker.png。
- 本轮实际发送HID：通过扩展既有UsbDemo读取ThemeProject并复用生产Scene/Renderer/HID链路。原配置1920x462/90°横屏60帧1连接无传输错误；交换逻辑宽高462x1920/90°的竖屏测试9帧9连接8次写入取消等待，持续输出未通过，已停止；恢复横屏时初始等待约4秒，随后25帧1连接无传输错误。未写用户设备配置，未改协议/驱动/固件。
- 不得把诊断退出码0、首帧成功或超时自动重连当成竖屏/人工拔插验收通过；实体视觉确认尚未收到。该失败仅对应具体宽高/旋转组合，不可推断所有竖屏方案均失败。下一轮需独立定位，当前不重新探索协议。
- 证据creator-device-landscape/portrait/restore.jsonl和creator-device-acceptance.json；UsbDemo --self-test 6项通过。详细记录CREATOR_CENTER_ADVANCED_ACCEPTANCE.md。macOS、真实DPI、小时级长稳、视觉及物理拔插仍待验收。

# 2026-10-21 创作中心第五轮六项收尾

- 主题方向通过ThemeOutputGeometry保持原设备编码尺寸，映射逻辑宽高/旋转，不覆盖保存配置、不改HID协议。原竖屏462x1920/90失败历史保留；462x1920/0短测40帧及30分钟实测通过。
- 组合名称/折叠/整组锁定隐藏、整体旋转/对齐画布/样式复制、吸附与缩放尺寸反馈；素材大图/双击/状态恢复/缺失警告；最长边640缩略图与共享72项温缓存。
- Release构建0警告/错误，完整455项DisplayTests、最终167项创作专项、SmokeTests通过。新包0.1.0-preview.20261021.1已生成并启动。
- 实体测试09:36:22—10:06:22，1782帧、1连接、0错误，Stopped退出0。用户确认方向走秒正常、拔下约5秒接回恢复。但日志无WaitingForDevice且连接未增加，人工视觉通过与日志重连未验证必须分开，不能称为完整热插拔机器证据。
- 实际单显示器1600x900/96DPI；125%/150%仅模拟渲染，真实不同DPI多屏未验收。详细文件/截图/日志及边界见CREATOR_CENTER_STABILITY_ACCEPTANCE.md。

# 2026-10-21 离线开发：文件夹批量修复

- 新增CreatorFolderRepair.cs，通过原资源库与ImageSceneLoader匹配所选目录同名失效素材；仅当前目录、不模糊搜索，保留未匹配/不支持/损坏引用。横竖布局一起修复，一次撤销；云端来源标记不降低，原文件不移动，布局样式不改变。
- 入口CreatorView“素材检查→从文件夹批量修复”；CreatorProjectManagement初始化及命令通知接入。候选登记完成前不改项目源；登记失败可能保留已登记素材，不自动删除。
- 新增CreatorManagementTests四项检查；Release0警告/错误、完整459项DisplayTests、SmokeTests通过。日志creator-folder-build/regression/smoke.log；发布包0.1.0-preview.20261021.2。无实体HID命令，物理热插拔日志验证继续待办。

# 2026-10-21 批量修复预览

- 选择文件夹后先生成只读ThemeRepairPlan，在CreatorRepairPreview独立窗口显示逐项结果，取消不登记素材/不改项目。确认后重新扫描校验项目ID与匹配列表，变化则拒绝执行；执行复用原批量修复，一次撤销。
- 增加无匹配/同名歧义/类型不符/损坏图片解释；无可修复项禁用按钮。UI使用现有主题资源，路径可滚动和悬停查看。
- 最终Release0警告/错误、174项创作专项和SmokeTests通过；本轮未重跑无改动模块完整回归，上一轮完整459通过。首次独立弹窗资源缺失已修复并复测。截图creator-repair-preview.png已查看。日志creator-repair-preview-build/tests/smoke.log；包0.1.0-preview.20261021.3。无真实HID命令。

# 2026-10-21 创作效率六项

- ThemeProjectStore覆盖前备份旧源，History/<ID>/最多10份受管理版本；备份失败不替换正式源，外来损坏文件保留。独立CreatorSourceBrowser预览真实Scene，恢复为新ID副本并写草稿。Templates/保存源引用，首页浏览并创建副本，不复制资产数据库。
- 素材修复逐项勾选/真实图片预览/关闭释放/选中引用应用；整组重排含上移下移、锁定阻止、组内次序保留；循环定位隐藏锁定图层并展开组/滚动。
- ThemeLayout每方向最多32条设计坐标参考线，Clone/Validate/持久化兼容旧源；标尺拖出、移动、双击/拖出删除，单次撤销；位置吸附包含组，未旋转自由尺寸吸附保持锚点。等比/旋转尺寸不强行吸附。编辑覆盖层不输出到设备。
- ThemeExportInspection逐项报告本地/云端/未知/缺失/尺寸与格式/视频/插件依赖，保存MD；打包前确认继续。CloudResourceId存在时即使旧源标Local也禁止打包。Dispose幂等修复独立预览关闭边界。
- 完整489项回归通过；最终报告排版/历史保留/组命令调整后Release0警告错误、202项创作专项、SmokeTests通过。日志creator-six-regression/final-build/final-focused/smoke.log。截图creator-six-history/repair/guides/export.png已检查；包0.1.0-preview.20261021.4。
- 本轮无实体HID命令；未进行macOS、真实多DPI/长时间鼠标操作或物理重连日志验收。模板删除重命名界面未做，历史只备份源不备份素材。详见CREATOR_CENTER_PRODUCTIVITY_ACCEPTANCE.md。

# 2026-10-21 图片拖动交互修正

- ThemeCanvas原先所有Wallpaper命中均框选，导致从壁纸素材入口添加的图片即使选中也不能直接拖动；现选中且未锁定背景进入普通拖动。普通元素/已选背景不再被穿过它的参考线截走点击。未选背景仍可框选，标尺带保留参考线创建。
- 新增三项策略检查覆盖选中背景、参考线交叉与锁定背景。日志creator-drag-build/tests.log；包0.1.0-preview.20261021.5。没有修改设备协议或发送真实HID命令。
- 最终Release0警告/错误，205项创作专项通过。真实鼠标长期操作仍需人工体验；不把命中策略检查当作实体设备验证。

# 2026-10-21 四角缩放

- ThemeCanvas缩放命中移至标尺/参考线处理之前，统一旋转坐标和10px界面命中范围、最近角标选择，并增加缩放鼠标指针。新增6项命中回归（3种比例/4角、标尺冲突、旋转、锁定）。
- Release0警告/错误，211项创作专项通过。日志creator-resize-build/tests/publish.log；包0.1.0-preview.20261021.6。实际运行进程3220仍为20261021.1，待新版发布后正常关闭保存并切换。无实体HID命令。
- 旧版3220已通过CloseMainWindow正常退出，随后启动20261021.6（进程19092），新进程正常响应；没有强制结束旧进程。

# 2026-10-21 默认等比、Shift自由缩放

- ThemeCanvas按Shift反转默认缩放；默认关闭独立尺寸吸附以保比例，Shift自由+Alt绕过。背景壁纸未选中也可直接拖动（锁定除外）。全部10种ThemeElementKind复用移动/四角缩放。
- ResizeLayerFromCorner基于手势初始快照同步文字/数字时钟/日期/星期字号字距；图片自由缩放置Stretch并入同一撤销。ScaleGroup/ScaleSelection增加可选preserveAspect参数，默认兼容既有调用；自由模式独立缩放尺寸与中心位置，保留原旋转，不声称支持斜切。
- Release0警告/错误、256项创作专项和配置SmokeTests通过；完整回归正在执行，结果后补。日志creator-uniform-build/tests/regression/smoke/publish.log。包20261021.7。无真实HID命令。
- 完整回归最终544项全部通过（退出0）；运行包20261021.7生成后切换新版。此前“正在执行”记录由本条结果更新。
- 旧窗口正常退出后已启动20261021.7，进程21020正常响应；未强制结束或更改设备设置。

# 2026-10-21 第1/2/4/5项画布体验

- CreatorNavigationTools：循环定位画布外元素、保留尺寸/组内相对位置移回，超大居中；整组选中置顶置底、锁定保护、一操作一撤销，右键菜单和Ctrl+Home/End。
- CreatorViewport：滚轮锚定指针设计坐标、空格左键/中键平移、失活与捕获释放；真实WPF事件验证不改源坐标。视口边界处允许锚点被合法滚动范围限制。
- ThemeEditorViewport：按项目ID+方向原子存储zoom/归一化scroll/精确selection与focus，250ms合并与重复去重，关闭等待最新任务。坏文件回退、过期ID忽略、组内单成员选中精确恢复，方向Undo/Redo恢复相应状态。不污染源/草稿/历史或原设置。
- 初版检查发现关闭期间最后写入可能被新任务替换，Flush改为等待当前最新任务；旧测试固定右键7项改成实际命令绑定检查。
- 最终Release0警告/错误、完整562项DisplayTests和配置SmokeTests通过；日志creator-navigation-final-build/regression/smoke.log，前轮专项creator-navigation-tests.log。截图creator-navigation-state.png已查看，源与素材为隔离临时目录。
- 包0.1.0-preview.20261021.8。未做第3项裁剪和第6项应用确认。本轮无真实HID命令，物理重连日志和真实多DPI跨屏仍待设备/环境验收。详见CREATOR_CENTER_NAVIGATION_ACCEPTANCE.md。
- 已正常关闭旧窗口并启动20261021.8（进程5064），新窗口响应正常；未强制结束旧进程。

# 2026-10-21 创作工作台第1—6项

- 静态图片双击/项目工具裁剪窗口，Fill/CropX/Y/ImageZoom真实渲染与原比例恢复，一次撤销不改原件；Zoom默认1、范围1—8。旧客户端可能忽略新增参数，不声称跨版本/跨平台效果一致。
- CreatorApplyConfirmation实际Scene预览，目标/方向/有效几何/问题列表，每秒和确认时刷新，最终Apply再次校验；USB忙碌/未连接/视频、缺失资源、插件未启用、Windows非主屏目标无效等阻止。轮播界面启动也先确认。
- 网格1—500设计单位参与吸附，抽稀绘制且不进入输出；NumericExpression有限四则运算无脚本求值，NumericStepper复用MVVM转换。
- 批量透明度/适用颜色字体/锁定解锁，跳过不适用或已锁对象，一次撤销。
- Themes/theme-playlist.json仅项目ID/顺序/10—86400秒/目标，最多100项，不新建资产库。独立播放实例加载后交换Scene，复用现有输出，保存编辑项目不被轮播替换；失效/插件/USB视频跳过。真实DispatcherTimer10秒及模拟Windows输出通过；停止切换保留当前画面，管理窗口关闭不停止；新会话不自动输出。USB实机轮播和重连尚未验证。
- 自定义模板管理重命名/确认删除，不删原项目素材；ThemeProject.Category源保存、首页分类与名称/分类搜索。管理操作禁用重入。
- 完整587项回归通过；收尾后Release0警告/错误、28项工作台模块及SmokeTests通过。日志creator-workbench-final-build/regression/final-tests/module/smoke/publish.log；5份creator-workbench-*.png为真实WPF隔离测试截图，已检查。
- 包0.1.0-preview.20261021.9，详见CREATOR_CENTER_WORKBENCH_ACCEPTANCE.md。本轮无实体HID命令，不改协议/驱动/固件/资源下载权益。
- 已正常关闭旧窗口并启动20261021.9（进程9580），窗口响应正常；未强制结束旧进程。


# 2026-10-21 整体体验第1—8项

- 新增AsyncPreviewImage：后台最长边960解码、4并发、72项/估算64MiB共享缓存、卸载控件引用释放；主页/在线/创作封面接入。图库过滤去重复线性查找；创作素材筛选缓存随真实资源库更新；项目/草稿JSON后台解析。
- 搜索合并最新请求、取消结果分类不提交、等待刷新包含后续任务；返回详情恢复滚动；下载错误不污染目录状态、失败任务原位重试、汇总/清除结束记录；导入前重查取消。
- 创作Shift方向键10单位/Ctrl+D、捕获失败复位及LostCapture处理；关闭等待草稿、草稿文件名/ID校验、异常保留提示；素材兼容性文字和100层上限提示。
- 主题轮播当前/下一候选/倒计时、暂停继续与暂停时手动下一项，主轮播页显示主题状态；不改HID协议。
- 完整623项通过、最后新增截图就绪断言后Studio30项通过、新增体验21项和配置SmokeTests通过；应用构建0警告错误。本地真实PostgreSQL API集成1项/单元6项/类型检查通过，未改后端源码或.env。
- 改造前鼠标捕获断言失败；当前原生桌面无法捕获，应用已安全降级，测试明确BOUNDARY，不能声称实际拖动验收通过。静态云端分发通过不代表云端时钟/插件/主题和支付完成。
- 测试包20261021.10，日志client-experience-*，详见CLIENT_EXPERIENCE_ACCEPTANCE.md及实际WPF截图。实体D92长测/热插拔日志、真实多DPI/长时间操作仍待验收。

- 20261021.10已使用PowerShell7完成发布（5.1不支持GetRelativePath）；ZIP SHA256为14811E75D78E1A0569DA170230701FD9D4FBF153655EC6667D3C984C6C85D031。正常关闭9580后启动新版3372，窗口正常响应；本地API及PostgreSQL保持运行，未覆盖已有配置。


# 2026-10-21 实体D92主题轮播与热插拔验收

- 新增显式工具tools/DeskDisplay.HardwareAcceptance，复用生产Creator/UsbOutput/Scene/HID；A/B是可编辑源+系统时钟，用户配置读SHA并隔离写入。没有改业务/协议/驱动/固件。工具构建0警告错误。
- 横屏360.69秒338帧、2连接、17个主题显示事件（含首项）；用户拔插时真实枚举消失20次、WaitingForDevice1次、重连至第89帧后持续至338，reconnectEvidence=true。用户确认方向走秒及拔插恢复正常。
- 竖屏120.24秒120帧、1连接、6个主题显示事件（含首项）、无等待/失败；生产映射462x1920/0，原设置1920x462/90保持。用户确认竖屏方向/文字/秒数正常。两轮用户settings SHA256未变。
- 暂停/继续/手动切换已执行；机器日志和用户确认分开保留，不覆盖以前缺失热插拔机器证据的历史记录。本轮补齐本机横屏短时物理重连证据，仍非小时级/休眠占用/竖屏物理拔插/macOS认证。
- 详见D92_HARDWARE_ACCEPTANCE_20261021.md及artifacts/hardware-{landscape,portrait}-20261021/。测试结束已停止诊断USB输出，准备重新打开20261021.10。
已重新打开20261021.10，进程16420、窗口DeskDisplay、Responding=True；诊断连接已释放，未自动启动新的USB输出。


# 2026-10-21 在线资源参考布局

- 主窗口顶部增加在线专用搜索，左右复用真实分页命令；搜索支持Enter/图标、从详情返回列表。侧栏在线资源点击弹出壁纸/时钟/插件/主题，键盘可开关，强类型OnlineResourceKind，选中高亮。
- 一级下紧接二级；方向/排序/布局在右上，标签/连接/刷新/更新收进⋯，下载任务底部展开。横向2/3列、竖向6/8列，4:1及1:4完整等比显示；真实ComboBox模板与圆角剪裁。
- 云端仍仅静态壁纸；其他类型明确未开放并禁用搜索/下载，不伪造业务。保持旧详情和筛选状态；不改协议/驱动/固件，不发HID、不改用户配置。
- 最终构建0警告错误、完整643项通过，收尾后Studio49项/配置SmokeTests通过；真实WPF截图已检查1320×900/1100×740/横竖和菜单，未新增真实多DPI验收。日志online-reference-*。
- 包20261021.11，SHA256 5BFA4138EE493765DEC9CB35702846713707591C0830D8DAC7AA78B395F78B2D。详见ONLINE_RESOURCES_REFERENCE_ACCEPTANCE.md。
已正常关闭旧版16420，启动20261021.11，进程16596，窗口正常响应；未强制终止旧进程。

# 2026-10-22 多设备与主题自适应阶段 1

- 用户确认架构报告后创建 feature/multi-device-foundation 分支，保留全部既有工作区改动，未提交或推送。只新增 Core/Devices、Themes、Adaptation、Output 契约与纯规则，以及 net8.0 的 DeskDisplay.AdaptationTests；解决方案只添加测试项目。
- Device/DisplayProfile/DeviceProfile/ThemeDocument V2/LayoutProfile/OutputSessionSnapshot 分离；实际表面像素与编码旋转显式验证。身份缺号/重复返回需要确认；尚未接入真实发现、人工绑定或租约。
- 真实比例匹配、显式覆盖、1%默认可配置容差；Contain/Cover/基础Responsive锚点和设计单位边距，诊断旋转裁切/旧Stretch；不写源。V1内存读取保留全部图层配置、资产/云端引用、组和参考线，确定性横竖LayoutId；不写V2文件或设置。
- 改造前后构建均0警告错误；最终新增63项、既有DisplayTests611项、配置SmokeTests19项通过。详见MULTI_DEVICE_FOUNDATION_ACCEPTANCE.md及artifacts/multi-device-*日志。
- 虚拟测试的单目标断开/人工重新连接/独立停止和一万次模拟tick，不等于自动重连、生产调度、小时级内存、真实双D92或macOS验收。没有发送HID、改协议/驱动/固件、覆盖用户配置；没有重启应用。
- 生产App/单输出Scene/渲染器本轮未接新契约；下一阶段先做Registry、每设备独立会话/场景、原子租约和旧入口统一互斥，再逐步上设备UI与编辑器；不要把本轮Core完成误认为已有真实多设备输出。

# 2026-10-22 多设备输出会话阶段 2

- 新增Core DeviceRegistry、进程内租约、OutputSessionManager；每目标独立驱动/取消/状态/清理，异步准备后复核连接epoch，等待设备保留归属，旧回调不污染新会话；处理准备失败、停止、驱动/释放异常和外部阻止。
- Windows文件句柄锁经真实第二进程验证；旧D92HidRenderTarget和RenderWindowService也检查同一设备锁。只改输出循环外的归属保护，不改协议、分包、唤醒、心跳和5秒完整帧写入超时。
- WindowsThemeSessionFactory创建独立ThemeScene/renderer/window，SceneView按ResolvedLayout等比变换复用旧绘图。图片引用计数+冻结像素共享、256MiB保留预算，插件复制现有数据到独立Scene。真实时钟按系统时间更新。新路径拒绝视频、未知实时能力和不足一秒帧周期的已知吞吐配置，旧视频路径保留。
- App仅在DESKDISPLAY_MULTI_DEVICE=1时装配新发现/会话服务并等待正常退出；默认关闭，不自动新建输出会话。旧UI继续单会话兼容；新设备卡片/批量应用/编辑器尚未接入。不发布、不重启正在运行的软件，不覆盖配置或主题，不提交推送。
- 最终构建0警告错误；Core适配/会话95项、Windows完整640项（新增会话29项）、配置19项通过；真实WPF/JPEG尺寸/旋转/像素/走秒检查、可控Windows窗口断开恢复及旧入口冲突通过，发送器和显示器为替身，没有发送实体HID命令。一次完整回归发现测试误用Started作为首帧就绪，修正显式等待后最终退出0。
- 详见MULTI_DEVICE_SESSIONS_ACCEPTANCE.md和artifacts/session-*。尚未验收真实单/双D92、多显示器热插拔、小时级内存/吞吐或Mac；USB/Windows同一物理屏人工关联/冲突组、无唯一ID确认UI仍待补，不能称所有物理冲突已解决；图片预算也不是整个进程内存上限。

# 2026-10-22 多设备管理页（阶段3的设备UI部分）

- 新增DeviceManagementView两列Soft UI卡片和ManagedDevicesViewModel。设备页读取真实Registry与已保存/当前预览主题，逐台应用/停止、所选批量应用/停止、每卡独立状态与错误、未选卡片禁用批量按钮、异步准备期间仍可停止。WorkspacePanel接新页，旧DevicesView保留折叠区。未实现创作中心目标预览/多布局编辑，不称整个阶段3全部完成。
- App默认启用发现/管理UI但不自动新建输出；DESKDISPLAY_MULTI_DEVICE=0回退旧入口。原USB参数仅给唯一且精确匹配serial的设备作初始值，其他USB填0等待用户设置；系统副屏读取实际像素且不额外旋转。正常关闭等待页面操作并取消新会话。
- 独立DevicePreferenceStore保存%APPDATA%/DeskDisplay/managed-devices.v1.json，只显式保存名称/尺寸/旋转，合并已有设备，原子写+句柄锁，未知/坏格式保留。没有改用户原settings或主题，不保存自动恢复/确认状态。
- 人工确认限定当前观察epoch，端点或身份/断连变化撤销。已发现的重复serial候选可锁定本次路径；候选消失不回退他机、I/O断开后要求重确认，并额外持有serial共享锁，保守地同serial仅一个输出。空serial仍被旧枚举排除，不声称已支持；系统镜像/歧义阻止。未改协议报文/分包/心跳/固件，没有实机HID发送。
- 构建0警告错误，完整Windows回归661项、Core适配/会话95项、配置19项通过；收尾按钮和准备取消检查后最终页面专项25项通过并再次构建。661是此前完整回归计数，25是最终专项，不相加冒充一次运行。真实WPF离屏1100x740/1320x900截图已查看，数据明确为离线测试替身。
- 详见MULTI_DEVICE_PAGE_ACCEPTANCE.md；日志artifacts/device-page-*，截图artifacts/device-management-{1100,1320}.png。未发布/重启运行中的软件、未提交推送。真实双D92、manual路径、系统屏热插拔、多DPI/长测、Mac仍未验收；HID与Windows同物理屏的持久关联/冲突组尚未接，页面提示只选一种连接，不能宣称物理冲突已完整解决。
- 下一轮建议做创作中心目标设备预览与布局选择，保持源主题只读适配；随后补跨端点关联、关联持久化和完整V2布局写回。

- 收尾纠正真实主窗口集成：原DeviceManagementView本就绑定MainViewModel，已保留为外层导航容器；新增ManagedDevicePanel绑定DevicesViewModel，保留实际USB导航按钮。补真实MainWindow导航/按钮断言及截图，避免仅独立控件通过却主窗口错绑。去除多设备页旧单设备总状态文案，卡片各自显示真实状态。
- 最终修正后的完整Windows回归667项通过（含设备页27项）、Core95项和配置19项通过；最后纯文案清理后页面27项再通过并重新构建0警告错误。主窗口图artifacts/device-management-shell-1320.png，均为明确的离线测试设备/源。前述661/25为收尾前历史计数，不作为最终结果。

# 2026-10-22 创作中心按设备预览、诊断与应用

- 创作中心预览页增加“按设备预览与应用”，读取真实设备卡片；只读主题快照，根据实际像素/方向选择布局。完整显示/等比裁切与输出共用场景准备及布局变换，系统时钟正常更新；预览不持有设备租约，不修改源/脏状态。
- 增加裁切、留边、字体缺失、旧Stretch等诊断；缺素材失败禁用当前目标应用。当前/所选应用复用独立会话与确认/占用检查，逐卡结果；关闭预览释放资源但不停止已应用会话。
- 最终构建0警告错误；Windows完整686项（新增预览19项）、Core95项、配置19项通过。真实主窗口入口与横竖离屏截图已核对；截图明确使用离线测试替身。
- 详见CREATOR_DEVICE_PREVIEW_ACCEPTANCE.md及artifacts/creator-device-*。未发HID、改协议或用户配置，未发布/重启/提交推送。尚无V2多布局写回/版本编辑、跨端点物理绑定；真实双D92、多DPI/长测和Mac仍待验收。

# 2026-10-22 多布局工作流与物理端点关联

- ThemeProject新增Version2命名布局（最多62个），深拷贝/校验、选择/复制/重命名/删除、撤销和正常保存；V1只读不重写。LegacyThemeAdapter向输出文档提供全部布局。EditableLayoutAdapter将实际分辨率适配结果变为可编辑图层，字体/间距等比转换，不栅格化时钟；预览页“保存为布局并微调”返回编辑器，仍需正常保存落盘。
- 每个命名布局的视图状态独立；全部布局接入素材导出/打包/导入/缺失检查与修复。ThemeScene随编辑布局更新实际像素。
- 设备偏好增加PhysicalGroup；同一物理屏的多个端点人工填同组并保存，新OutputSessionManager同时获取端点和组租约，逐卡报告冲突。运行中禁止改组但允许原有名称保存；不自动猜关联。旧单设备入口及外部软件不自动使用此组，不宣称所有物理冲突解决。
- 最终构建0警告错误；完整Windows707项、Core95项、配置19项通过。收尾选择器/创建校验/撤销视图恢复等后专项40项再次通过，不能把40加到707。真实WPF1100x740/1320x900编辑器及预览截图已检查，离线替身明确标注。
- 只读UsbDemo --list当前发现1台D92（ReportLength1025），未发送真实CRT/HID输出命令。因此第4项真实双D92验收尚未完成，离线并行/互斥结果不能冒充硬件长测。未发布/重启用户应用/提交推送；未覆盖用户配置。
- 详见MULTI_LAYOUT_WORKFLOW_ACCEPTANCE.md和artifacts/layout-upgrade-*、creator-layout-*.png、multi-device-inventory-20261022.json。下次两台实机到位按报告核对双序列号、不同/相同主题、单台拔插/独立停止与持续运行证据；Mac接入仍需单独验收。

# 2026-10-22 创作中心八项体验整理

- 左侧素材/图层拆标签并独占高度；布局名/复制/删除收进布局管理。右侧按单选/多选/无选择显示真实组件、共同属性或画布尺寸/背景；画布尺寸不拉伸图层且不静默换方向。图片图层接共享异步缩略图缓存，其他类型图标，悬停/键盘焦点显示锁定隐藏操作。
- 折叠面板后适应画布，横竖切换显示真实默认布局尺寸；视图捕获补LayoutId。移动/缩放增加设计坐标、比例/Shift提示、实际对齐与最近可见邻居间距；纯ThemeMeasurements不改输出Scene。极小画布不绘制HUD，避免负尺寸。
- 编辑器只保留“预览与应用”，管理设备/服务就绪且无视频时直接打开目标预览；无设备/视频保留原预览与兼容输出。已知设备断开默认离线预览，不启用应用。未增加USB视频能力。
- 生产App给Creator装配保存/放弃/取消窗口；离开/打开/新建/关闭等走同一决策。放弃等待草稿并删除/刷新恢复草稿，不写正式源；取消在输出清理前返回且时钟继续。无UI回调的测试/工具兼容默认自动保存。底部正式状态与草稿状态分开显示。
- 首页真实修改时间排序沿用，卡片显示布局数量/完整日期/继续编辑/菜单，封面实际PNG像素验证通过；没有新建资产库、填假资源或改用户配置。
- 完整Windows728项通过；Core95项、配置19项通过。收尾HUD边界/离线默认后专项20项和设备布局42项再通过，最终构建0警告错误；不要把分次结果相加。真实WPF1320x900/1100x740编辑器、首页、保存提示截图已检查，fixtures为临时真实可编辑主题及离线设备。
- 详见CREATOR_USABILITY_ACCEPTANCE.md及artifacts/creator-usability-*。未发送HID、发布/重启用户应用、提交/推送。真实鼠标捕获/拖动手感、多DPI和实体D92仍需人工验收，不用离线计算替代实测；既有多设备/旧入口物理互斥边界不变。

# 2026-10-23 新版启动与分段运行验收

- 发布并启动0.1.0-preview.20261023.1，自包含包ZIP SHA256=3D5284375161C4BFC3D86F7BAFC589F0C16F56E70092385CEC43D129D8B708FE。启动前无旧DeskDisplay进程；实际控件树显示新版/705份本地资源计数/D92已连接。未覆盖已有配置。
- computer-use截图FrameArrived超时、鼠标坐标geometry不可用；键盘和控件树部分可用。不能称鼠标拖动手感/原生DPI验收完成，没有用其他UI自动化通道绕过。增加隔离WPF横竖100/125/150%栅格截图及竖屏几何7项，不改变系统DPI。
- 构建0警告错误；创作/管理/流程专项302项、设备多布局42项、体验/倍率27项通过，分次计数不冒充一次完整回归。新截图creator-acceptance-*已查看；本轮应用源码未改，仅增测试和资料，发布包应用代码仍为此前完成版本。
- 实体D92现有1920x462/90参数：首轮60.96秒61帧1连接0失败，用户确认方向/文字/秒数正常；暂停在80秒后，另跑120.68秒120帧1连接5次主题显示事件，pause/resume/manual-next均true、0失败0等待，均正常Stopped、用户settings SHA不变。原result保留，视觉确认独立JSON记录；本轮未拔插/竖屏实测/双设备/小时级长测。
- 结束已释放诊断输出，重新打开新版进程9696，Responding=True，控件树D92已连接；不自动重启诊断输出。详见CREATOR_RUNTIME_ACCEPTANCE_20261023.md、artifacts/acceptance-20261023-*及hardware-{landscape,extended}-20261023/。

# 2026-10-23 本地可编辑模板库V1

- 保留clock/text/photo横竖6项，新增wallpaper/work/camera横竖6项，共6类12项。元数据明确背景选择与插件依赖，Core工厂每次创建新项目/图层，横竖独立布局；相机原生比例读取现有RetroCameraClockScene，不拉伸/旋转面板，竖屏独立组合数字时钟/日期。
- 从模板创作接真实SceneView冻结封面卡片、大预览实时时间；封面按实例缓存、插件状态变更重建。壁纸时钟可用原资源库静态素材或本地文件，保留真实AssetId/path/云端来源到横竖层，无新资产库；异步加载与取消、失败禁用创建、可明确清除为纯色。
- 相机模板使用现有CameraScene，仅插件启用后可创建；停用即移除模板预览、显示依赖提示并禁用创建。UsesScene补模板预览，以便插件在模板页真正持续走秒。Use从准备成功的可编辑源克隆独立主题；原自定义模板服务保持。
- 完整Windows774项、Core95项、配置19项通过。收尾封面缓存与依赖占位后模板专项38项再次通过，最终构建0警告错误。既有设备页尺寸检查受到远程桌面工作区压缩（1028x749），已改明确1100x740/1320x900逻辑控件表面，专项27项通过；不宣称原生窗口/DPI验收。
- 真实WPF封面/壁纸/相机启停/竖屏截图已核对并存artifacts/template-library-*.png。用户资源未改，测试用临时PNG/项目/设置；未发送HID、改协议/驱动/固件、发布/重启用户软件、提交推送。实体相机模板效果、Mac/高帧率未验收。
- 详见LOCAL_TEMPLATE_LIBRARY_ACCEPTANCE.md和artifacts/template-library-*日志。当前运行进程9696仍为先前20261023.1，新模板仅在最新构建中，后续需正常关闭保存旧窗口再启动新版。

# 2026-10-23 创作中心参考布局改造

- 编辑器收起全局侧栏（返回恢复），左280面板分56工具栏和素材/图层内容；原列表和编辑逻辑复用。顶部分类移至画布属性、移除常驻操作说明、缩放区靠右。选中浮动复制/粘贴/置顶/删除条跟随设计坐标并限制视口边界；通知合并，布局完成重新定位，使用Actual尺寸避免DesiredSize包含Margin造成定位反馈。
- 完整Windows778项通过；收尾后体验专项33项通过，Core95/配置19通过，最终build0警告错误；真实WPF横竖图已复核。远程窗口尺寸/DPI和真实鼠标捕获边界不变。
- 见CREATOR_REFERENCE_LAYOUT_ACCEPTANCE.md及artifacts/editor-reference-*。未改业务/协议/用户配置、未发HID、未重启用户软件、未提交推送。


# 2026-10-23 创作中心属性区收敛

- 中央顶栏接真实布局选择/布局菜单/设备预览，底栏接设计尺寸与视图菜单（网格间距/吸附/参考线）；浮动条接顺序菜单/单选锁定。右侧专属内容优先，透明度数值/色块色值一行，精确位置/名称样式/文字高级属性折叠；多选对齐分布折叠。保留所有原命令及编辑绑定，未改Core/HID/渲染。
- 默认构建遇运行exe锁，改独立BaseOutputPath=artifacts/properties-build/最终0警告错误。专项38项、完整Windows785项通过（分次不相加），横竖截图已复核；更新4个既有UI测试先展开折叠组，原断言保留。
- 尝试正常关闭旧Debug窗口出现保存修改提示，但远程控件树未返回、截图超时；已Esc取消关闭并确认提示消失，保留用户未保存编辑。没有强杀/盲点保存，新版尚未重启显示。完成后应由用户保存关闭旧窗口，再打开artifacts/properties-build/Debug/net8.0-windows10.0.19041.0/DeskDisplay.App.exe。
- 详见CREATOR_PROPERTIES_LAYOUT_ACCEPTANCE.md及artifacts/creator-properties-*，未发HID、未提交/推送。


# 2026-10-23 创作中心六项交互

- ThemeCanvas新增真实TextBox双击普通文字编辑，Enter/失焦提交、ShiftEnter换行、Esc取消、单步撤销，时钟不转文字；CommitPendingEdits事件在RunAsync/Save/Close前提交输入，订阅随View清理。输入深色对比，不修改源颜色。
- 浮动条放整个帧外，空间不足隐藏，捕获鼠标/平移/输入时隐藏，原右键菜单仍可操作。图片拖入接真实资源库/设计坐标/Uniform/AssetId，PNGJPEG而非视频；快捷替换背景复用已有层/锁定，裁剪复用既有原图参数流程。图层补F2名称定位、排序松开提示，保留原组/排序逻辑。
- ThemeEditorViewport.Workbench可选侧车记录面板/展开/标签/参考线/组折叠，按项目/方向/LayoutId隔离；恢复期间禁止反向保存中间UI。原主题格式不改。
- 完整Windows802项通过；收尾输入颜色后专项18项通过；Core95/配置19通过，最终构建0警告错误，真实WPF截图已查看。新测试CreatorDirectEditingTests含真实PNG/资源去重/原文件字节/输入/恢复/浮动避让；原生鼠标捕获与Mac仍未验收。
- 独立构建artifacts/workflow-build/Debug/net8.0-windows10.0.19041.0，已启动新版PID15648 RespondingTrue，首页真实控件树D92已连接；没有旧窗口。UI点击仍报coordinate input geometry is unavailable，因此留在首页，不能称已原生双击拖动验收。未发送诊断HID、未修改用户配置/提交推送。见CREATOR_DIRECT_EDITING_ACCEPTANCE.md及artifacts/creator-six-*。


# 2026-10-23 精确编辑六项

- 可选ThemeLayer.VerticalAlignment（默认Center）/AutoGrowTextHeight（默认false），旧JSON兼容校验；真实Scene按普通文字独立行绘制/垂直对齐，字体度量与App溢出/自动增高共用，度量缓存128项不持有UI/场景。自动高度与文字一次撤销，显式适应高度不改宽。
- 原裁剪拖动复用并提炼PanImage可测；重置完整等比可撤销，替换支持保留/重置裁剪；裁剪窗口Reset会撤销RestoreAspect请求。原图未改。多选浮动菜单增加对齐/等距；图层菜单合并重命名/副本/组内下一层/锁定显隐等，原菜单保留，组内单层可移动且组关系不变。
- CreatorPreflight结构化LayerId/Blocking问题；中央应用前检查窗口能定位，缺素材/禁用插件阻止继续，越界/文字溢出警告。旧目标能力复用原CheckThemeApplication，多设备仍由设备预览逐台核对；不放宽USB视频能力。不自动阻断普通预览，入口明确应用前检查。
- 完整Windows828项通过；收尾输入滚动/命令通知后precision专项25项、direct专项19项再次通过，Core95/配置19通过，最终build0警告错误，diff检查通过，分次计数不相加。见CREATOR_PRECISION_ACCEPTANCE.md与artifacts/creator-precision-*。
- 使用computer-use正常关闭旧窗口，真实保存提示Tab到保存按钮后回车，旧窗口退出；没有放弃或强杀。新版precision-build启动PID6668 RespondingTrue，控件树D92已连接。点击仍coordinate input geometry is unavailable，部分键盘导航可用，当前在资源库；原生鼠标/中文IME/跨DPI未验收。没有诊断HID/改协议/驱动/固件/提交推送。


# 2026-10-24 核心交付：契约、真实发布链路、固定安装版

- 新增 D92_CONTENT_CONTRACT_V1.md、StudioResourceContract 及13项契约检查；下载前重新检查兼容性，同源同资源URL校验，未知策略拒绝。未扩大静态PNG/JPEG服务能力，云端主题/外部插件/付费流程仍未实现。
- tools/Verify-StudioPipeline.mjs 建立独立PG/Nest/私有存储，真实登录上传发布，Windows生产下载/损坏缓存恢复/入库/可编辑主题保存/实时时钟渲染/云端原图不再分发共7项通过；服务已关闭，无覆盖后台.env或用户数据。
- Windows完整842、适配95、配置19、安装事务7项分别通过，build0警告错误。发布自包含0.1.0-preview.20261024.1，已安装LOCALAPPDATA/D92Studio/app并打开，桌面固定快捷方式与窗口版本已核对，D92已连接、Responding=True。安装暂存校验后替换，旧app备份保留，未代码签名。
- 本次云端主题使用现有UsbDemo --theme发送真实D92：40秒40帧1连接0错误、正常Stopped，settings SHA不变；本轮肉眼确认待用户回复，未拔插/双实体/长测。截图捕获两次超时，不冒充视觉验收。
- 详见 D92_CORE_DELIVERY_ACCEPTANCE_20261024.md。未提交/推送，原有变更保留。

- 本轮实体视觉补充：用户随后确认 Web 发布链路生成主题的方向、文字和秒数均正常；已同步验收文档和 pipeline-hardware-result.json。


# 2026-10-24 已保存主题快速使用

- 保存主题仍用原ThemeProjectStore；资源库全部/主题新增两列主题区与12项分页，支持名称搜索、收藏和使用历史。不复制项目进壁纸资产库，未保存编辑状态保留。
- 创作中心卡片使用主题、编辑器资源库主题、首页最近使用已接入。SavedThemeUse读磁盘独立副本，复用设备预览与应用，增加本软件会话的单台停止入口，不自动抢占。usage.json仅保存收藏和使用时间，成功接受输出才记历史，坏元数据保留。
- 完整Windows862项通过，收尾专项19、适配95、配置19分别通过，build0警告错误。缩略图文件占用和壁纸异步缩略图通知回归已修复，离屏真实WPF截图复核。
- 已安装并打开0.1.0-preview.20261024.3，固定桌面入口未变，实际版本和D92已连接/Responding已核对；旧app备份保留。远程鼠标几何不可用，未完成真实新入口点击/实体输出验收，不扩大上一轮实机结论。
- 详见THEME_QUICK_USE_ACCEPTANCE_20261024.md；未提交/推送。

# 2026-10-24 主窗口边缘缩放修复

- 原生WM_NCHITTEST复现左边缘HTCLIENT=1（导航覆盖缩放带），标题空白原本HTCAPTION=2；根Border保留8DIP缩放带，WindowChrome边框8/标题56、关闭系统默认Aero按钮命中，继续原系统命令。最小尺寸1100x740降为960x640，初始尺寸不变。
- 新增13项真实WPF/HWND窗口检查（四边四角、标题、1000x680、按钮命中、最大化/还原），完整Windows875通过，build0警告错误。未自动修改系统DPI/发送新HID命令，不冒充人工拖拽验收。
- 已安装打开0.1.0-preview.20261024.4，UIA确认版本及D92已连接，进程16432响应正常，安装前后settings SHA相同，备份保留。首次安装阶段提前启动旧进程引发文件占用，安装器保留原app；正常退出并等待重试成功后再启动。
- 详见WINDOW_CHROME_FIX_20261024.md和artifacts/chrome-*。未提交/推送。

# 2026-10-24 按设备生成背景铺满布局

- 设备预览增加生成铺满布局，仅在编辑回调可用时启用。复用EditableLayoutAdapter/新增独立NamedLayout，背景覆盖目标实际尺寸并等比裁切，时钟文字按原适配等比保留，不覆盖原布局/自动保存或输出。仅选择底层可见未旋转静态全画布背景，无合格背景明确拒绝。
- 专项15项、完整Windows890项、Core95项分别通过，build0警告错误。实际SceneJpegRenderer左右像素对比验证移除留边，WPF按钮回调验证通过；未做此次新布局的D92实体输出验收。
- 已安装打开0.1.0-preview.20261024.5，版本/已连接/Responding核对，settings SHA前后相同，旧app备份保留，用户原项目未覆盖。入口：创作中心编辑→预览与应用→选择设备→生成铺满布局→检查保存→再应用。资源库只读预览需先编辑才能生成。
- 详见BACKGROUND_LAYOUT_ADAPTATION_20261024.md与artifacts/background-*。未提交/推送。

# 2026-10-24 创作中心新增素材/图层置顶

- 仅按用户最新两项指令改排序，画布默认尺寸不改。素材按AddedAt倒序、相同时间后加入优先，导入符合当前筛选的素材后列表滚顶；普通新增图片/壁纸/组件统一置顶并选中，跨布局复制也置顶。专用设置背景/模板背景保留背景语义，原项目不重排。
- 专项14项通过（真实导入/批量/时间并列/筛选/通知/渲染顶层/撤销重做/保存/尺寸不变），完整Windows904通过，build0警告错误。旧测试中的普通新增壁纸沉底断言已更新为新要求置顶，仍检查真实素材加载。
- 已安装打开0.1.0-preview.20261024.6，UIA核对版本及D92已连接，进程15812响应正常，安装settings SHA不变、旧app备份保留。此前旧窗口控件树为空且截图超时，用户确认保存关闭后才安装，无强杀或盲点丢弃。
- 详见CREATOR_ORDERING_ACCEPTANCE_20261024.md和artifacts/ordering-*。未提交/推送，未新增HID实机验收。

# 2026-10-24 创作中心体验重组首轮

- 复用原命令重组顶部项目菜单/保存/完成并使用、左侧添加内容与图层、固定右侧元素与画布；选择变化自动切属性，手动改画布不跳页，布局管理原组名移入画布页以保留工作区展开状态。首页收紧为新建/模板与更多菜单，空白画布增加真实添加入口；设备预览直接显示当前确认/停止，批量设备区折叠。
- 原另存为/导出/打包/历史/素材检查/项目工具命令保留，底层Core/Display/Library/HID及主题尺寸不改。专项48、完整Windows914通过，build0警告错误；真实WPF横竖/倍率与设备预览截图已核对，不冒充新实机验收。
- 已在用户确认保存关闭后安装并打开0.1.0-preview.20261024.7；界面版本与安装标记一致、D92已连接、进程12500响应正常。安装settings SHA不变、旧app备份保留，记录artifacts/ux-installed.json；未强杀或丢弃编辑。
- 详见CREATOR_UX_REORGANIZATION_ACCEPTANCE_20261024.md与artifacts/ux-*。素材类目合并/正式三步使用容器/全面问题修复面板等长期项未宣称完成，未提交推送。


# 2026-10-25 创作中心素材入口与三步使用

- 左侧图片合并壁纸/我的素材；保留共享资源筛选、导入和新图层置顶；旧工作区两名称映射。普通添加Image，明确背景选择独立复用原路径，不修改旧主题图层。
- 设备预览增加看效果/选设备/检查并应用步骤，同一预览实例保留目标与适配，现有命令/确认/冲突和释放不变；无设备/视频原回退路径保留。
- 构建0警告错误、完整Windows921通过，设备专项重跑通过、截图核对。已安装打开20261025.1，安装设置SHA不变、旧app备份保留。无新增HID实机验证。详见CREATOR_MATERIAL_FLOW_ACCEPTANCE_20261025.md。未提交推送。

# 2026-10-25 属性区常用参数与高级信息分组

- 仅CreatorView.xaml调整：外观提前、图片先替换/填充/裁剪，精确裁剪/亮度与素材路径折叠；保留原组名与命令绑定、锁定/多选和保存逻辑。
- build0警告错误、专项51通过，真实WPF截图已核对。包20261025.2准备完成，尚未安装；旧版进程9224运行，已询问用户保存关闭。详见CREATOR_INSPECTOR_UX_20261025.md。未提交推送。

- 20261025.2安装跟进：用户确认保存关闭、旧进程正常退出后已安装并打开，新进程2852响应正常；settings SHA相同、旧app备份保留。安装记录artifacts/inspector-ux-installed.json。

# 2026-10-25 数字时钟紧凑边框

- 新建DigitalClock按渲染器固定数字advance/字体/格式/间距计算紧凑边框，居中放入当前逻辑画布。旧主题与模板不自动改写。右侧时钟设置增加适应内容，保留原框中心及旋转、支持撤销，走秒不自动改尺寸。原四角默认等比/Shift自由缩放不变。
- 构建0警告错误，完整Windows925通过；新测4项覆盖紧凑边框/旋转中心/走秒/撤销。包20261025.3已完成，当前应用仍运行，已请求保存关闭，尚未安装；详见CREATOR_CLOCK_FIT_20261025.md与artifacts/clock-fit-*。未新增HID实机验收，未提交推送。

# 2026-10-25 十款内置数字时钟设计

- DigitalClockTemplates.cs增加10种数字时钟设计，各独立横竖布局，原ThemeTemplates注册共新增20卡片；从模板创作可见。真实DigitalClock/Date/Weekday/形状，无图片与插件依赖，原模板及用户自定义内容保留。
- build0警告错误，模板专项159、完整Windows1046通过；真实渲染总览artifacts/digital-clock-templates.png已查看，竖屏字体行高余量已修复。
- 包20261025.4已生成（包含未安装的.3紧凑时钟修复），旧版2852仍运行，待此前保存关闭请求确认后安装最新版；不要先装.3。详见DIGITAL_CLOCK_TEMPLATES_20261025.md及artifacts/digital-templates-*。无新实体屏验收，未提交推送。

- 20261025.4安装跟进：用户确认保存关闭，旧进程正常退出后已安装并打开最新版；settings SHA相同、旧app备份保留，界面版本核对成功。digital-templates-installed.json记录。包含.3紧凑边框修复与10款时钟设计，.3无需另装。

# 2026-10-25 编辑页十款数字时钟组件与个人样式

- 用户澄清要求是编辑页时钟面板的组件而非整套主题。现已接入十款真实渲染卡片：普通字体/字重/字宽/间距、矢量数码管、小秒和竖排；独立新图层置顶、不替换项目，日期/星期入口保留。
- 新增保存为我的时钟样式，组件配置保存在Themes/ClockStyles，不是素材资产库；只保存时钟、不保存背景或其他图层，支持重新加入与移除样式，已有图层不随样式删除。右侧可命名与调整字重、字宽和样式。旧主题缺少新增字段时保持默认旧效果；macOS尚未实现新绘制效果。
- build0警告错误、专项69、完整Windows1115通过，实际WPF截图clock-component-editor.png已看；没有新样式实体屏目视验收。
- 已安装打开20261025.5，安装时客户端已退出；settings SHA相同，旧app备份保留，进程5388响应正常。详见CLOCK_COMPONENT_PRESETS_20261025.md与artifacts/clock-components-*。未提交推送。

# 2026-10-25 完成并使用简化为单窗口

- 三步向导改为核对目标/预览→保存并应用，另有仅保存；画面适配/多设备默认折叠，原高级入口保留，软件状态与各目标结果直接显示。编辑器通过原SaveAsync回调先保存，保存失败不停止旧输出。打开前提交延迟字体/数值绑定。无设备时可使用真实源Scene预览并保存；视频保留旧路径，不扩大D92 USB视频能力。
- 同管理器精确SessionId停止旧输出，新主题预检失败保持旧运行；原USB通过稳定唯一序列号桥接，只取消捕获的原run，匹配USB轮播随切换停止，其他设备/外部所有者不抢占。先停后启不是原子无缝替换，失败不承诺自动回滚旧输出。
- 成功接受编辑器应用后为稳定身份记住已确认配置，新增可选ProfileConfirmed默认false；身份/参数/关联组变化及明确撤销时不复用，歧义设备不复用。未知设置保留，不自动输出。
- 构建0警告错误，完整Windows1137通过，实际WPF截图simple-apply-preview.png已看。初轮原视频暂停时间断言失败、重跑通过，未放宽阈值。无新实体D92输出验收。
- 已安装打开20261025.6，安装时客户端已退出，settings SHA相同、旧app备份保留；详见CREATOR_SIMPLE_APPLY_20261025.md与artifacts/simple-apply-*。未提交推送。

## 2026-10-25 已保存主题直接切换修复
- 修正自动替换本软件输出误绑 HasSaveAction 的问题。SavedThemeUse 无保存回调也可直接切换；不改写源项目，不影响其他设备，保留外部占用保护和预检。
- 构建0警告0错误，完整Windows回归1139项通过，退出码0。无保存回调的托管会话、旧USB转交和外部租约路径已覆盖。
- 已安装并打开0.1.0-preview.20261025.7，实际控件树确认版本、首页和D92已连接；settings哈希未变，旧应用备份保留。未进行新实体屏目视验收。详见SAVED_THEME_SWITCH_FIX_20261025.md及artifacts/saved-switch-*。
## 2026-10-25 编辑器直接设备预览与秒钟裁切修复
- 保存与完成并使用之间新增预览：唯一已确认设备直接发送当前快照，不保存主题；多设备或未确认目标仍需选择确认。保持既有能力与租约检查，持续输出直到切换/停止。
- 显示秒钟自动扩展不足的图层边框，保持中心、字体及原有较大尺寸；一次撤销同时还原开关和几何参数。
- 构建0警告0错误，完整Windows回归1142项通过（退出码0），包20261025.8已生成。当前客户端仍运行，已请求用户保存关闭后安装；尚未安装.8，未发送新的实体HID测试画面。
- 详见CREATOR_QUICK_PREVIEW_SECONDS_20261025.md与artifacts/quick-preview-*。
- 用户已保存并关闭后，已安装并打开20261025.8；settings哈希未变，旧app备份保留，安装记录artifacts/quick-preview-installed.json。

## 2026-10-25 首页同步托管主题输出
- 首页改绑HomeScene；成功应用或设备预览后，按实际目标布局创建独立的实时预览。失败不替换首页，关闭应用窗口不销毁首页场景；旧共享场景主动切换时恢复原流程。多设备显示最后成功应用目标。
- 构建0警告0错误，完整Windows回归1145 项通过（退出码0）。安装包20261025.9已生成，当前客户端仍运行，等待保存关闭后安装。无实体HID新验收。详见HOME_OUTPUT_SYNC_20261025.md及artifacts/home-output-*。

- 已安装并打开20261025.9；旧窗口实际仍有未保存编辑，已通过正常Ctrl+S保存（未保存标记清除）再Alt+F4退出，没有强制终止。settings哈希未变，旧应用备份保留，见artifacts/home-output-installed.json。

## 2026-10-25 首页与资源库使用主题直接应用
- HomeView与LibraryView使用SavedThemeUse.UseDirectAsync，读取保存源快照，直接应用已连接且确认的D92，不弹预览，不保存编辑器。多台使用明确选中USB序列号，否则提示配置目标。保留预检、租约、首页同步；按每卡AcceptedOutputRevision识别本次是否真正接受，防止旧输出误报。
- 构建0警告0错误，完整Windows回归1149 项通过（退出码0）。首轮新增测试因无Application实例断言空引用，修正测试后通过，未放宽应用安全检查。包20261025.11已准备；.10中间包不安装。当前仍运行，等待用户保存关闭安装。见SAVED_THEME_DIRECT_USE_20261025.md和artifacts/direct-use-*。

- 用户保存关闭后已安装并打开20261025.11；settings哈希未变，旧应用备份保留，安装记录artifacts/direct-use-installed.json。启动器初次等待窗口超时，但刷新窗口列表确认同一客户端正常运行，没有重复启动。

## 2026-10-25 主题到壁纸反向USB会话转交
- 日志确认壁纸失败为设备锁冲突。UsbOutputViewModel启动前通过ReleaseManagedOutputAsync等待同序列号稳定唯一的托管会话精确停止，保留外部设备锁与其他输出。明确区分终止历史与运行状态，避免历史快照误阻止壁纸启动。错误显示具体原因。
- 构建0警告0错误，最终完整Windows回归1153项通过退出码0。首轮新增测试发现终止历史边界已修复；第二轮原窗口最大化断言失败，未改窗口代码/放宽条件，最终重跑通过，日志保留。
- 客户端已退出后安装打开20261025.13，settings哈希未变、旧app备份保留。没有本轮实体HID新画面验收。详见WALLPAPER_USB_HANDOFF_20261025.md及artifacts/wallpaper-handoff-*。

## 2026-10-25 输出协调、临时预览、常驻与启动恢复
- 新增OutputCoordinator共享切换/重入所有权与全页状态栏，停止保留可中断驱动准备的原路径。本地浏览与USB实际Scene引用分离，显式应用才切换；首页优先显示活动设备场景。默认目标断开不自动改用别的D92。
- 设备预览保存原来源，重复更新后仍可结束恢复，正式选择新内容会清临时模式，版本变化不覆盖新会话。临时内容不写使用历史/恢复记录。
- 常驻托盘/登录Windows启动/启动恢复默认false。原AppSettings新增可选字段合并保存。支持已保存主题、静态图片、股票/相机插件；只恢复已连接且确认的稳定相同设备，缺失/占用不抢占；轮播和基础场景恢复仍未实现。注册项只在选项改变时应用，测试替身未改系统开机设置。
- 新增有界切换/UI延迟/真实JPEG指标；仍是UI线程渲染，未声称彻底解决卡顿。
-
最终验证：构建0警告0错误，Windows完整回归1177项、适配95项、配置19项、打包7项通过（退出码均0）。新增明确断开的默认目标不得改用其他连接设备检查。最终包20261025.15；.14为中间包不安装。截图已复制至artifacts/output-ux-home.png和output-ux-settings-125.png。最终30层编码平均20.22ms、p95 22.40ms、最大23.63ms；同轮UI最大调度延迟16ms（此前冷窗口构建样本735ms，不能据此宣称卡顿消失）。当前旧客户端仍运行，待保存关闭安装。

- 首轮回归发现停止被协调锁阻止已修复；后续回归旧测试依赖本地预览自动输出，更新为显式应用并保留尺寸/旋转/JPEG验证。详见OUTPUT_EXPERIENCE_20261025.md。未发送新实体HID验收指令，未提交推送。

## 2026-10-26 安装输出体验升级版
- 用户保存关闭后，安装并打开0.1.0-preview.20261025.15。安装标记与控件树版本一致，首页D92已连接，进程响应正常，目标选择器可见。settings SHA安装前后一致，旧app备份保留，记录artifacts/output-ux-installed.json。
- 托盘/登录启动/启动恢复默认关闭，没有代用户开启注册项或恢复输出。启动日志在线资源连接失败，未将其误报为全部在线功能正常，未擅自启动其他后台服务；主窗口正常运行。未新增实体HID画面验收。

## 2026-10-26 — USB 轮播恢复 / 静态 JPEG 缓存 / 输出重试

- 已安装并打开 `0.1.0-preview.20261026.1`。安装前后 settings.json SHA256 一致，旧应用备份保留。
- 修复 USB 已输出时主题轮播没有显式应用下一场景；主题、壁纸轮播可在主动开启启动恢复后恢复当前项及暂停状态。正常退出先记录轮播模式再停止计时器。
- 静态冻结位图使用每渲染器单项、8 MiB 上限 JPEG 缓存；动态主题、时钟、插件不缓存。失败输出新增底部显式重试，不自动重试、不绕过设备确认及占用检查。
- 最终构建 0 警告、0 错误；Windows 1194 PASS、适配 95、配置 19、打包 7 均通过。资料及日志见 PLAYLIST_RECOVERY_CACHE_20261026.md 和 artifacts/playlist-cache-*。
- 真实安装窗口控件树确认新版版本、首页及 D92 已连接，进程 Responding=True。截图捕获超时，本轮未宣称截图验收。
- 本轮未发送真实 HID 测试命令；新增轮播/重试测试使用模拟传输及真实 WPF/JPEG 渲染。仅一个传统 USB 轮播目标，恢复重新计算完整间隔，不还原剩余秒数。启动恢复保持原设置 false。
- 当前云端资源连接仍不可用，首页推荐为空/明确标注既有演示案例；不宣称云端验收通过。
## 2026-10-26 — 首页及资源库直接使用入口

- 用户反馈“使用主题/使用壁纸”入口不见。源码核对：主题仍有常驻直接使用按钮；首页静态壁纸改成“查看壁纸”，资源库壁纸应用仅在悬停时显示，造成入口不一致。
- 首页静态壁纸按钮改回“使用壁纸”，直接调用现有 Workspace/USB 输出链路；缩略图仍打开详情，视频仍进入查看，不虚构 USB 视频能力。
- 资源库壁纸卡片下方常驻“使用壁纸”，移除重复悬停应用按钮；收藏继续使用既有入口。主题“使用主题”保持常驻且直接输出，无预览弹窗。
- 新增回归验证直接使用停止轮播并替换输出、不跳详情/预览，以及未悬停时真实 WPF 卡片按钮可见可用。
- 构建和测试资料：artifacts/direct-use-build.log、direct-use-special.log、direct-use-regression.log；发布版本 0.1.0-preview.20261026.2。测试使用模拟输出，不等于本版实机验收。
- 最终结果：1198 项 Windows 回归全部通过，构建 0 警告/0 错误；.20261026.2 安装完成，settings.json 哈希保持一致。

## 2026-10-27 — 六项日常使用闭环

- 已安装并打开 0.1.0-preview.20261027.2（.1 为中间包不交付），settings.json 安装前后 SHA256 一致，旧 app 备份保留。进程 Responding=True；实际首页控件树确认版本、D92 已连接、本地预览/定位资源/停止入口。原生截图 FrameArrived 超时，坐标几何不可用；未强行点击或伪称视觉验收通过。
- 首页新增实际输出内容状态、定位与独立停止。统一轮播页含壁纸/主题两个标签，创作中心主题轮播入口也导航到该页；复用原有两套播放引擎，非混编单一列表。
- 设置新增已保存数据备份/导入、版本及更新内容、本地 ZIP 校验和程序目录入口。备份素材去重流式处理、4 GiB 上限，元数据有界；清单哈希/路径/主题版本校验。导入确认后下次启动合并，已有设置及同 ID 源项目不覆盖，资源库新增 ID 合并，失败回滚新增文件和原元数据。凭据、设备锁、插件程序和系统字体不纳入。
- 实际数据已备份：1156.60 MiB，686 文件、1 原有缺失素材；校验通过，并在隔离 artifacts 目录成功恢复 686 文件，无冲突，未覆盖用户目录。产物路径分别记录 artifacts/workflow-real-backup.json 和 workflow-real-restore.json，artifacts 已 Git 忽略。初版 512 MiB 内存限制中止后已改流式，不将该失败当最终结果。
- 图库搜索合并 250 ms，诊断页增加最近/最大筛选耗时；5000 条模拟元数据检查三屏有界加载及翻页。专项最大约 11 ms，不含解码/网络/HID，不宣称卡顿彻底消失。
- 版本更新验证当前版本、产品、架构、文件清单、SHA256，拒绝降级和多余程序，打开已校验的本地暂存目录后由用户保存退出运行 Install.ps1。没有线上更新 API 或发布者签名，未自动执行选择包中的脚本。
- 最终构建 0 警告/0 错误，完整 Windows 1221 PASS，适配95、配置19、打包7均通过。基线1198 PASS。日志 artifacts/workflow-streaming-regression.log / workflow-build.log / workflow-adaptation.log / workflow-smoke.log / workflow-packaging.log；真实发布包也通过同一更新校验服务。
- 真实 D92 120.50 秒测试完成，静态图与主题轮播5次变化，暂停、继续、手动下一项执行，0 传输失败，用户设置未变。没有检测到拔插；用户目视及重连确认仍待回复，不能宣称通过。实机证据 artifacts/workflow-hardware-20261027/result.json。未更改 HID 协议；本轮单设备不能证明多台 HID 实机并行能力。
- 详见 DAILY_WORKFLOW_ACCEPTANCE_20261027.md。没有提交或推送 Git。

## 2026-10-27 用户补充实机确认
用户确认：方向、文字和秒数正常，USB 拔插后恢复。作为独立人工观察记录，证据 artifacts/workflow-hardware-20261027/user-confirmation.json。本轮自动测试日志未捕获断开/重连事件，保留其原始 reconnectEvidence=false，不回填或伪造日志。目视与人工拔插验收已得到用户确认；日志级重连证据仍未补齐。

## 2026-10-27 — 混合轮播、回退、草稿、资源修复、固定内容

- 新包 0.1.0-preview.20261027.3 已生成。最终构建0警告/0错误；完整 Windows 1243 PASS、适配95、配置19、打包7通过。旧草稿命令测试因新预览需要确认而等待，已仅停止独立 DisplayTests 进程（未停止用户客户端），旧日志保留为 content-regression-initial.log；确认函数可注入测试，产品默认真实预览。最终全套回归退出0。
- 混合列表引用既有壁纸/主题ID，逐项停留、拖动/按钮排序、随机、暂停/继续、下一项、跳过失效；单个明确确认目标输出，手动应用/停止终止对应调度，启动恢复沿用可选设置。保留旧两套队列和USB视频限制。home-pins.json/mixed-playlist.json纳入备份。
- 托管替换准备失败或新替换首帧5秒内失败时，按原持有来源尝试恢复；保留配置/身份/租约检查和原错误，不把失败新主题记为成功。明确停止通过版本阻止回退重启；外部占用不抢占。后期硬件故障仍按原重连链路处理，不承诺任意故障均回滚。
- 草稿按钮先真实场景预览，确认后独立副本；取消不变，脏编辑沿用保存/放弃/取消。顶部区分正式保存和自动草稿，路径修复保持原正式编辑时间避免隐藏草稿。
- 资源检查支持缺失/重复扫描、单项定位、目录唯一同名匹配，修复保留ID/分类/收藏并更新已保存主题引用，当前脏编辑器不覆盖。实际只读扫描712项，94个重复条目、1缺失、0读取失败。旧截图 a18344d8-0588-4b39-8e1a-b291ecf37fab.png 原件未找到，保留条目待用户选择替换，不使用无关图片。
- 首页及资源库☆可固定/取消，最多12项，常用区独立于最近排序，复用直接使用能力；收藏仍独立保留。
- 包完整性由本地更新服务复核通过。无本轮新HID实机验收。详见 CONTENT_EXPERIENCE_ACCEPTANCE_20261027.md 与 artifacts/content-*。
- 当前旧客户端仍运行，已发起保存并关闭请求，尚未安装 .3。不要把新包完成误报为客户端已更新。未提交推送。
## 2026-10-27 UI 重构公共按钮基础
- 保留旧样式键，修复主按钮悬停白字浅底，补模板对齐、键盘焦点、AccessKey、禁用提示与显式 IconButton/DangerButton；正文色 #202C43。
- 基线与最终 Windows 回归均1243 PASS退出0；构建0警告0错误。见 UI_DESIGN_SYSTEM_ACCEPTANCE_20261027.md、artifacts/ui-system-*。
- 仅源码样式基础，未安装新版、未修改业务/配置/HID；无新截图及多DPI视觉验收。后续按 UI_REFACTOR_PLAN_20261027.md 分批实施。

## 2026-10-27 UI 重构第2–6批
- 首页设备栏换行、卡片标题操作分区；资源分类共用可换行筛选；创作属性面板260并支持左右调宽；预览长标题；设置四组；轮播表格、设备与旧页面控件及资源修复窗口统一。详见 UI_REFACTOR_BATCHES_2_6_20261027.md。未修改HID及业务数据格式。
- Windows1258、适配95、配置19、打包7通过；专项15包括960×640/1320×900布局与真实键盘焦点。包20261027.4已准备，488文件SHA校验通过。
- computer-use隔离窗口控件树可读，原生截图两次超时；没有新截图、多DPI人工或HID验收。
- 当前用户客户端进程18048仍运行、安装标记20261027.2。已异步请求保存关闭后安装.4；没有强退/替换，之前未安装.3功能已包含在.4源码。未提交或推送。

- 用户确认保存并关闭后，已安装并打开20261027.4，settings SHA256一致，旧app备份backup-e3075b4889064083aaf880d3c986fa63保留。实际控件树确认新版版本、首页D92已连接和直接使用入口；进程21268 Responding=True。见artifacts/ui-installed.json。截图/多DPI人工验收仍待补，无新HID测试。

## 2026-10-28 D92视频阶段1–2离线原型
- 新增Core IVideoFrameSource/ILiveVideoFrameSource及VideoFrameClock，Windows按时间采样和持续MediaPlayer帧服务器两后端，私有STA等比合成/旋转/JPEG，单请求无托管待发队列；取消/释放已测。
- D92HidRenderTarget仅增加完整帧耗时事件，保留协议/租约/串行写入/重连和100ms下限。独立VideoPrototype工具支持显式真实测试、离线视频、独立预编码JPEG基线，方案加入sln。未开放App原视频能力保护。
- Windows1277 PASS退出0，专项19；Release0警告0错误。旧采样高请求仅约2.5FPS；连续后端全分辨率运动夹具5/10请求完成4.9/9.3FPS、均约120KB JPEG，纯离线不含HID，每档10秒。原图案每250ms变化，不能把完成FPS当独立刷新率。
- 用户连接确认后HID/PnP都仍无匹配，随后明确暂时跳过实机继续离线。本轮从未发送真实HID命令、未装新App/修改用户设置。客户端仍20261027.4。
- 见D92_VIDEO_PROTOTYPE_ACCEPTANCE_20261028.md、tools/DeskDisplay.VideoPrototype/README.md、artifacts/video-*.json与日志；后续实测USB后再做正式客户端/组合主题/轮播接入。没有硬解、长测、睡眠唤醒或多设备视频验收。未提交推送。

## 2026-10-28 D92视频实机短测
- 用户明确确认保存关闭后，识别C511D3782B05/report1025，按保存的1920×462/90°进行真实HID测试。基线137帧、视频141帧；各档Failed=false，工具退出0，settings SHA一致。
- 使用新增30FPS内容夹具（90张不同图案、约3秒循环），视频专项19通过；前一轮4FPS图案夹具的证据仍保留。预编码1/2/5/10请求完成1.0/2.0/4.87/9.23FPS；真实视频5/10请求完成4.90/9.20FPS、均约119KB/JPEG。均为主机完整写入计数，不是精密面板刷新率。
- 详见D92_VIDEO_HARDWARE_ACCEPTANCE_20261028.md。证据目录artifacts/video-hid-validation-eee0154ecbc54f6180e9a53ec331bc5f；工具原始PhysicalDisplayConfirmed=false保留，目视确认已询问仍待回复。未做长测/视频拔插/睡眠/双设备/30FPS输出，未安装新App或开放视频入口。

- 2026-10-28 视频重播60秒及目视已通过：用户确认方向/完整画面/持续变化循环正常、无黑屏花屏。主机5/10请求完成4.70/9.19FPS，共417帧，无传输失败，settings SHA一致。证据artifacts/video-hid-repeat-147bfb3065504ffbbe1e76e475255224，独立user-confirmation.json保留；未将主机FPS冒充面板精确刷新率。仅短测，未做长测和视频拔插，安装版仍.20261027.4、视频入口未开放。

## 2026-10-28 正式客户端独立视频
- 已安装并打开0.1.0-preview.20261028.1，真实控件树确认版本及D92已连接；488文件SHA/大小通过，settings安装前后SHA一致，旧程序备份保留。见D92_VIDEO_CLIENT_ACCEPTANCE_20261028.md。
- 我的资源库/最近使用开放使用视频，首页共享实际输出帧、暂停/继续/停止；准备首帧后替换，失败重试，停止取消准备，成功应用停止旧轮播。USB静音循环最高请求5FPS；不开放组合视频、视频轮播、开机视频恢复或多设备视频。
- Windows最终1294 PASS退出0，视频专项16、适配95、配置19、打包7通过。首轮循环取帧断言失败保留日志，修复等待新原生帧事件后严格断言和全回归通过。
- 180秒正式VM实机记录750帧/2连接/ReconnectObserved=true，用户明确确认画面正常、拔插自动恢复。此前120秒未观察且未恢复不算通过。后续新帧等待修复已做原生自动化回归，未重复180秒硬件验收。未宣称精密面板FPS或长测内存稳定。
- 未修改HID协议/用户配置，未提交推送；没有新截图、多DPI、睡眠或长测验收。

## 2026-10-28 请求30FPS实机测试
- 用户退出软件后显式测试30FPS，60.03秒完成692帧=11.53FPS，Failed=false，用户回复画面正常。证据artifacts/video30-hardware-c02740c26ddf4cffab96d65734538118，详见D92_VIDEO_30FPS_ACCEPTANCE_20261028.md。
- 同素材离线约11.6FPS；实机平均取帧15.81ms、合成/旋转/JPEG45.81ms、写入21.50ms。当前处理链明显有成本，不宣称30FPS成功或硬件极限已知。
- 诊断工具新增显式ExperimentalHighFrameRate最高30；默认100ms保护、正式安装版5FPS及HID协议不变。视频专项21 PASS退出0（包括新增两项限速保护）。用户settings SHA一致，未安装新版、未自动重启客户端，未做长测/拔插或提交推送。

## 2026-10-28 视频接近30FPS优化与发布
- 正式客户端已安装/打开20261028.2。相同全分辨率30FPS内容/质量90，离线29.67FPS，工具60秒1726帧=28.77FPS；共享正式客户端链路60.0257秒1735帧=28.90FPS，无失败状态。详见D92_VIDEO_30FPS_OPTIMIZATION_20261028.md与artifacts/video30-client-hardware-8ab5159ec2f54adcb3cc4b7ea1605667.json。目视已询问未回复，不复用优化前确认。
- 同尺寸跳过WPF重复合成，物化正交旋转，复用新事件但循环回跳等新帧；共享VideoFramePipeline最多预备1帧并丢弃150ms过期帧，编码/串行HID重叠。高频会话成对请求释放1ms计时精度；协议、租约、心跳、写入期限保留。
- 默认视频目标30，USB设备设置提供5/10/20/30（停止后可选、本次运行参数、不迁移配置）；默认底层调用仍限10，客户端视频显式允许30。不是锁定30、精密面板FPS或所有素材承诺。视频主题/轮播/开机恢复仍未开放。
- Windows完整1308 PASS退出0；20项客户端专项通过，旋转/循环/单帧过期/冻结/重复释放等包含在全回归中。全回归后资源释放结构整理另通过正式VM构建和实机流程。发布488文件SHA/大小通过，settings安装前后SHA一致，旧app备份backup-e3dfb19ae3c14aec9550aa86758c5090保留。
- 真实控件树确认新版版本、D92已连接及使用入口。设置页点击两次coordinate input geometry is unavailable，刷新恢复后仍失败已停止输入；无新截图或设置页人工视觉确认。未进行30FPS长测/拔插/睡眠/多设备，未提交推送。

## 2026-10-29 优化后两轮视频目视补确认
- 用户分别对优化工具30FPS实测和正式客户端30FPS链路验收回复“正常”。两轮各自保存独立user-confirmation.json，D92_VIDEO_30FPS_OPTIMIZATION_20261028.md已更新。
- 主机实测28.77/28.90FPS与目视证据齐全；不表示锁定30或精密面板刷新测量。本轮仅归档，不重发HID、不修改应用/用户设置。30FPS长测、拔插、睡眠、多设备仍未验证。

## 2026-10-29 30FPS目标30分钟持续输出验收
- 用户确认退出客户端/CRAFT后，正式输出VM+隔离资源库+真实D92 C511D3782B05运行1802.339秒，50,501完整帧，平均28.0197FPS，Failed/BlockedByCraft=0、Error=null、测试退出0，停止释放通过。不是完整可视主窗口30分钟交互测试，不能宣称锁定30或精密面板FPS。
- 增强ClientVideoTests显式实机模式的每10秒FPS/内存/句柄/CPU采样与进度检查点，失败也保留证据，不覆盖既有结果；预检20专项PASS退出0。没有修改安装版App或HID协议。
- 用户回复画面正常，后明确不方便拔插、要求跳过；本轮未验证30FPS重连及睡眠唤醒。30分钟共182采样点，预热后私有146.39–191.73MiB，前后5分钟中位161.39→167.78MiB；托管12.00→12.83MiB，句柄中位1027→1011。无持续单向增长/输出停止，但不据此宣称无泄漏，数小时与可视UI长测仍待补。
- 报告D92_VIDEO_30FPS_SOAK_ACCEPTANCE_20261029.md；结果artifacts/video30-soak-23f146289bff4c2a9a71ea9d5e14f52d.json，进度/确认/跳过记录分别独立保存；汇总artifacts/video30-soak-summary.json。settings前后SHA一致，安装版保持20261028.2，无重新安装或Git提交推送。长测后重新打开原安装版。

## 2026-10-29 视频功能1–6批次（等待关闭后实测/安装）
- 已实现视频/壁纸/主题混合轮播及按时长/原生循环完成切换；暂停只停止切换，播完事件限定同设备同内容，兼容旧MixedEntry格式。原壁纸USB轮播亦允许视频。
- 新ThemeVideoComposition在私有STA复用SceneView合成单视频+可编辑实时组件/插件；保存源项目不变，首页接收实际合成帧。一个布局最多一个可见视频，多视频准备拒绝。复用已验证HID，不修改协议。
- UsbVideoPreferences按serial保存5/10/20/30及opt-in自动降档；完整发送FPS显示于首页/USB页，连续5个1秒窗口<80%逐档降低，不降低质量。启动恢复支持Video/暂停态，默认不开启；缺失/断开/占用不改选目标，可重试。失败首帧尝试回退被释放的受管源，过期回退受保护。
- 完整1345 PASS退出0（video-next-confirmed-regression.log）；专项77、适配95、配置19、打包7通过。修复等待新事件期间媒体回跳漏计，旧不支持视频的UI断言改为缺失/未解码保护。严格像素及循环断言保留，失败日志保留。
- 20261029.1已打包，488文件大小/SHA通过，尚未安装（仍20261028.2）。实机工具首次因DeskDisplay.App运行在发送前退出，无新HID发送。已异步请用户保存关闭后进行约40秒静态/视频时钟主题/静态实测并安装，未强退进程。
- 用户明确跳过拔插，未请求再次操作；不自动睡眠。新视频主题长测、多设备/完整可视UI长期运行、真实30FPS拔插/唤醒仍未验证。见D92_VIDEO_EXPERIENCE_ACCEPTANCE_20261029.md。未提交/推送。

## 2026-10-29 视频1–6安装与新主题实机短测完成
- 用户确认保存退出后，新功能HID静态→视频时钟主题→静态实测成功，使用保存的1920×462/90°配置，Success=true、Error=null、退出0。视频阶段首帧后30.0005秒474帧=15.7998FPS，实际自动降档30→上限20；平均约127KB/JPEG、写入20.74ms。不可将新主题称为30FPS；合成开销仍待优化，也不复用独立视频约28FPS证据。目视已询问尚待回复。
- 证据artifacts/video-feature-hardware-3a44bdec466246f1b50b193b54a50877/result.json及video-feature-hardware-repeat.log；首次进程占用保护日志保留，没有强退或绕过占用检查。本轮仍未做真实拔插/睡眠/视频主题长测。
- 已安装并打开20261029.1，488文件大小/SHA通过，settings实测及安装前后SHA一致。旧app保留backup-249a2ea110104e2a8cfee74b34422d60。真实窗口树确认版本及D92已连接，进程15412 Responding=True；未新增截图或完整设置页人工视觉验收。见artifacts/video-features-installed.json和D92_VIDEO_EXPERIENCE_ACCEPTANCE_20261029.md。
- 未提交/推送Git，未修改HID协议/固件。用户资源库未加入测试素材，实际启动恢复开关未替用户开启。

## 2026-10-29 视频主题目视补确认
- 用户对静态→视频背景+右下实时时钟→静态的方向、视频、秒数与黑屏/花屏检查回复“正常”。本轮目视已通过，独立user-confirmation.json保存在artifacts/video-feature-hardware-3a44bdec466246f1b50b193b54a50877。
- 实测仍为约15.8FPS；未修改原始测量或程序/设置，也未补作拔插、睡眠或长测。

## 2026-10-29 视频组合体验五项优化与性能边界
- 实现缓存透明前景/CPU像素叠加（严格条件快路径，复杂场景保留WPF）、托管USB/系统副屏背景暂停但时钟更新、轮播当前/下一项/倒计时、故障恢复入口。软件预览最多15Hz，HID请求仍独立最高30FPS；未修改协议。
- 离线基准15.13→29.73FPS，不等于实机。原六轮30分钟组合主题长测在第三轮性能下降后正常关闭取消，未通过完整长测；用户确认背景暂停/时钟走秒/继续播放正常，独立user-confirmation.json已存。证据artifacts/video-refine-soak-7b2125edb6034293969d4c2b789f81b7。
- 预览限频后两次60秒实机流程正常退出，后一次有效播放18.26FPS，约127400字节/JPEG、HID写入30.66ms，自动降30→20；不可宣称稳定30FPS。见artifacts/video-preview-budget-native-hardware/result.json。未补作拔插/睡眠/多设备实机，也不将隔离SceneView窗口称为完整主窗口长测。
- 专项33检查通过。此前全回归1355通过；新增两项预览测试后的首次全回归在既有工作区sidecar断言失败（video-preview-budget-regression.log）；串行复跑中，尚未将其宣布通过。
- 20261029.3打包488文件SHA通过，尚未安装；zip SHA256 7B0E10462F8410A99C8703CA9C2E059DD06BB425ADF83B015967D82415B7AE4B。用户设置SHA仍2E256A832DFE8607540ECF3AC7A326617F43746E01F19B159A551EF9A2EFEC1F。
- 详见D92_VIDEO_REFINEMENT_ACCEPTANCE_20261029.md。未提交/推送；.2为中间包，不推荐安装。
- 串行复跑video-preview-budget-regression-repeat.log退出0、1357项PASS；初次失败日志保留，未改测试断言或删失败结果。专项33项通过。
- 已安装20261029.3，488文件通过；原app备份backup-4a79641d4d0e4c99b50712d0fa59bb90，settings安装前后SHA一致。正在打开并核对真实窗口版本。

## 2026-10-29 继续性能诊断与手动画质
- 用户已确认保存退出，已完成60秒实机诊断及质量80对照；未要求拔插。基线日志证实缓存前景持续生效，前台预览后多阶段耗时均上升；5Hz预览实验无稳定效果，已撤回。
- 新增每约5秒的结构化解码/合成编码/HID/JPEG/缓存帧日志；视频JPEG质量按设备手动70/80/90，默认90、旧配置不变，独立视频与组合主题共用，下次播放生效。没有降低用户真实设置。
- 质量80实机17.98FPS、97818字节/帧、HID26.40ms；未解决30FPS。见artifacts/video-quality80-hardware/result.json。基线video-bottleneck-hardware-diagnostics及失败实验video-preview-adaptive-hardware均保留。
- 新设备布局快路径专项通过；画质前全回归1358通过。最新全回归video-quality-final-regression.log进行中；20261029.4发布进行中，尚未安装。当前安装3已退出。
- 最终全回归video-quality-final-regression.log退出0、1360项PASS。20261029.4发布488文件SHA/大小通过，ZIP SHA256 483E205CDD48C928598CEEE1507DC6AC575C1512D13063A34C722BE307D76847。
- 已安装4，旧app备份backup-82bcb3b35c6b49dc89779fc917ef80bf；用户settings SHA安装前后一致2E256A832DFE8607540ECF3AC7A326617F43746E01F19B159A551EF9A2EFEC1F。新代码未替用户选择80画质。
- 30FPS仍未达成，完整长测未重跑通过；请勿把新增手动画质视为性能已解决。未提交/推送。
- 启动后真实窗口文本确认0.1.0-preview.20261029.4与D92已连接，进程正常响应。尝试打开设置入口时电脑控制返回coordinate input geometry is unavailable，未继续盲点；设置页未新增人工视觉验收或截图。

## 2026-10-29 P0分段测量与独立HID曲线
- 用户确认保存关闭后完成四档合法固定JPEG实机测试；未改协议/设备配置/固件。约59.9/80.6/99.5/118.8KB平均写入8.52/11.04/13.82/16.90ms；请求仍封顶30FPS，不能称无节流极限。逐report回归斜率0.1445ms、R²0.311，不支持250us固定端点周期结论。bInterval未读取。
- 离线原生比例视频+真实时钟15秒：444帧29.57FPS，生成p95 52.53ms/p99 59.96ms；进程分配539.41MB/s约18.24MB/完成帧、Gen2=536。均为诊断进程测量，GC暂停/慢帧因果尚未跟踪。
- 详细计时默认关闭；新增VideoStageTiming、VideoMeasurementStatistics及可选native/混合/旋转/JPEG分段计时，原始JPEG/Preview/协议语义未改。Prototype支持CaptureDiagnostics/ComposeClock、生成固定JPEG、分析曲线，内存记录后批量落盘。
- 保留用户未跟踪VideoJpegFrameSource - 副本.cs，只在csproj显式排除重复编译。没有删除/覆盖副本。
- 专项8项通过，全回归1368项退出0；工具最终构建通过。证据artifacts/video-p0-20261029-1793260891567、文档D92_VIDEO_P0_MEASUREMENT_20261029.md。settings前后SHA一致。
- 安装版仍20261029.4；未发布新包、未自动提交推送。下一步P1先控制缓冲区生命周期与复制，保持暂停干净背景/独立预览所有权；不要同时切编码器和并行队列。

## 2026-10-29 P1前两项：读回缓冲所有权与去复制
- 新增可选IBufferedLiveVideoFrameSource/VideoBufferFrame；Windows读回支持调用方数组切片，旧ReadLatestAsync保持独立不可变快照。
- VideoJpegFrameSource每个串行解码器持有一个ArrayPool.Shared租赁数组，读回直接写入，去掉每帧new byte[]与Pixels.ToArray两次大数组分配。暂停/预览持有独立冻结Bitmap，关闭等待在途任务后归还数组；未改HID、编码器、旋转或并行队列。
- 首次before/after后又用同一统计CLI、保留P0 Core/Display DLL执行before→after→after→before对照；每帧18.22–18.24MB降到11.13MB（约39%），平均FPS基本持平。Gen2约537–541→704–706，15秒GC暂停约243–254ms→273–281ms，没有证明GC/卡顿改善，不可称稳定30FPS解决。
- 证据artifacts/video-p1-1793263066408/comparison.json及各result.json。工具新增ProcessGcPauseMs阶段差值；诊断覆盖全进程不作单线程归因。
- 新10项生命周期检查纳入全回归1378项，退出0。工具构建通过，settings SHA不变。见D92_VIDEO_P1_READBACK_ACCEPTANCE_20261029.md。
- 本轮没有发送真实HID、没有新前台预览/长测，也未发布安装包、未提交推送；安装版仍20261029.4。用户源码副本保留。后续P1继续局部混合、旋转及WPF原生内存压力/GC事件剖析，暂不同时换编码器或加工作线程。

## 2026-10-29 P1第二批：局部混合与旋转工作区
- 新VideoOverlayBlender计算实际非透明alpha包围盒，只在前景更新时扫描；混合限定区域，透明前景直通。混合工作数组复用，仍从干净背景重新复制，冻结输出不别名可变数组。
- 新VideoRotationBuffer复用两个数组，90/270度32x32分块、180反向；源编码会话持有工作区并在等待在途帧结束后释放。旧静态RotatePixels保留供测试/调用，输出快照独立。
- 离线分步测量：前批P1约11.13MB/帧→仅ROI7.56→完整约0.463MB（减96%）；相对P0约18.24减97.5%。ROI访问133812/887040像素，混合约1.33→0.83–0.88ms。旋转总耗时约3.8–4.1ms，未证明显著提速。平均FPS约29.7，p99仍有60–73ms波动；不称30FPS稳定。
- 31项像素专项通过，完整回归1409项退出0；工具构建通过。证据artifacts/video-p1-pixels-location.txt，汇总comparison.json。见D92_VIDEO_P1_PIXEL_ACCEPTANCE_20261029.md。
- 用户设备仍连接且软件/CRAFT退出后，完成真实组合主题A/B短测。探索30秒手动激活时机不同，不作结论；再使用相同测试DLL、隔离替换基线Core/Display，并令窗口启动请求激活，各60秒：18.527→18.961FPS，两者都30→20，HID约32ms，未解决前台输出性能。记录controlled-hardware-before/after及hardware-comparison.json；未新增视觉确认或拔插/长测。
- settings SHA前后一致，未发布安装包/未提交推送，安装版仍20261029.4；用户源码副本保留。下一步应看BitmapSource往返、WPF原生位图内存/GC与前台资源同步，以及非原生比例快速路径。不要把本轮托管分配改善写成稳定30FPS通过。

## 2026-10-29 24FPS档位与帧预算修正
- USB选项新增24，归一化/按设备保存恢复均支持，默认30不变；自动降档30→24→20→10→5。新增24归一化、阈值、禁用降档、真实会话间隔与持久化检查，全回归1414项通过。
- 实测发现D92循环CRAFT检查在timer.Start之前，每帧检查成本漏算预算；仅移动计时起点到检查前，每帧检查频率、租约、整帧超时/取消、协议不变。
- 同一原生比例视频时钟主题、1920×462/90°、自动降档关闭：修正前四组均22.72–22.83FPS；修正后可见q90=24.034/无窗口q90=23.949/可见q80=23.942/无窗口q80=23.990。每组60秒，实际间隔p99=54.27–59.47ms，未达此前50ms参考标准，未进入30分钟长测。主机写入FPS不等于面板呈现；没有新视觉/拔插确认。
- 证据artifacts/video24-1793266754011/comparison.json及八组result.json。测试工具新增FPS/AutoReduce环境选项、阶段间隔分布；默认旧测试参数仍30/自动降档开。用户实际设置测试前后不变。
- 已安装20261029.5，488文件验证通过；旧app备份backup-697daaae31274de4964602692941b12e；ZIP SHA554C91758B8CC098F7DADAAE06EDAE58A0BB4767CD764CFA887E4D745536B598。settings SHA仍2E256A832DFE8607540ECF3AC7A326617F43746E01F19B159A551EF9A2EFEC1F。
- 见D92_VIDEO_24FPS_ACCEPTANCE_20261029.md。没有自动提交推送GitHub，用户源码副本保留；本版也包含尚未发布的P0/P1代码。
- 后续值得测量但未实现：ThemeVideoComposition在子场景捕获为空时仍每秒设置_overlayDirty，同时墙钟跨秒也重绘，可能造成额外前景重绘；先测事件与尖峰关联，不据此宣称已定位p99全部根因。
- 启动后真实窗口文本确认20261029.5与D92已连接，进程Responding=True。设置按钮UI点击返回coordinate input geometry is unavailable，未继续盲点或修改用户选项；24选项已由数据绑定/会话/持久化回归覆盖，未新增设置页截图。

## 2026-10-29 前景缓存、非原生比例快速路径与24FPS长测
- 修复空子场景捕获造成的重复前景失效；纯视频＋时钟只捕获一次无外部数据的子场景，股票自身时钟仍按秒更新，图片/股票真实变化仍触发更新，动画不伪装静态。
- 新VideoCanvasNormalizer用于Uniform的等比画布归一化；640×360真实H264可走1920×462合成快速路径，主题背景色填充，不拉伸；不支持配置保留WPF回退。Standalone也支持。像素参考/暂停快照/生命周期覆盖在19项专项中，完整1433回归通过。
- 新VideoSlowFrameRecorder与不可变look-ahead计时快照，标记未完成producer；默认诊断关闭，最多300条详细慢帧，累计计数完整。关联计数重叠，不作因果归因。
- 两组可见窗口/质量90/请求24/禁用降档实机60秒：原生23.333FPS、p99 74.00ms；16:9简单双色视频23.538FPS、p99 82.98ms。均无输出错误，16:9不是复杂视频性能证明。
- 真实视频＋时钟长测1803.20秒，42273帧，23.443FPS（排除5.007秒背景暂停后23.444），p50/p95/p99=41.25/53.71/81.37ms；平均JPEG127234B、HID21.33ms。全程EffectiveFps=24、错误状态0，静态→视频→静态完成。用户确认暂停与恢复“正常”，独立保存visual-confirmation.json，没有改原始PhysicalDisplayConfirmed=false。
- 42272间隔中3115个>50ms，730关联重绘、1786关联读回、2720关联GC暂停，2326仍有在途预生成；详细保留300，其余累计计数保留。未达到均匀24FPS参考目标，不得标为稳定24或30FPS已解决。
- 私有内存220.62–254.59MiB，早/晚中位239.18/240.89MiB，句柄1458→1380，无明显持续增长但不是无泄漏证明；未做拔插/睡眠/多设备/复杂动画长测。证据artifacts/video-smooth-1793276166466，文档D92_VIDEO_SMOOTHNESS_ACCEPTANCE_20261029.md。
- 首次短测被应用占用保护阻止；正常关窗后驻托盘，用户退出后重试，未强杀/绕过保护。所有硬件测试结束后正常释放。
- 已安装20261029.6，488文件校验通过，旧版backup-68e18f09d95644f58d977f7fc9ff62e4；ZIP SHA786B61D91868DF040E76073E6A158EEEA36010522CA66AFC2FDD886ABA4E8331。settings SHA仍2E256A832DFE8607540ECF3AC7A326617F43746E01F19B159A551EF9A2EFEC1F，主题/图库保留。
- 已打开新版，真实窗口文本确认20261029.6、D92已连接、尚未开始输出；进程Responding=True。本轮无新截图，未自动提交推送，用户源码副本保留。
- 后续优先减少WPF原生BitmapSource往返与GC压力，做可重复单变量A/B；不要把本轮GC相关性当成唯一根因，也不要因16:9简单片低字节就声称所有视频已解决。

## 2026-10-29 编码分配单变量实验（尚未发布）
- 先试VideoRotationBuffer私有WriteableBitmap同步编码复用，像素/JPEG独立性专项通过但ABBA性能不一致：旋转约减1ms、Gen2降，p99反而一组85ms；已撤回全部生产及实验测试改动，源码实验快照留artifacts/video-encode-reuse-1793283639549。不把减少GC次数当作平滑度改善。
- 保留候选：VideoJpegFrameSource独享MemoryStream重置Position/Length复用容量，ToArray输出仍独立快照；等待在途工作后释放流。暂停/取消/预览所有权不变，无协议/质量/刷新率改动。
- 同一原生比例真实视频时钟24FPS离线15秒，新→旧→旧→新：新24.13/24.06FPS，p99 43.99/54.75；旧24.00/24.20，p99 43.77/44.43；分配约463KB/帧→265KB（-43%），Gen2约475–481无改善。不能称解决卡顿或稳定24FPS。
- 工具构建0警告错误、缓冲专项通过，完整1434项回归通过exit0。第一次dotnet DLL启动因DPI上下文缺失中止，改测试EXE后通过，没有改DPI检测。误用相对ArtifactsPath生成目录移入artifacts/video-encode-generated-quarantine保留，未删除用户文件。
- 详见D92_VIDEO_ENCODING_ALLOCATION_20261029.md。settings SHA仍2E256A832DFE8607540ECF3AC7A326617F43746E01F19B159A551EF9A2EFEC1F。
- 安装版仍20261029.6，进程8056仍在运行；已异步请用户托盘退出以做约2分钟HID前后对照，尚未回复，未发送本轮HID、未强杀/安装/打包/自动提交推送。后续先等用户关闭确认，再相同参数前后短测，不能把离线结果当实机结果。

## 2026-10-29 JPEG流实机复测与发布20261029.7
- 用户明确授权：以后独占设备测试可直接正常退出D92 Studio，不必重复请求关闭。必须保留未保存内容并遵守占用/整帧安全退出，不能把正常退出授权当成任意强杀许可。本次检查时软件/CRAFT已退出。
- 相同视频时钟、24FPS、质量90、预览可见、自动降档关，各60秒：旧24.189FPS/p99 49.14ms，新24.224/p99 49.01ms，慢间隔13→12，均无错误/降档，前后静态切换完成。基本持平，只证明本轮没有明显退化，不覆盖此前30分钟p99边界；无本轮新视觉/拔插/长测确认。
- 证据artifacts/video-encode-reuse-1793283639549/hardware-comparison.json；文档D92_VIDEO_ENCODING_ALLOCATION_20261029.md。离线分配约减少43%的结论保留，不宣称稳定24/30FPS全面解决。
- 已安装20261029.7，488文件校验通过；ZIP SHA1991EF30B7095F799020BCE4044E43EAFFA8B08DC7417B3B34F376E600BD4AF0；旧版backup-6096783e6ad641af9224ad4fef6817cf，settings SHA仍2E256A832DFE8607540ECF3AC7A326617F43746E01F19B159A551EF9A2EFEC1F。1434完整回归仍通过，无新增代码改动后重测需求。
- 已打开软件，未自动提交推送。下一步继续针对读回与WPF原生位图生命周期测量，维持单变量A/B，不能仅据Gen2计数推断原因。

## 2026-10-29 读回VideoFrame复用候选（未发布）
- WindowsLiveVideoFrameSource每会话复用一个BGRA8/Ignore CPU VideoFrame，CopyToAsync替代每帧CreateCopyFromSurfaceAsync，再写入原调用方数组；打开失败/关闭释放目标帧。HID、质量、FPS、队列语义不变。官方CopyToAsync文档已核对，不能推断底层驱动无分配或所有显卡都支持。
- ABBA各15秒原生视频时钟24FPS离线：旧读回5.34/5.39ms，新5.01/5.18ms；旧p99 44.04/44.96，新42.93/48.65；FPS约24持平，Gen2未改善，托管约266→262KB/帧。小幅阶段收益，不称解决卡顿。
- 新真实红蓝视频BGRA通道/alpha检查通过，缓冲专项与完整1435回归通过exit0，构建0警告错误。见D92_VIDEO_READBACK_REUSE_20261029.md、artifacts/video-readback-reuse-1793287040624/comparison.json。
- 按用户授权正常Alt+F4退出安装版，进程确已结束，不再要求用户重复关闭。硬件旧版测试在Unique connected target required处失败，尚未发送HID；随后只读枚举[]、PnP未发现5548:1011，记录hardware-before.log/device-list.log。未进行本轮实机、视觉、拔插、长测。
- 不发布安装候选，现有20261029.7已重新打开，settings SHA仍2E256A832DFE8607540ECF3AC7A326617F43746E01F19B159A551EF9A2EFEC1F；未自动提交推送。设备恢复后先做旧/新各60秒同参对照，检查输出兼容性，再决定发布。不要覆盖本轮失败原始目录，重试用新证据名称。

## 2026-10-30 视频模式与像素适配优化，发布20261030.1
- 用户授权继续1/2/3/4，随后明确“先进行其他测试，不要进行这一步”：跳过真实素材分类验收。只读过库元数据，未播放/修改个人手机视频或库16:9视频；不把合成片当复杂风景/人物验收，不做新模式30分钟长测。
- D92 C511D3782B05恢复，软件/CRAFT已退出，无需强杀；读回复用旧/新各60秒：23.998/24.067FPS，p99 53.02/53.75ms，无错误，完成静态切换。阶段小收益，不称整体提速。
- Core新VideoPlaybackMode(Custom/Quality/Smooth)与VideoQualityController；旧JSON缺Mode仍Custom。预设Resolve不破坏手动字段。质量优先24/Q90/不降帧；流畅30/Q80起、60–90、目标100KB、>110KB最多重编一次；范围内有缓慢恢复，最低质量仍可超预算，不承诺硬字节上限。自动降档仍持续5个低吞吐窗口30→24→20→10→5，不自动升帧。
- 独立视频和WindowsThemeSessionFactory共用策略，按真实已发送帧显示质量/字节/原因/实际FPS/上限；预设UI隐藏保留自定义参数，下次播放生效，按设备持久化。未改协议/驱动/独占/整帧超时机制。
- VideoCanvasNormalizer高度已匹配时跳过冗余垂直插值，整数水平位置直拷，半像素仍保留覆盖。640×360需放大无收益（13.16→13.50ms）；新生成1280×720双色H264解码到匹配高度归一化12.98→12.34ms，FPS约24持平。BitmapSource往返尚未消除，未声称已突破整体瓶颈。
- 最终1454完整回归/65专项通过、exit0，构建0警告错误；包含旧配置兼容/按设备模式/实际24与30间隔/恢复自定义/控制器上下限与暂停/整数半像素/WPF参考。日志artifacts/video-modes-final-regression.log等。
- 三组各60秒真实HID＋测试片时钟（均含5秒背景暂停和前后静态切换）：Quality24.149FPS/p99 49.62ms/JPEG127182B/HID19.15ms；Smooth29.867/p99 58.01/JPEG95388B/HID14.70/Q80→78、有效30；同目标Custom30Q90对照29.821/p99 57.75/JPEG127304B/HID19.77。均无错误/降档。FPS基本持平，Smooth仅明确减少约25%字节、26%写入耗时；短测非稳定30或面板呈现证明。
- 证据artifacts/video-modes-1793316982231；报告D92_VIDEO_MODES_ACCEPTANCE_20261030.md。向用户询问实屏文字/清晰度，尚未回复，未伪造视觉确认。真实素材/复杂动画/拔插/睡眠/多设备/新模式长测均未覆盖。
- 已安装并打开20261030.1，488文件校验，旧版backup-139f00b33584414e8130d628bd4e0dc9，ZIP SHA0DA11A81C0D16C4EBFE1277E7B9059ED3D386B8F8F1E1A0F6A20E7FB6FBACA74。settings SHA仍2E256A832DFE8607540ECF3AC7A326617F43746E01F19B159A551EF9A2EFEC1F，没有自动改用户模式或视频文件。
- 启动真实文本确认版本与D92已连接；设备页点击返回coordinate input geometry is unavailable，不盲点，未新截图/设置页现场验收。入口设备管理→原有D92 USB输出→视频模式。未自动提交推送；用户源码副本保留。
- 后续核心工作：更广泛的BitmapSource往返/原生内存消除仍待测量，不宜无限微调小缓冲；流畅模式待用户目视与长期/真实内容授权验收。用户已要求跳过真实素材，不可擅自恢复该项。

## 2026-10-30 RC收敛1/2/3/4，发布0.1.0-rc.20261030.1
- 用户授权做发布整理、新电脑验收准备、核心流程和统一反馈。未承诺当前环境可代替干净OS，WindowsSandbox.exe不存在；没有卸载.NET/厂商环境、创建用户、改安全策略或做公开发布。
- Core.Storage.StudioDataPaths统一默认APPDATA/DeskDisplay；仅显式D92_STUDIO_DATA_ROOT绝对路径隔离，库/主题/缓存/会话/配置/设备偏好统一，默认位置不变。WindowsDeviceLeaseProvider保持原全局锁路径。
- 首次启动自检D92_STUDIO_FIRST_START_SMOKE=1要求新隔离目录，加载真实主窗口、记录运行时/数据路径/无自动输出证明后正常退出，不操作真实用户配置。正常启动不自动退出。包内Test-Release.ps1可复用到其他电脑，但只验证实际运行所在环境。
- 安装保存d92-files.json；Restore-Previous.ps1校验备份/路径/运行进程再交换，保留现版本。旧无清单备份不能自动回滚，需重装原ZIP；回滚不回滚数据格式。Uninstall增加InstallRoot/NoShortcut，拒绝重解析目录，保留用户数据和备份。
- 新文档/Install.cmd/恢复工具/Test-Release放在app/Release并纳入哈希，不在ZIP根目录添加额外文件。旧preview客户端DLL实际InspectAsync已接受RC ZIP。README根目录和应用更新说明修正旧“不支持视频”描述。
- OutputTroubleshooting新增超时/访问/保存失败建议和有界具体错误描述，OutputCoordinator.Details含原始错误+建议；USB驱动状态、managed停止、workspace/资源错误同步全局。缺失素材按资源ID显式重试，目标变更则拒绝；详情“显示到D92”统一为所选UseLabel，不改驱动/协议。
- 构建0警告错误，1465完整回归exit0，223核心专项exit0，19配置冒烟，13安装事务。证据artifacts/rc-regression.log、rc-workflow.log、rc-config-smoke.log、rc-packaging-final.log。
- 实际自包含RC包隔离验收12项全通过：中文含空格安装，外部DOTNET_ROOT置空路径仍从包内运行时启动/加载窗口并正常退出；新数据零输出，无自动输出/恢复/开机注册；升级、校验回滚、卸载保留数据；真实用户settings SHA仍2E256A832DFE8607540ECF3AC7A326617F43746E01F19B159A551EF9A2EFEC1F。artifacts/rc-acceptance-1793322384868/result.json。
- D92 C511D3782B05真实核心输出短测：原生合成测试片+时钟，1920x462/90，静态→视频→静态，30秒722帧，活跃24.08FPS/p99 53.77ms，5秒背景暂停继续，无错误、正常停止。没有新增目视/拔插/睡眠/多设备/长测；个人素材验收仍按用户指示跳过。
- 发布/安装并打开0.1.0-rc.20261030.1，494文件验证，ZIP SHA491BCF7AAE3B1BFC7AF08692E7C655F542771BF96E083C69B7D20C00ED58403A；旧版backup-e9dece3ae4b847c3a3d91e5b91fea11c（无新清单，恢复需原ZIP）。真实窗口确认RC版本、D92已连接、本地预览尚未输出，进程1940正常响应。未自动改变视频模式或启动恢复。
- 报告D92_RC_ACCEPTANCE_20261030.md；包内RELEASE_NOTES/KNOWN_ISSUES/ACCEPTANCE明确干净Windows10/11、标准用户、未装厂商环境、实际DPI与签名等仍待测，只可受邀封闭测试，不是正式商业发布。回归快照不是实际Windows125%验收。
- 未自动提交上传源码；基础cd4d4c9上仍有开发改动和用户源码副本。候选以ZIP校验固定，正式发行前应归档源码/tag。后续优先其他电脑运行包内Test-Release及人工清单，不新增大功能，不擅自恢复用户已跳过的真实素材测试。

## 2026-10-31 D69 识别、离线协议依据及首次单帧
- 用户新接入 D69，明确授权直接继续。实时 HID 2100:0027、Usage FFA0/1、输入513/输出1025/Feature0、Input/Output Report ID均0；HidD序列号C511D3782B2E与PnP一致。
- 扩展tools/Inspect-D92Hid.ps1只读查询序列号和Report ID，未改生产D92枚举器。厂商现有日志将相同序列号识别为D69/固件V25.D69_LLT.01.002；D69主题配置1280x480，仅为配置证据，面板尺寸及传输方向仍需目视。
- 本机厂商DLL版本已不同于旧D92调查：SHA D1814EC62BCD096029A9756578EFCBCBB378826623F024FC5A74D403C5ABFBF1；EXE SHA19C742FCD06788E1EBCF5DCF896A209A738EC4DA956E59317E866B561CA109AF。离线EXE D69 sendExtendedFrame lambda RVA22E4A0，22E783设置B1，22E794调用SDDevice appendSendData。未执行厂商DLL/附加进程/调用固件工具。
- 只在tools/D92.ProtocolProbe增加显式D69单帧计划：强制序列号、尺寸1280x480或480x1280、Report0/B1/Hold0、capabilities复核、独占写句柄；D92默认身份规则保持。增加DeskDisplay进程保护。生产客户端尚未支持D69，没有打包安装。
- 构建0警告错误，33头/50分包/8无效输入检查通过，新D69计划6组拒绝检查通过。首次发送因运行中旧预览版被拒绝、没有写入；通过computer-use确认首页、正常Alt+F4退出（用户已授权），未强杀。运行的旧版路径artifacts/releases/D92Studio-0.1.0-preview.20261021.6-win-x64/app，不应当成RC版。
- 第二次发送一份CRT/DIS唤醒+一帧1280x480测试JPEG，58425B、58图像报告、主机写入25ms。证据artifacts/d69-first-frame-20261031/send-attempt-2.jsonl。用户视觉问题已发出，尚未回复；不能宣称D69实体显示通过，更不能宣称视频/重连/正式客户端接入完成。测试未发送模式、亮度、闪存、固件命令。
- 文档D69_HID_INSPECTION_20261031.md。当前Studio/CRAFT已退出，测试图待用户观察。settings当前SHA7830FB3E371AD3EED4496D9AC7CC8E0C37CD0A593E9852C4379DAB4A4C2F23E1，与历史记录不同；本轮无修改设置文件操作，但没有关闭前哈希，不能声称与历史配置逐字节不变。未提交推送。
- 后续用户要求重发；10:51:28单帧再次写入成功（58报告/16ms），但用户确认“只显示一秒就重启”，进一步确认“测试图消失，停留在开机LOGO”。实体持久显示验收失败，之前关于单帧保留画面的假设撤回。visual-failure.json独立记录，不改原始写入日志。
- 只读PnP LastArrivalDate=10:51:45.321、当前在线ProblemCode0；无连续枚举及对应系统事件，不能断言命令导致USB重启或已定位根因。没有再发送HID。
- 优先离线核对D69模式/会话/心跳/连接关闭，当前探针发送完就关闭，不能直接套用D92生命周期。新增静态反汇编artifacts/d69-screen-command-20261031.txt：D69 sendScreenCommandToDevice RVA233150设置模式相关状态并调用67990，尚未执行该模式命令。暂不继续实机循环，不开放正式客户端D69支持。
- 连接保持复测：探针增加可选SessionSeconds(默认0，仅D69，0..40)，保持同一独占连接、完整JPEG后每秒CONNECT，未发SCREEN模式/重置。两轮都仅图像+首次心跳成功，下一次写入IOException/HRESULT8007001F(Win32 31)，不到30秒即停止。第二轮用户明确要求重来，非自动重试。路径artifacts/d69-session-location.txt、d69-session-retry-location.txt。未获本轮视觉成功确认，不能说测试30秒通过。
- 原因未确定：保持连接仍失败，不能仅归因单帧后关句柄，也不能确定心跳是解法。停止重复当前流程，下一步优先离线核对模式/图像方向/调用链。EXE67990构造CRT00SCREEN+末字节(参数2=>00，参数1=>01)，未执行，不推断设置持久性。构建0警告错误，91原协议检查+3会话输入拒绝通过；生产客户端未改。
- 用户继续测试：仅改JPEG为顺时针90°的480x1280（57800B，同质量85/420重新编码），Session30秒/每秒CONNECT不变、无SCREEN模式。artifacts/d69-rotation-20261031-115646首轮30.022秒1帧57报告30心跳全写入；用户要求重发，repeat-115759第二轮30.026秒相同参数全写入，无IOException。还没有实体视觉确认，不能标为显示通过。
- 注意退出边界：两轮结束后约12-14秒PnP新LastArrival分别11:57:29.843、11:58:41.039，不能宣称退出后保持画面或重启问题全面解决。旋转/重新编码内容差异与长期/模式/关闭流程仍未分离。第二轮视觉问题已发出待回复，不继续自动重复。
- 用户在屏幕旁再次要求测试，observe目录见artifacts/d69-observe-location.txt；第三轮旋转JPEG30.038秒、57图像报告/30心跳全写入。用户明确确认“期间显示正常，结束后才返回 Logo”。visual-confirmation.json独立保存：30秒会话实体显示通过，退出后画面保持/正常停止未通过，不改变原始日志pending字段。不要再要求重复相同目视测试；下一步核对持续会话/停止边界，再测变化帧和长一点的会话。不能宣称正式客户端/重连/视频已支持。
- 用户继续：新增tools/D92.ProtocolProbe/DiagnosticClockFrames.cs专用STA+容量1请求队列，复用ClockScene/SceneView/SceneJpegRenderer，真实DateTimeOffset.Now采样，1280x480逻辑画布/90°=>480x1280/Q85，按秒变化。探针LiveClock显式开启，SessionSeconds最大120；原计划默认0。每帧5秒独立截止、单写者帧间心跳，无模式/固件/重试/重连。
- artifacts/d69-clock-20261031-120623：90.019秒88帧3173图像报告88心跳全部写入、87后续时间样本均不同秒，无IOException；前/中PnP LastArrival12:02:01.583，结束后新LastArrival12:08:07.154，退出边界仍未解决。视觉问题已发出尚未回答，result.json PhysicalDisplayConfirmed=false。不能宣称真实时钟实机目视通过、正式客户端支持、视频或自动重连通过。
- 构建0警告错误，离线真实渲染维度/变化/快照独立性/退出检查通过，91原协议检查和4LiveClock输入拒绝通过。没有更新安装包、配置或提交GitHub。
- 用户确认90秒D69实时时钟“画面和秒数正常，期间稳定”。已独立保存artifacts/d69-clock-20261031-120623/visual-confirmation.json，更新报告为主机+目视通过。原result.json反馈前false字段保留。仅本轮会话显示通过，退出后回Logo/重枚举、重连、多设备、视频、正式客户端接入均未完成。

## 2026-10-31 D69客户端实验接入，安装0.1.0-rc.20261031.1
- 用户要求加快并直接下一步；已将D69加入现有多设备发现/主题会话/设备卡片。默认D92HidDevice.Enumerate仍仅D92，EnumerateSupported一次扫描两种明确型号；D69独立2100:0027稳定键、复核序列号/Usage/报告长度/Report ID0。JPEG必须480x1280，否则任何图像报告前拒绝。D92旧键/视频节奏保持。
- Core D69Compatibility只允许1280x480逻辑、90°传输、每秒更新；Video Unknown，复杂插件和其他方向拒绝。WindowsThemeSessionFactory真实渲染质量85。设备页默认参数不串旧D92配置，保存偏好不覆盖，需要手工确认。D69可从设备管理/创作中心选，首页及资源库旧D92快捷入口仍不选择D69，不能告知用户这些快捷按钮已支持D69。
- 联调抓到独占句柄导致后台枚举漏设备，先失败5帧并保留记录；改FileShare.Read允许只读发现、拒绝第二写者。首次共享读30秒21帧未达阈值，保留失败；之后独立计首帧等待和活跃时间，30.307秒31帧6次枚举无错；旧轮没有阶段数据，不能断言低帧数仅由启动造成。最终单次扫描补测15.161秒16帧3次枚举无错，启动0.311秒。
- 正式会话链60.735秒60帧无错；用户当时要求重来，新的30秒视觉问题仍待回复，没有本轮新视觉成功确认。此前90秒探针时钟的用户确认保留，不能借给新轮。实机全针对C511D3782B2E，未验证本轮拔插/重连/多台/视频/竖屏/长测。
- 构建0警告错误，本轮全量曾1480 PASS；最后共享/单次枚举/提示修正后执行D69、多设备、设备页专项41/50/49 PASS（含公共前置，D69相关20项），95适配/会话与19配置冒烟通过。最终没有再全量跑1480，报告明确区分。证据artifacts/d69-full-regression.log、d69-final-*.log、d69-client-*-location.txt。
- 发布ZIP SHA CD4699E2C70B032CF4D45909E6754CDBDBF118A16FBAD1B0C07619C0FAF1DA6D，494文件；实际包隔离12项通过artifacts/d69-package-acceptance-20261031-123427。安装到LOCALAPPDATA/D92Studio/app，桌面D92 Studio快捷方式更新，备份backup-f21dc3861ccf4fff8b6c7fa1cd1ba608；settings SHA仍7830FB3E371AD3EED4496D9AC7CC8E0C37CD0A593E9852C4379DAB4A4C2F23E1。
- 已打开新版PID9836/window198410，真实窗口确认0.1.0-rc.20261031.1、尚未输出，Responding=True。Sky click设备导航返回coordinate input geometry is unavailable，重新观察后单次Tab仍焦点Window，不继续盲键；未完成实际设备页点击/截图，也未勾选/应用个人主题。用户可自行点击设备管理使用。
- 报告D69_CLIENT_ACCEPTANCE_20261031.md。源码用户副本保留，未自动提交推送。停止/退出后回Logo未解决，不能声称已完成；下一步正常停止/重连验收，再统一首页/资源库目标选择。
- 用户要求重发正式链路30秒；确认安装版在首页且无输出后，按既有授权正常Alt+F4关闭。最新artifacts/d69-client-observe-location.txt指向证据：活跃30.3156秒/31帧、6次并发设备刷新、无错误，启动0.5527秒。用户明确确认“方向、文字、秒数正常，期间稳定”，已独立保存visual-confirmation.json，原result.json反馈前false字段保留。本轮正式会话显示+后台刷新实机目视通过；退出后Logo、重连、视频、其他方向未验收。

## 2026-10-31 预览关闭崩溃修复，安装0.1.0-rc.20261031.2
- 用户报告未知错误退出。真实日志12:55:08.028 FTL：InvalidOperationException“在窗口关闭期间…无法调用…Close”，CreatorDevicePreview.ReleaseOnClose原34行；全局UI异常处理器Shutdown(1)，12:55:09.462退出。不是本轮HID写入错误。提取artifacts/preview-close-crash-20261031.txt。
- 日志此前两次Desktop/bbb.jpg FileNotFound已在CreatorDevicePreviewViewModel.RebuildAsync捕获，导致预览失败但本身非退出异常；它让DisposeAsync同步完成，从Closing事件finally再次Close产生重入。没有替用户删除/替换缺图或修改主题路径，bbb.jpg仍需重新关联。
- 修复src/DeskDisplay.App/Views/CreatorDevicePreview.xaml.cs：清理完成仅队列调度Close，等原Closing栈退出；OnClosed标记与Dispatcher关闭检查防止已关闭窗口再次关闭。没有屏蔽全局异常或改HID协议。
- CreatorDevicePreviewTests新增同步清理单次/重复/父窗口关闭三例，以及真实缺失素材后关闭两例。creator-device专项99 PASS（包含共同前置及布局检查）；theme-quick-use、creator-direct、D69专项通过；构建0警告错误。证据artifacts/preview-close-*.log。未重新全量跑1480项，也未再发HID。
- 包内更新说明加入修复，ZIP0.1.0-rc.20261031.2 SHA10CF8C0367C58E3D1FBEA41CD4349C5B7AB30495356196DC85E962CD5E040371；实际包隔离12项通过artifacts/preview-close-package-20261031-130155/result.json。安装成功，备份backup-2834d31be0654b93a899e57a5341bc60。
- 用户在上版运行期间已修改配置，修复开始时settings SHA为F0A70DAC3622480EA0C1AB0537159B55131B3EA98FD0C7FD1FCF0998C16B8937；包验收/安装/重新启动后仍相同，不回退到更早7830。未覆盖配置、资源库或主题。
- 已打开新版PID8548/window12519246，Responding=True，真实窗口确认0.1.0-rc.20261031.2和“尚未开始输出”。没有自动提交推送。D69停后Logo/重连/视频等边界仍不变。
