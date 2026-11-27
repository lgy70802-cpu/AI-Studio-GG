# 创作中心属性区整理（2026-10-23）

## 功能位置

- 中央上方：真实布局选择器、布局操作菜单（复制、复制到另一方向、删除）、按设备预览、面板折叠。布局重命名保留在折叠的布局管理区。
- 中央底部：当前设计尺寸；视图菜单（网格、间距、吸附、标尺/参考线）、适应窗口、缩放。
- 选中浮动条：复制/粘贴、图层顺序菜单、单选锁定、删除。各操作复用原命令。
- 右侧：文字/时钟/图片专属内容优先；外观用透明度滑块+数值、色块+色值；位置尺寸、更多文字设置、名称/样式复制等默认折叠。
- 多选的对齐/分布收进折叠区；没有选中对象时仍可编辑画布，主题分类收进项目信息。

## 代码与检查

- `src/DeskDisplay.App/Views/CreatorView.xaml`：重新组织实际WPF控件及绑定。
- `src/DeskDisplay.App/Views/CreatorView.xaml.cs`：视图菜单的参考线开关接原Canvas。
- `tests/DeskDisplay.DisplayTests/CreatorUsabilityTests.cs`：新增布局选择位置、弹出菜单DataContext、网格/参考线实际开关、默认折叠断言。
- 构建采用独立 `artifacts/properties-build/`，避免覆盖正在运行且锁定exe的原Debug目录。首次默认目录构建的文件锁错误保留日志；独立目录最终构建0警告错误。
- 体验专项38项通过；横竖WPF截图已复核，使用临时可编辑主题与真实Scene。截图为远程环境的实际可用窗口尺寸，非原生多显示器DPI实测。

## 边界

不修改Core、HID协议、渲染器或用户设置。不新增USB视频能力。不用假数据替代主题。图层拖动/缩放仍走原逻辑；原生鼠标交互手感仍需人工确认。

证据：`artifacts/creator-properties-build.log`、`creator-properties-tests.log`、`creator-properties-regression.log`、`creator-properties-landscape.png`、`creator-properties-portrait.png`。

- 旧UI测试同步先展开名称、几何、对齐分组，再验证原绑定行为；涉及CreatorTests、CreatorWorkbenchTests、CreatorWorkflowTests、CreatorAdvancedTests，未删除原断言。
- 尝试正常切换运行窗口时出现真实保存提示；远程会话控件树未返回且截图FrameArrived超时，已Esc取消关闭并确认对话框消失，保留原编辑窗口。未盲点保存/放弃、未强制结束进程，新版尚未替换正在运行的窗口。


## 最终结果

- 完整Windows回归785项通过，退出码0；最终构建0警告0错误，diff空白检查通过。
- 专项38项与完整785项为分次结果，不相加。
- 新版启动文件：artifacts/properties-build/Debug/net8.0-windows10.0.19041.0/DeskDisplay.App.exe。运行窗口仍为之前的Debug目录版本。

