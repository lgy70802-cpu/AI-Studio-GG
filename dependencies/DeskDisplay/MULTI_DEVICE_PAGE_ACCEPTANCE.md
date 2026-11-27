# 多设备管理页交付与验收

日期：2026-10-22。分支：`feature/multi-device-foundation`。

本轮完成阶段3中的设备页、逐台/所选设备控制和当前连接身份确认。创作中心的目标设备预览、多布局版本编辑、跨连接物理设备关联和完整V2持久化尚未完成，不将本轮称为整个多设备架构的最终验收。

## 使用入口

左侧「设备管理」打开新版页面。后台发现默认启用，不自动输出；环境变量 `DESKDISPLAY_MULTI_DEVICE=0` 可回退到原副屏界面。原有副屏设置与输出保留在页面下方的折叠区，USB、首页、图库和创作中心原入口保持可访问。

1. 选择已保存的创作主题或当前主预览中的真实 ThemeScene 源项目。没有源项目时列表为空，不填充演示主题。
2. 核对设备卡片的型号、连接方式、标识、连接状态、像素和方向；USB填写表面尺寸及传输旋转，系统副屏读取实际分辨率且不额外旋转。
3. 勾选当前设备和参数确认，再点击「应用主题」。每台设备单独显示准备、等待首帧、输出、等待设备恢复、失败或停止。
4. 勾选卡片后可「应用到所选设备」或「停止所选」；各设备保留自己的结果，一个参数错误不会中断其他设备。没有选中卡片时批量按钮不会空执行。
5. 「停止此设备」只停止对应管理器会话，也可取消正在异步准备的会话。它不强制抢占或结束原兼容入口、其他进程的输出。
6. 「保存设备设置」只保存名称、像素和旋转；重启不自动开始输出，也不保存人工确认状态。

旧入口已经持有设备锁时，新入口会报告占用，需要用户先停止原输出。静态图片、时钟、文字、形状及已启用的股票/相机组件继续复用既有场景和渲染器；D92 USB仍不支持VideoScene，旧Stretch需要先调整。布局默认按真实比例选择并完整等比显示，不修改编辑器源坐标。

## 数据与身份

- 新增 `%APPDATA%\DeskDisplay\managed-devices.v1.json` 独立设备偏好文件，不改原settings.json和主题文件。
- 初次显示时只给序列号精确匹配的唯一D92读取原USB参数；不会把一台的分辨率复制给其他设备。无已知配置的USB显示待填写的0值，不能直接启动。
- 保存前校验格式、尺寸、数量和名称；未知版本或损坏文件保留并禁用覆盖。原子替换与写入句柄锁保护保存，合并其他已存在的设备记录，保留断开设备的偏好。
- 改变输出参数或观察到断开/端点/身份变化会清除勾选确认；已观察的当前连接epoch不匹配时拒绝确认。
- 对重复序列号、已被现有枚举发现的USB候选，人工确认只绑定本次连接路径。发送前仍检查VID/PID/serial/Usage，候选不存在时不回退到另一台；I/O断开后停止并要求重新确认，不能把这种人工确认当作稳定硬件唯一标识。
- 手工路径额外持有该序列号的共享锁，因此相同序列号的候选保守地只允许一个输出，防止原序列号入口绕过路径锁；不声称重复序列号双机已可安全并行。
- 当前D92原枚举仍排除空序列号设备，本轮没有解除该限制。系统显示源重复/镜像或缺少可唯一寻址的标识时明确阻止应用。

## 修改文件

