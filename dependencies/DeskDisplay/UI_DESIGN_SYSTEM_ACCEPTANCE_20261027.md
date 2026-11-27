# UI 重构第一批：公共按钮基础

本批修改 Themes/Colors.xaml 与 Themes/Controls.xaml，不代表全局 UI 重构完成。

- 正文色统一为 #202C43；新增控件底色及危险按钮颜色资源。
- 保留 SoftButton、PrimaryButton 等原资源键。悬停/按下采用轻量遮罩，避免主按钮白字遇到浅色底的问题。
- 内容对齐、边框、内边距通过模板绑定保留；支持 AccessKey；键盘焦点增加边框；禁用按钮允许显示已有提示。
- 新增显式 IconButton、DangerButton，供后续页面逐步使用，不强行改变旧编辑器控件尺寸。
- 未变更 ViewModel、设备驱动、主题格式、用户设置或输出状态。未安装替换运行版。

验证：改造前与改造后 Windows 完整回归均 1243 PASS、退出码 0；应用构建 0 警告、0 错误。日志分别为 artifacts/ui-system-baseline.log、ui-system-regression.log、ui-system-build.log。

尚未进行截图、多 DPI 人工视觉验收或新 HID 实机测试。后续依照 UI_REFACTOR_PLAN_20261027.md 分批推进表单、筛选、首页及其他页面。此前待安装的功能包状态保持不变。
