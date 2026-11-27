# 按设备生成背景铺满布局（2026-10-24）

## 使用

创作中心打开主题进入编辑 → 预览与应用 → 选择已配置的目标设备 → 生成铺满布局 → 返回编辑器检查 → 保存 → 再预览并应用。

目标尺寸来自该设备实际配置，不固定成某一个 D92 尺寸。当前 D92 配置1920×462会生成这个尺寸的新布局，而原1920×480横屏及其他横竖布局保留。生成只是编辑操作，不自动保存文件或向设备发送；可继续微调、撤销或放弃。资源库快速使用的只读预览没有编辑回调，生成按钮保持禁用，应先点编辑进入创作中心。

## 规则

- 复用 EditableLayoutAdapter 的独立布局捕获与 CreatorViewModel.AddEditableLayout，不修改原布局。
- 先按选定的等比适配方式捕获前景位置、尺寸、字号、字间距，再单独处理背景。
- 自动背景范围严格限制为：可见、未旋转、静态 Image/Wallpaper，原边界覆盖整个源画布；在合格图片中选择绘制顺序最底层。小图、时钟、文字、视频不会被当成背景。
- 背景边界改为目标画布完整大小，Fill改为等比裁切铺满。保留素材ID、路径、裁剪中心、缩放、透明度等其他参数；不使用非等比拉伸。
- 没有合格背景时明确提示，不擅自放大前景图。透明PNG、原图本身自带黑边、其他前景遮挡等问题不在此操作中自动消除。
- 保存后的新布局按目标真实比例精确匹配，沿用现有输出链路与权限、依赖、能力检查。不改变HID协议、设备配置、驱动或固件。

当前用户源文件中的4:1画布与1920×462输出本身约造成左右各36像素留边；3:1图片若在全画布内完整显示会产生更宽留边。具体图层还可能经过用户移动/放大，因此这不是对每个现有主题黑边宽度的通用断言。本轮没有覆盖用户主题文件来强制修正。

## 代码

- src/DeskDisplay.Core/Themes/EditableLayoutAdapter.cs：CaptureWithFilledBackground。
- src/DeskDisplay.App/ViewModels/CreatorDevicePreviewViewModel.cs：CreateEditableLayout(fillBackground)。
- src/DeskDisplay.App/Views/CreatorDevicePreview.xaml及代码后台：真实“生成铺满布局”按钮、提示与编辑回调。
- tests/DeskDisplay.DisplayTests/BackgroundAdaptationTests.cs（新）及Program.cs。

源项目和包格式版本不变，仍是已有V2多布局结构，无需新增素材数据库或强制迁移旧项目。

## 验证

- 构建0警告0错误：artifacts/background-build.log。
- 新增专项15项通过：artifacts/background-tests.log。
- 完整Windows890项通过、退出码0：artifacts/background-regression.log。
- Core适配/会话95项通过：artifacts/background-adaptation-core.log，虚拟目标。
- 专项覆盖背景全尺寸、引用/ID保留、时钟等比与位置、源项目不变、保存恢复、实际比例选择、不同分辨率、独立竖屏、缺失背景拒绝、真实WPF按钮和编辑回调。
- 使用真实Scene/WindowsThemeSessionFactory/SceneJpegRenderer对3:1背景输出1920×462 JPEG进行左右边像素检查：完整显示有深色边，生成后边缘恢复图片颜色。隔离测试图见artifacts/background-adaptation-before.jpg与background-adaptation-after.jpg，已检查；这些不是用户主题实机截图。

未进行本次新布局的实体D92显示验收，没有自动修改当前设备画面；原有实机结论不扩大。

## 交付

已安装打开0.1.0-preview.20261024.5；控件树核对版本、D92已连接，进程5296 Responding=True。安装前后settings SHA相同，原app备份保留，见artifacts/background-installed.json。更新前发现用户的保存提示，重新读取时窗口已由用户关闭，助手未点击保存或丢弃；没有强杀进程或修改用户项目文件。

ZIP：artifacts/releases/D92Studio-0.1.0-preview.20261024.5-win-x64.zip。

SHA256：A8D8684059599C6324201062FFFF97ECA3E976596A30A805ADB489598DBA2EBF。

未提交/推送Git，原有工作区变更保留。
