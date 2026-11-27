# 创作中心按设备预览与应用验收

日期：2026-10-22。分支：feature/multi-device-foundation。

## 已完成

入口：创作中心 → 打开主题 → 预览 → **按设备预览与应用**。

- 从真实设备注册表选择目标，依据实际表面宽高和方向预览；显示传输旋转，横竖布局独立选择。使用主题快照，只读适配，切换设备不改变编辑项目、脏状态或撤销历史。
- 完整显示与等比裁切使用现有 LayoutSelector / LayoutResolver。预览与独立输出共用 WindowsThemeSessionFactory 的素材准备、ThemeScene 和布局变换，时钟读取真实系统时间。预览不建立输出会话、不获取设备租约。
- 显示比例匹配、留边/裁切、图层越界、字体缺失等诊断。缺失素材或插件导致准备失败并禁用当前目标应用；缺失字体提示实际回退。旧非等比 Stretch、方向或边距问题不能静默应用。新会话路径仍不支持视频，不增加 D92 USB VideoScene 能力声明。
- 可应用到当前预览设备或所选设备；复用设备参数确认、连接状态、占用和独立会话检查。每台显示自己的结果，一台失败不取消其他目标。不修改设备页原先选择的主题。关闭预览释放预览资源，已启动的输出由设备页独立停止。
- 快速切换取消并丢弃过期准备结果；场景、计时器、订阅和图片引用在关闭时释放。

## 本轮文件

- 新增 App/ViewModels/CreatorDevicePreviewViewModel.cs。
- 新增 App/Views/CreatorDevicePreview.xaml、对应代码及 CreatorDevicePreviewEntry.cs；修改 CreatorView.xaml 增加入口。
- 修改 ManagedDevicesViewModel.cs，集中目标参数验证，提供快照应用与可辨识设备标签；App.xaml.cs 装配同一预览工厂。
- 修改 Display/Services/WindowsThemeSessionFactory.cs，抽取共用场景准备；新增 ThemeDevicePreview.cs 管理预览生命周期。
- 新增 tests/DeskDisplay.DisplayTests/CreatorDevicePreviewTests.cs；Program.cs 注册专项及完整测试。

以上 App/Display 路径位于 src/DeskDisplay.App 与 src/DeskDisplay.Display。

## 验证结果

- 完整解决方案构建：0 警告、0 错误，artifacts/creator-device-build.log。
- Windows 完整回归：686 项通过，artifacts/creator-device-regression.log；其中新增设备预览 19 项，包含真实 MainWindow → CreatorView 入口、快照不变、实际 WPF 绘制、走秒、横竖切换、预览/输出布局一致、无预览租约、独立应用和释放。
- Core 适配/会话：95 项通过，artifacts/creator-device-adaptation.log。
- 配置冒烟：19 项通过，artifacts/creator-device-smoke.log。
- git diff --check 通过；Git 提示部分既有文件行尾将转换，不是空白错误。
- 已检查真实 WPF 离屏截图：artifacts/creator-device-landscape.png、artifacts/creator-device-portrait.png。截图设备和主题为明确标识的离线测试替身；故意超出画布的文字触发裁切诊断，不代表真实双 D92 输出。原生窗口边框在离屏截图底部有黑色区域。

## 保留边界

本轮完成目标设备预览、诊断、创作中心单/多目标应用入口；未完成 V2 多布局文件写回、布局版本编辑和跨 USB/Windows 端点的持久物理关联。相同物理屏请只选择一种连接。旧主题仍通过内存只读适配；不能宣称完整多布局创作已经完成。

没有发送真实 HID 命令、改协议/驱动/固件或用户配置；没有发布、重启用户正在运行的应用、提交或推送。真实双 D92、系统显示器热插拔、长时间吞吐/内存、多 DPI 和 macOS 未在本轮验收。离线替身输出通过不等于这些硬件能力已验证。

下一步：实现版本化多布局保存/迁移，再补物理端点关联与真实多设备验收。
