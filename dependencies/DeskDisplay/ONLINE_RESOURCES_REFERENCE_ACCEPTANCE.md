# 在线资源参考图布局改造验收 — 2026-10-21

版本：0.1.0-preview.20261021.11。

## 本轮对应需求

1. 左侧在线资源图标点击弹出二级菜单：壁纸、时钟、插件、主题。键盘右键/Enter/Space可展开，Escape关闭；选中项高亮，选择后收起。沿用68px主导航和已有页面实例。
2. 搜索移入窗口顶部，仅在线资源页显示；左右分别是实际资源“上一页/下一页”命令，保留忙碌与页码边界。Enter或搜索图标提交；从详情搜索时回到列表。这里是资源分页，不是浏览历史前进后退。
3. 分类区压缩：一级分类下紧跟其二级分类，无子分类时隐藏对应行；右上角显示方向、排序和布局下拉。标签、连接、刷新、当前页更新收入“⋯”工具菜单。下载任务仍可在页面底部展开管理。
4. 布局模式：横图默认舒适2列、紧凑3列；竖图舒适6列、紧凑8列。缩略图容器保持4:1或1:4，原图Uniform完整等比显示；圆角用真实WPF裁切处理，没有把参考图作为背景。
5. 缩略图标题移至图片左上角，权益角标在右上，分辨率与下载状态放图下。保留资源详情、收藏、下载校验/取消/重试及版本更新。

## 真实能力边界

- 当前服务仅开放静态壁纸分发。时钟/插件/主题菜单可选择，但显示明确“尚未开放”页面并禁用搜索/下载，不使用壁纸数据冒充这些资源。
- 动态云端壁纸与热度排序没有新接口，本轮不伪造可用能力。
- 保留当前Soft UI蓝色设计、原有主窗口拖动和系统按钮；没有复制参考图里的AI广告、促销弹窗或营销入口。
- 新布局模式和资源类型是会话状态；没有覆盖用户配置或改写现有资产数据库。
- 本轮不发送实体HID命令，不修改Core/Display/设备协议、驱动或固件。此前实体验收结果仍参见D92_HARDWARE_ACCEPTANCE_20261021.md；不把UI回归当作新的实机验收。

## 实际修改文件

- src/DeskDisplay.App/ViewModels/CloudLibraryViewModel.cs：强类型OnlineResourceKind、资源菜单元数据、未开放状态与命令保护、布局列数及视口重算、详情搜索返回列表。
- src/DeskDisplay.App/Views/MainWindow.xaml / .xaml.cs：顶部搜索及分页、侧栏菜单弹出、键盘入口和选中高亮。
- src/DeskDisplay.App/Views/OnlineResourcesView.xaml / .xaml.cs：紧凑分类、Soft UI真实ComboBox模板、右上排序布局、工具弹出、卡片与下载区域。
- src/DeskDisplay.App/Views/RoundedPreviewClip.cs：按实际控件尺寸更新圆角剪裁，保留原图缩放方式。
- tests/DeskDisplay.DisplayTests/StudioApiTests.cs：真实控件绑定/布局/下拉选项/二级菜单/未开放页面/详情状态/顶部搜索/最小窗口检查。

仓库另有此前未提交工作，本清单不是整个git工作区变更的归属声明。本轮没有修改后端代码。

## 测试

- 改造前Release构建通过；artifacts/online-reference-baseline-build.log。
- 最终Release构建0警告、0错误；artifacts/online-reference-final-build.log。
- 完整DisplayTests643项PASS、退出0；artifacts/online-reference-regression.log。包含既有资源库、创作、输出替身、场景与真实本地API检查。
- 完整回归后的收尾只增加详情搜索回列表、关闭后的属性保护和下拉文字对比度；最终Studio专项49项PASS、退出0；artifacts/online-reference-tests.log。
- 配置SmokeTests通过；artifacts/online-reference-smoke.log。
- 已检查1320×900默认/紧凑、1100×740最小窗口、竖屏模式、侧栏弹出及未开放页面的实际WPF截图。真实125%/150%多屏DPI与触控仍未新增人工验收。
- 图片内容来自本机API已明确标记的验收资源，不是新增假商城数据。

## 截图

目录：tests/DeskDisplay.DisplayTests/bin/Release/net8.0-windows10.0.19041.0/

- online-reference-default.png：默认横屏两列。
- online-reference-compact.png：横屏三列。
- online-reference-sidebar-menu.png：四项二级弹出菜单（独立Popup窗口，因此单独截图）。
- online-reference-unavailable.png：未开放资源类型。
- online-reference-portrait.png：竖屏紧凑八列，素材容器1:4。
- online-reference-minimum.png：1100×740窗口。

## 发布

artifacts/releases/D92Studio-0.1.0-preview.20261021.11-win-x64.zip

SHA256：5BFA4138EE493765DEC9CB35702846713707591C0830D8DAC7AA78B395F78B2D。
