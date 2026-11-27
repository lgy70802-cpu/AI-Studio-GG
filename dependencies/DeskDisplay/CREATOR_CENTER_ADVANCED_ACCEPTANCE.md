# 创作中心第四轮：交互、素材窗口与实机验收

日期：2026-10-20。对应本轮六项工作；在现有WPF/MVVM客户端内升级，没有替换应用或设备协议。

## 实体D92结果：部分通过，不能全部验收

本轮扩展现有 `tools/DeskDisplay.UsbDemo`，读取真实可编辑ThemeProject并由ThemeScene/SceneView/SceneJpegRenderer和生产D92HidRenderTarget输出。时钟使用系统时间，没有固定JPEG替代可编辑组件。

| 场景 | 实际传输结果 | 结论 |
| --- | --- | --- |
| 横屏组合主题，原保存的1920×462逻辑输出、90°旋转 | 60帧、1次连接、无等待/传输错误，正常停止释放 | 仅发送侧短时通过；画面、方向、走秒仍等待用户观察确认 |
| 竖屏组合主题，测试性交换为462×1920逻辑输出、保持90°旋转 | 9帧、9次连接，多次出现“The operation was canceled.”并重连 | 持续输出不通过，已停止；不能当作人工拔插重连通过 |
| 恢复原横屏输出参数与横屏主题 | 初始等待约4秒后25帧、1次连接、无传输错误，正常停止 | 已恢复已验证参数；无新设备设置写入 |

竖屏问题发生在上述具体宽高与旋转组合。它不能推导为所有竖屏方案均不支持，也不能把“首帧写入成功”或“诊断进程退出码0”解释为持续输出通过。下一步需要单独定位这个参数组合的持续刷新问题，本轮没有重新探索协议、增加未知命令或修改驱动/固件。

用户的视觉确认和物理拔插观察仍待补充。本轮日志中的竖屏重连由写入超时触发，并不是一次已确认的拔插恢复。长时间运行和Windows真实DPI也不因短时发送通过而自动通过。

证据：`artifacts/creator-device-landscape.jsonl`、`creator-device-portrait.jsonl`、`creator-device-restore.jsonl`、`creator-device-acceptance.json`；计划和可编辑源位于同目录。原USB设置仍由现有配置机制管理，诊断计划不覆盖用户配置。诊断程序的 `--self-test` 6项渲染检查通过，无硬件访问。

## 新功能

### 画布与组合

- 多选后右侧“组合 / 取消组合”，或Ctrl+G / Ctrl+Shift+G；GroupId随源项目保存。当前为平级组合，不支持嵌套组。
- 点击画布组合成员会选择整组；Ctrl/Shift切换整组选择。图层名称仍可用于单独选中成员编辑。
- 多选外框四角可以整体等比缩放，位置、宽高、字号和间距一起变化，成员旋转保持不变。一次拖动一次撤销；存在锁定成员时不启用整体缩放。
- 粘贴组合分配新的组ID，不会加入原组；跨布局复制单个元素清除原组关系。组旋转没有在本轮实现。
- 画布右键菜单复用复制、粘贴、组合、取消组合、上移、下移、删除命令。

### 素材窗口

- 编辑器左侧“浏览 / 替换素材”打开可调整大小的独立窗口。继续使用原资源库对象、ID、路径和缩略图服务，不建立第二套资产数据库。
- 名称/标签/分类搜索、横竖方向、来源、一级和二级分类联动。每页最多12项，两列卡片，横图4:1、竖图1:4，等比完整显示。
- 可添加到画布；仅单选未锁定图片/壁纸时启用“替换当前素材”。替换保留位置、大小、裁剪、透明度等样式，可撤销；视频仍受原输出能力限制。
- 未下载的云端资源继续先走在线资源及权益下载流程。

### 属性与首页

- RGB与透明度颜色选择器，带真实色块和十六进制结果；应用后更新图层，取消不修改。
- 字体选择下方显示当前字体文字样例；X/Y、宽高、旋转、字号和间距提供数值步进。
- 宽高比例锁定同时约束数值输入，超过尺寸范围时拒绝修改，避免破坏比例。
- 首页新增横竖屏项目筛选；编辑器“另存为”把当前编辑保存成新ID项目，保留原正式源文件，包括原文件尚未包含的当前修改。
- 原有未保存标记、保存反馈、草稿恢复和关闭保存机制继续保留。

### 性能

- 历史快照改为独立对象复制：两个布局、图层列表及图层对象均独立，不复制解码像素。后续增加可变引用类型字段时必须同步扩展Clone方法。
- 共享WPF渲染器增加最多512项的文字排版缓存，键包含内容、字体、字号、字重、颜色、DPI和文化设置；更改属性不会复用错误样式。
- 保留图片路径/长度/修改时间失效机制及128MiB图片缓存。缓存上限不等于整个活动项目的内存上限。
- 新增三张不同4096×1024 PNG后台加载、Dispatcher响应、切换项目后旧ImageScene可回收检查；仍保留100图层、100帧渲染、反复切页、撤销与计时器释放检查。
- 测试输出包含独立对象快照与JSON往返的本机时间对照。复杂100图层JPEG仍是约百毫秒级样本，不承诺高帧率USB或数小时稳定性。

## 验证与文件

- 完整DisplayTests：428项通过；Release构建0警告/0错误，配置SmokeTests通过。
- 最后的另存为语义、组合选择和控件样式调整后，再执行构建与创作专项；140项通过，最终结果见 `artifacts/creator-advanced-focused.log`。
- 构建、完整回归、配置日志：`artifacts/creator-advanced-build.log`、`creator-advanced-regression.log`、`creator-advanced-smoke.log`。
- 新截图在DisplayTests Release目录：`creator-group-editor.png`、`creator-material-browser.png`、`creator-color-picker.png`，另有原紧凑/横竖编辑截图。测试素材全部在临时隔离目录。
- 主要新增文件：`CreatorAdvanced.cs`、`CreatorMaterialBrowser.xaml/.cs`、`CreatorColorPicker.cs`、`NumericStepper.cs`、`CreatorAdvancedTests.cs`。
- 主要修改：`ThemeProject.cs`、`ThemeGeometry.cs`、`CreatorEditingTools.cs`、`CreatorExperience.cs`、`CreatorViewModel.cs`、`CreatorView.xaml/.cs`、`ThemeCanvas.cs`、`SceneView.Theme.cs`、`CreatorWorkflowTests.cs`、诊断工具Program.cs。

macOS客户端没有在此Windows仓库实现或执行。本轮不更改HID编码、设备驱动与固件，也不增加USB视频能力。

运行包：`artifacts/releases/D92Studio-0.1.0-preview.20261020.4-win-x64.zip`。

## 2026-10-21 后续修正

本节历史竖屏失败没有删除。后续保持编码尺寸的方向映射、交互增强和30分钟实测见 [稳定性验收](CREATOR_CENTER_STABILITY_ACCEPTANCE.md)。