| 文件 | 内容 |
|---|---|
| `src/DeskDisplay.App/ViewModels/ManagedDevicesViewModel.cs` | 真实卡片、主题选择、逐设备/批量操作、配置保存、确认和状态同步 |
| `src/DeskDisplay.App/Views/ManagedDevicePanel.xaml` / `.xaml.cs` | 设备VM驱动的Soft UI两列卡片、工具栏、空状态及兼容入口 |
| `src/DeskDisplay.App/Views/DeviceManagementView.xaml` / `.xaml.cs` | 保留主VM外层容器，正确接入内层面板并保留USB导航；不混用两层DataContext |
| `src/DeskDisplay.App/Views/WorkspacePanel.xaml` | 设备页切到新容器 |
| `src/DeskDisplay.App/ViewModels/DevicesViewModel.cs` | 新管理器附加和正常关闭等待 |
| `src/DeskDisplay.App/App.xaml.cs` | 默认装配发现与UI；仅显式操作后开始会话；复用真实主题和资源服务 |
| `src/DeskDisplay.Infrastructure/Configuration/DevicePreferenceStore.cs` | 独立、版本化、合并/原子保存的设备设置 |
| `src/DeskDisplay.Core/Devices/DeviceRegistry.cs` / `DeviceContracts.cs` | 当前连接确认、身份变化撤销和人工确认判定 |
| `src/DeskDisplay.Display/Services/WindowsDeviceDiscovery.cs` | 串行异步刷新与实际显示器快照，不在UI执行HID枚举 |
| `src/DeskDisplay.Display/Services/WindowsThemeSessionFactory.cs` | 拒绝无法唯一寻址的系统显示源，传递确认过的USB描述 |
| `src/DeskDisplay.Display/Usb/D92HidRenderTarget.cs` | 人工确认路径筛选、重复序列号保护、手工确认设备断开后停止；不改报文和分包 |
| `tests/DeskDisplay.DisplayTests/ManagedDevicePageTests.cs` / `Program.cs` | 页面行为、真实WPF截图、保存和身份边界检查，加入完整回归 |

## 验证结果

- 改造前后解决方案构建均为0警告、0错误。
- 最终Windows完整回归667项通过，包含设备页27项；配置SmokeTests19项通过，平台无关适配/会话95项通过。移除旧单设备状态文案后，页面专项27项再次通过，并再次完成解决方案构建。各计数不相加冒充同一个测试套件。
- 专项覆盖真实VM→SessionManager、默认无输出、逐设备确认、修改参数撤销确认、批量部分失败隔离、单台停止、源主题不变、设置合并及未知版本保留、单台拔插模拟、确认epoch、重复serial路径限定和身份变化撤销。
- 1100×740和1320×900实际WPF窗口离屏渲染截图已经人工查看，修正背景、断开颜色和首帧状态文案。截图使用明确标注的离线测试源与设备替身，不是两台实体D92的连接证明。

截图：`artifacts/device-management-1100.png`、`artifacts/device-management-1320.png`、`artifacts/device-management-shell-1320.png`。第三张来自实际MainWindow外壳；主窗口设备导航和原USB按钮另有明确断言。收尾曾发现主VM容器与设备VM面板的绑定层次差异，已拆分并补齐实际主窗口路径验证。

日志：`artifacts/device-page-build.log`、`device-page-tests.log`、`device-page-regression.log`、`device-page-adaptation.log`、`device-page-smoke.log`。

运行：

```powershell
dotnet build DeskDisplay.sln --nologo -v quiet
dotnet run --project tests/DeskDisplay.AdaptationTests --no-build
dotnet run --project tests/DeskDisplay.DisplayTests --no-build
dotnet run --project tests/DeskDisplay.SmokeTests --no-build
```

只做页面专项：设置 `DESKDISPLAY_TEST_SUITE=device-page` 后运行DisplayTests，完成后清除该环境变量，避免误以为只跑专项就覆盖了完整回归。

## 尚未验收与后续任务

本轮没有发送真实HID命令、修改驱动/固件、执行实机长测、发布安装包、重启正在使用的软件或提交/推送Git。

仍需验证真实单/双D92、新人工路径选择、真实系统显示器热插拔、多DPI与小时级内存。macOS需要在其实际仓库和设备上单独验收。软件显示的「已启动」不等于实体画面已经正确。

USB与系统连接是否对应同一物理屏还没有持久人工关联；当前页面明确要求同一物理屏只选择一种输出方式，不能声称跨端点物理冲突已完整解决。未知第三方占用仍不能仅凭软件锁证明可用，保留原Craft保护且不强制抢占。

下一轮建议接创作中心目标设备预览与布局选择，切换预览不得改写源项目；再补跨端点人工关联、设备主题关联持久化和完整V2布局保存。
