# 创作中心稳定性与交互验收（2026-10-21）

## 本轮完成

1. 主题输出方向使用通用 ThemeOutputGeometry 映射：切换设计方向时交换逻辑宽高并修正四分之一圈旋转，保持已配置的编码光栅尺寸；不覆盖用户设备配置，不改 HID 报文。
2. 组合名称随源项目保存；组合展开/折叠、整体锁定/隐藏、整体旋转、对齐画布、复制/粘贴适用样式及缩放设计尺寸反馈。每次整体修改可一次撤销，锁定成员不参与几何修改；仍为平级组合。
3. 素材窗口支持真实大图预览、双击添加、页码/选中/滚动位置恢复及缺失文件警告。预览关闭释放详情图，使用原资源库与共享缓存。
4. 缩略图按最长边不超过640像素解码，不放大原图；素材翻页复用72项温缓存并保护当前页，避免竖图按固定宽度解码导致过大内存。
5. 紧凑窗口、缩放渲染、实际可用显示器枚举与移动检查。

## D92 定位依据

历史失败记录仍保留在 CREATOR_CENTER_ADVANCED_ACCEPTANCE.md：逻辑462×1920、旋转90°对应的编码尺寸出现反复写入取消。修正为逻辑462×1920、旋转0°后，40秒测试发送40帧、1次连接，无传输错误。横屏1920×462、旋转90°也对应相同编码尺寸。

据此实现保持既有编码尺寸的方向映射。接收端内部限制的具体机制未知，不推断未知字段，不重新探索协议。生产映射使用实际配置参数，以上数字仅为本次设备测试配置。

30分钟测试于09:36:22至10:06:22正常完成（状态时间跨度1799.94秒，计划1800秒），累计1782帧，1次连接，0条错误，最终Stopped、退出码0。用户确认竖放时画面方向与走秒正常，并确认拔下约5秒接回后画面正常恢复。但完整日志没有WaitingForDevice事件且连接次数未增加，因此自动重连日志验收仍未通过，不能声称本次已取得物理断开/重连的机器证据。后续需要核对实际拔插设备及诊断所选序列号，再单独复测。原始日志artifacts/creator-stability-30min.jsonl；结构化摘要artifacts/creator-stability-acceptance.json。末段进程采样仅为局部内存观察，不作为全程无泄漏证明。

## 自动化与界面检查

- Release构建：0警告、0错误。
- 完整DisplayTests：455项通过；最终尺寸反馈标签调整后再次构建，创作专项167项通过。
- SmokeTests通过。日志：artifacts/creator-finishing-build.log、creator-finishing-regression.log、creator-finishing-focused.log、creator-finishing-smoke.log。
- 新测试覆盖真实USB ViewModel方向映射/配置保留、组合持久化及撤销、样式不覆盖内容、100份真实临时素材分页与缓存、缺失文件、双击、大图窗口释放与状态恢复。
- 截图：tests/DeskDisplay.DisplayTests/bin/Release/net8.0-windows10.0.19041.0/ 下的 creator-group-management.png、creator-scaled-125.png、creator-scaled-150.png、creator-material-browser-retained.png。已检查组合界面与150%缩放截图。
- 当前实际环境只有一台1600×900、96 DPI显示器。125%/150%为WPF缩放渲染检查，不能当作真实Windows缩放设置或跨不同DPI显示器验收；实际多DPI环境仍需补测。

## 主要源码

- src/DeskDisplay.Core/Models/ThemeOutputGeometry.cs、ThemeProject.cs
- src/DeskDisplay.App/ViewModels/CreatorFinishing.cs、CreatorViewModel.cs、UsbOutputViewModel.cs、LibraryViewModel.cs、WallpaperItemViewModel.cs
- src/DeskDisplay.App/Views/CreatorView.xaml/.cs、CreatorMaterialBrowser.xaml/.cs、ThemeCanvas.cs
- tests/DeskDisplay.DisplayTests/CreatorFinishingTests.cs、CreatorWorkflowTests.cs
- tools/DeskDisplay.UsbDemo/Program.cs：沿用生产渲染与HID链路，增加首帧编码尺寸诊断。

## 保留与限制

保留既有Windows副屏、场景、资源、插件、设置与HID传输链路。未修改设备驱动或固件。包含视频的主题仍不支持D92 USB输出。30分钟局部设备验收不代表小时级、所有素材复杂度或高帧率能力，也不代表macOS已验证。

运行包：artifacts/releases/D92Studio-0.1.0-preview.20261021.1-win-x64.zip。

