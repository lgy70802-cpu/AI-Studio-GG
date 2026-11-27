# 多设备与主题自适应：阶段 1

日期：2026-10-22。分支：`feature/multi-device-foundation`。

后续记录：阶段2的Windows会话底座和离线验收见 `MULTI_DEVICE_SESSIONS_ACCEPTANCE.md`；本文保留阶段1交付时的范围说明。

## 已完成的边界

本阶段仅新增 Core 平台无关模型、纯几何规则、V1 内存读取适配和测试项目。未把它们注册到 App 的依赖装配或生产输出入口。运行中的软件仍使用原有单设备实现；尚无新的多设备 UI。

没有发送 HID 指令、修改驱动、改变协议、覆盖用户配置、改写旧主题或自动升级主题包。既有未提交的改动保留，未提交或推送 Git。

## 实现文件

| 文件 | 内容 |
|---|---|
| `src/DeskDisplay.Core/Devices/DeviceContracts.cs` | DeviceDescriptor、身份可信度、端点 epoch、DisplayProfile、像素及旋转验证、能力证据、独立 DeviceProfile、保守身份判定 |
| `src/DeskDisplay.Core/Themes/ThemeDocument.cs` | SchemaVersion 2 的主题、多 LayoutId、不可变图层快照、设计坐标、基础约束及校验 |
| `src/DeskDisplay.Core/Themes/LegacyThemeAdapter.cs` | V1 只读内存转换，确定性横竖 LayoutId，保留图层全部既有参数、资产/云端引用、组及编辑参考线 |
| `src/DeskDisplay.Core/Adaptation/LayoutSelector.cs` | 人工覆盖优先于已保存布局；按方向、真实比例、默认布局和稳定 ID 匹配；默认 1% 可配置容差 |
| `src/DeskDisplay.Core/Adaptation/LayoutResolver.cs` | Contain、居中 Cover、基础 Responsive 锚点/相对位置/设计单位最小边距；不反写源；输出裁切、方向、旧 Stretch 和约束诊断 |
| `src/DeskDisplay.Core/Output/OutputContracts.cs` | 设备主题关联、期望/实际状态、会话快照、平台输出适配接口；本阶段只有契约，没有生产会话管理器 |
| `tests/DeskDisplay.AdaptationTests/*` | 独立 net8.0 测试可执行项目和仅供测试使用的 VirtualDisplayTarget |
| `DeskDisplay.sln` | 添加测试项目，保持原有 Any CPU 配置 |

Core 和新增测试项目均为 net8.0，无新 NuGet 依赖，不引用 WPF、Win32 或 HID 驱动。尚未在 macOS 运行，不能将 Windows 上通过的平台无关测试等同于 Mac 验收。

## 规则与兼容性

- 主题不包含物理设备 ID；设备到主题/布局的关联使用独立契约。
- 显示表面像素与传输像素分开；当前旋转契约明确要求 0/90/180/270 度且传输尺寸与旋转一致。未来其他传输缩放需另行能力设计，不能默认支持。
- 默认容差 1% 是应用规则，不是硬件能力结论。4:1 与 1920×462 不会被误判为精确匹配；2557×615 与 1920×462 可得到容差匹配。
- 无同方向布局、无效人工覆盖或失效的保存布局会明确报错，不自动换成另一方向。
- 源布局、解析结果与每目标输出尺寸分离；布局匹配与适配均不修改源坐标。内容统一缩放；Responsive 当前支持单轴 Start/Center/End/Relative 定位，不包含双边拉伸容器、自动避让、文本重排或最小可读字号算法。
- Cover 目前居中裁切；可调裁切对齐、分辨率清晰度和字体/插件能力预检尚未接入。
- 旧 Stretch 配置原样保留，并产生 `LegacyStretchRequiresReview`。几何解析成功不意味着旧图像已经消除拉伸；未来严格输出入口须处理该诊断，再投影到真实渲染器。
- 所有原有图层配置使用 JSON 快照保留；这是现有组件配置的兼容表示，不是另一套素材数据库。尚未接入完整 V2 保存、包导入导出或渲染配置转换器。
- 新身份规则在缺号/重复序列号时要求确认；未集成真实发现注册表或人工绑定 UI，不声称解决了第三方设备占用。

## 测试与运行

在仓库根目录执行：

```powershell
dotnet build DeskDisplay.sln --nologo -v quiet
dotnet run --project tests/DeskDisplay.AdaptationTests
dotnet run --project tests/DeskDisplay.DisplayTests --no-build
dotnet run --project tests/DeskDisplay.SmokeTests --no-build
```

改造前解决方案构建 0 警告、0 错误；现有 DisplayTests 和配置 SmokeTests 通过。新增适配测试 **63 项通过**；完成实现后整套解决方案构建 0 警告、0 错误，DisplayTests **611 项 PASS**、配置 SmokeTests **19 项 PASS**。计数来自本轮实际日志，未沿用历史验收数量。

新增测试覆盖：V1 数据完整性和不反写、JSON 往返、稳定布局 ID、精确/容差/人工匹配、横竖独立、统一缩放、Contain/Cover、Responsive 锚点/边距、旋转边界、旧 Stretch 提示、异常尺寸/配置、身份缺失/重复、同主题不同尺寸及不同主题虚拟输出、单目标断开/人工重新连接/停止、取消和错误目标尺寸。

一万次模拟更新只检查测试目标保留最后一帧，不是实际一万秒运行，也没有验证生产调度器、输出队列或内存趋势。虚拟目标的重新连接由测试明确切换状态，不是自动重连认证。

日志：`artifacts/multi-device-baseline-display.log`、`multi-device-adaptation.log`、`multi-device-build.log`、`multi-device-regression.log`、`multi-device-smoke.log`。这些构建产物可能被 Git 忽略；移交时可另附，不需要发布用户配置。

## 下一阶段

先实现 Windows DeviceRegistry / 会话租约 / 每设备独立场景及资源生命周期，并将旧输出入口一并纳入互斥管理；随后才接设备卡片、多设备应用和编辑器设备预览。保留原 HID 发送器与协议。双设备实机、Windows 系统显示器组合、长时间内存/队列和 Mac 输出需后续独立验收。
