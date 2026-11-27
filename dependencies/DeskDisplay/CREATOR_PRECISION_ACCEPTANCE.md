# 创作中心精确编辑与应用前检查（2026-10-23）

## 六项交付

1. **实际操作检查**：用真实WPF主窗口、场景、PNG文件和输出渲染检查文字、图片、图层、菜单与保存；原生客户端成功启动。远程鼠标点击依然报`coordinate input geometry is unavailable`，部分键盘导航可用，因此不把离屏测试当成鼠标拖拽、中文输入法或跨显示器DPI验收。
2. **多行文字**：普通文字按独立行使用实际字体渲染，支持顶部/居中/底部、自动增高、适应文字高度、宽高溢出提示。文字不非等比拉伸；自动增高和文字变更同一次撤销。画布输入框增加滚动，长行不会伪装为已在输出中自动换行。新字段可选，旧JSON仍默认居中和固定高度。
3. **图片裁剪**：复用真实图片拖动裁剪；重置裁剪恢复完整等比显示，可撤销。素材选择窗口增加“替换时保留裁剪”，默认保留，取消勾选后重置缩放与位置。裁剪窗口中先“恢复原比例”再“重置裁剪”会撤销原比例请求，避免意外改尺寸。原图字节保持不变。
4. **对齐辅助**：保留已有设计坐标中心/边缘吸附、间距和对齐线；浮动条增加多选快捷对齐与水平/垂直等距菜单，原右侧完整操作仍可用。
5. **图层**：在已有菜单上合并重命名、创建副本、选择组内下一层、锁定/解锁、显示/隐藏、组合与排序，未删除原裁剪、轮播、置顶置底等入口。组内选中层可独立移动，组ID与其他成员保持不变；锁定状态来自真实图层。
6. **应用前检查**：中央增加应用前检查入口。真实素材缺失/未加载、禁用插件为阻止项；文字溢出、画布外元素为警告。每项携带图层ID，可定位并展开其分组。当前单设备配置检查复用原能力判断；多设备仍在按设备预览中逐台检查，未放宽USB视频或未知设备限制。

## 文件

- Core：`Models/ThemeProject.cs`新增可选文字配置及枚举校验。
- Display：`Views/ThemeTextMetrics.cs`与`SceneView.Theme.cs`，共享字体度量、分行绘制及有界度量缓存。
- App ViewModels：`CreatorPrecision.cs`、`CreatorPreflight.cs`、`CreatorViewModel.cs`、`CreatorAdvanced.cs`。
- App Views：`CreatorView.xaml`/`.cs`、`ThemeCanvas.cs`、`CreatorCropEditor.xaml.cs`、`CreatorMaterialBrowser.xaml`/`.cs`、`CreatorPreflightDialog.cs`。
- Tests：`CreatorPrecisionTests.cs`、`CreatorDirectEditingTests.cs`、`Program.cs`。

## 验证

- 完整Windows回归828项通过，退出码0。收尾输入滚动和命令状态通知后，精确编辑专项25项、直接编辑专项19项再次通过；分次结果不相加。
- 最终解决方案构建0警告、0错误；Core适配/会话95项、配置19项通过，`git diff --check`通过。
- 新专项用实际像素验证多行垂直位置，真实PNG验证裁剪、替换及原文件不变，真实WPF检查菜单、警告、禁用继续按钮和定位。
- 测试仅操作临时项目、库与配置。未发送诊断HID、修改协议/驱动/固件、提交或推送。旧主题不主动迁移/覆盖；新增配置正常保存后落盘。

日志：`artifacts/creator-precision-*`。独立新版启动文件：`artifacts/precision-build/Debug/net8.0-windows10.0.19041.0/DeskDisplay.App.exe`。

## 运行窗口

发现旧窗口中有未保存主题；正常关闭出现保存对话框后，读取实际焦点，经Tab逐项确认焦点落在“保存”，回车后确认旧窗口正常退出。未选择放弃或强制结束。随后打开precision-build新版，真实控件树显示D92已连接。原生鼠标坐标仍不可用，中文输入法和真实拖拽手感待人工验收。
