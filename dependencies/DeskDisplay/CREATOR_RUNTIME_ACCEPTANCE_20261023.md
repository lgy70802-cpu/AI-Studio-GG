# 新版启动与创作流程验收 — 2026-10-23

## 版本与启动

- 创建本地测试包0.1.0-preview.20261023.1，使用现有Publish-WindowsTest.ps1，保留用户原配置。
- ZIP：artifacts/releases/D92Studio-0.1.0-preview.20261023.1-win-x64.zip。
- SHA256：3D5284375161C4BFC3D86F7BAFC589F0C16F56E70092385CEC43D129D8B708FE。
- 启动前没有DeskDisplay进程；通过computer-use启动新包，实际窗口控件树正常，首页报告D92已连接。随后正常关闭以释放设备，再用原验收工具独立测试；没有强制结束用户进程。
- 本轮仅新增渲染倍率测试和验收资料，未改应用、协议、驱动或固件源码。发布包已包含此前多布局与八项体验优化。

## 编辑操作与窗口检查

本机远程桌面的Windows.Graphics.Capture返回FrameArrived超时，鼠标点击返回coordinate input geometry is unavailable。仅能读取真实控件树及部分键盘操作，无法可靠完成鼠标拖动验收。没有改用其他Windows UI自动化通道绕过此限制；因此不能宣称“实际拖动手感已通过”。

补充的隔离WPF检查覆盖1100×740窗口横屏/竖屏，以及100%、125%、150%输出栅格倍率，共六张真实WPF渲染截图。它们验证布局与渲染，没有修改系统缩放，也不等同于真实显示器Per-Monitor DPI切换。已检查横屏150%及竖屏125%截图，画布方向与实际比例保持。

新增文件在原CreatorUsabilityTests.cs中，复用真实MainWindow/CreatorView；场景是临时真实可编辑主题，图片来自临时文件，不是硬编码线上资源。截图artifacts/creator-acceptance-{landscape,portrait}-{100,125,150}.png。

## 创作流程回归

- 解决方案构建0警告、0错误：artifacts/acceptance-20261023-build.log。
- 创作核心/素材管理/工作流专项302项通过：acceptance-20261023-creator.log。包含真实图片引用、编辑参数保存重开、横竖布局、缺失素材、输出前置检查及资源释放；100图层与100帧短循环继续通过。
- 设备预览/多布局专项42项通过：acceptance-20261023-devpreview.log。
- 体验与渲染倍率专项27项通过：acceptance-20261023-layout.log。新增7项，含六张倍率渲染及竖屏几何。
- 三次是独立专项运行，不作为一次完整回归计数，不将短时内存/渲染循环解释为长测。
- 这些自动化结果与下一节实体传输结果分开记录，不冒充整段流程由鼠标在用户窗口中执行。

## 实体D92

当前只发现1台序列号匹配的D92，使用已保存的1920×462、90°参数。工具复用生产Scene/Creator/USB链路，主题包含可编辑图片、文字与真实系统时钟；用户settings.json仅做读哈希，工具写入隔离目录。

首轮：60.96秒，静态首帧1帧、最终61帧、1次连接、0次失败，正常Stopped，userSettingsUnchanged=true。用户回复“方向、文字和秒数均正常”，记录在acceptance-20261023-visual-confirmation.json。机器原始result.json保留原字段，视觉确认作为旁证保存，不改写原始日志。

首轮工具的暂停/恢复/手动切换分别在80/95/110秒触发，因此60秒轮次没有覆盖它们，另用120秒隔离轮次补测。延长轮次实际120.68秒、120帧、1次连接、5次主题显示事件，pauseChecked/resumed/manualNext均true，0次失败、0次等待，正常Stopped，userSettingsUnchanged=true。延长轮次视觉未单独确认，不扩展首轮用户确认的范围。

原始证据：artifacts/hardware-landscape-20261023/、hardware-extended-20261023/及acceptance-20261023-*-plan.json。

## 尚待验收

- 实际鼠标拖动、四角缩放、Shift自由缩放及操作手感。
- 真实系统100%/125%/150% DPI与跨显示器切换。
- 本轮竖屏实体输出、USB拔插和双设备并行；不能复用以前验收替代本轮新证据。
- 小时级持续运行与macOS。

诊断输出已停止并释放设备，已重新启动发布包，进程9696、窗口DeskDisplay、Responding=True；实际控件树仍显示D92已连接。没有自动重新启动诊断输出，没有提交或推送代码，没有覆盖原配置。
