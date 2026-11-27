# 编辑器设备预览与秒钟边框

## 行为

在“保存”和“完成并使用”之间新增“预览”。提交当前可见输入后，创建真实主题快照；唯一已确认的连接设备可直接应用，不保存源主题，也不改变撤销历史。输出保持运行，直到再次切换或手动停止。多设备或未确认设备打开目标选择与确认窗口，不猜测目标。未连接时提示连接。保留原设备能力、素材预检、所有权及租约检查，不扩展 D92 USB 视频能力。

数字时钟开启秒钟时，按真实字体/组件度量扩展不足的宽高，保持中心、字体大小及原有更大边框；关闭秒钟不强制缩小用户边框。开关与边框变化是一个撤销事务。已有主题不批量迁移，可关闭再开启秒钟或使用“适应内容”。

## 文件

- `src/DeskDisplay.App/Views/CreatorView.xaml`
- `src/DeskDisplay.App/Views/CreatorDevicePreviewEntry.cs`
- `src/DeskDisplay.App/ViewModels/CreatorViewModel.cs`
- `src/DeskDisplay.App/ViewModels/CreatorClockSizing.cs`
- `tests/DeskDisplay.DisplayTests/ClockComponentTests.cs`

## 验证

日志 `artifacts/quick-preview-build.log`、`artifacts/quick-preview-regression.log`。新增秒钟宽高/中心检查、单步撤销与真实 WPF 按钮检查，复用无保存回调输出切换、素材失败保留旧输出、外部占用和多设备隔离回归。没有自动发送新的实体 HID 验收画面。

结果：构建0警告0错误，完整Windows回归1142项通过（退出码0）。.8安装包已生成，等待用户保存关闭后安装。

安装补充：用户保存关闭后已安装并打开.8，settings哈希一致，旧应用备份保留。
