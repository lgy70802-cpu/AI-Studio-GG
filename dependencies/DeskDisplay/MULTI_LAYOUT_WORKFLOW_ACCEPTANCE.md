# 多布局创作与设备关联验收

日期：2026-10-22。分支：feature/multi-device-foundation。

## 已交付的功能与入口

### 1. 多布局保存与管理

创作中心编辑器左侧增加布局选择、名称、实际设计尺寸、复制和删除。保留默认横屏/竖屏；同一主题可额外保存最多62个自定义布局。自定义布局有独立ID、名称、修订号和来源布局ID，图层仍是可编辑组件，不是合成JPEG。删除可撤销；重新打开恢复选择。编辑历史深拷贝所有布局，命名布局的缩放/平移/选择状态按布局ID分别保存。

旧Version=1项目只读时不迁移、不改原文件；新增自定义布局后使用Version=2编辑源格式，正常保存仍由现有ThemeProjectStore校验、原子替换和备份。旧客户端不保证能读V2，请保留源文件历史。编辑源ThemeProject与输出用的不可变ThemeDocument是不同契约，不应混用两种JSON。

### 2. 适配结果进入编辑器微调

创作中心 → 打开主题 → 预览 → 按设备预览与应用 → 选择设备/完整显示或等比裁切 → **保存为布局并微调**。

该按钮将当前已准备成功的适配结果转换为新的设计坐标和可编辑图层，缩放字体/间距并保留真实时钟、素材、插件配置。原布局不变；返回编辑器后可拖动、缩放、修改属性、撤销和正常保存。按钮创建编辑版本，仍需点击主编辑器“保存”写入磁盘；直接切换预览设备不会修改源主题。若目标尺寸导致参数超过既有校验上限，会报告错误而不插入损坏布局。

所有布局接入现有素材引用、导出检查、打包/导入、缺失检查及重新定位链路，没有新建资产数据库。

### 3. 用户明确关联物理端点

设备管理卡片增加“物理屏关联组”。确认属于同一块屏的USB与系统显示端点填写相同组名，点击原有保存设置，然后确认参数。组名去空白、锁键不区分大小写。保存沿用独立设备偏好文件，并保留其他已存设备。旧偏好缺少此字段按空组读取。

新管理会话同时持有端点租约与物理组租约。另一端点竞争同组时返回PhysicalDeviceBusy，页面逐台显示失败；其他设备可继续输出。停止/失败清理释放组锁。关联修改未保存不能应用；任一会话运行期间不允许改变组，普通名称保存保持原有能力。稳定设备ID仍由现有发现逻辑处理，关联组不替代序列号，也不自动确认歧义设备。

**适用边界：**关联是人工声明，不按分辨率或名称自动猜测。该组保护接入新的管理会话；旧单设备入口和不协作的其他软件没有自动读取此组，仍需只选择一种输出方式。不能宣称所有软件/所有物理冲突已经解决。未知、缺号及重复序列号沿用既有保守限制，不重新设计HID协议。

### 4. 双设备实机验收状态

只读运行现有UsbDemo --list，当前发现1台D92，ReportLength=1025，记录在artifacts/multi-device-inventory-20261022.json。本轮没有发送图像、初始化、心跳或固件命令。因此真实双D92验收**尚未完成**。

离线测试使用独立虚拟驱动，验证相同物理组互斥、其他设备并行及停止后组锁释放；已有完整回归继续覆盖独立会话、断开恢复和冲突规则。它们不等于双设备HID、持续运行、吞吐、多DPI或macOS实测。

两台设备到位后按顺序验收：

1. 核对两个不同稳定序列号及各自尺寸/旋转，确认端点没有指向同一块屏。
2. 分别应用不同的可编辑实时时钟主题，记录每台会话ID、首帧与运行状态；目视确认方向和走秒。
3. 两台应用相同主题，验证不同布局及各自实际像素。
4. 拔下一台约8秒再接回，记录对应端点消失/恢复；另一台持续走秒。
5. 单独停止/恢复其中一台，验证另一台不受影响；验证同屏关联端点无法重复启动。
6. 持续运行并记录进程内存、输出帧率、重连和错误，明确实际时长；测试结束释放所有会话。

### 5. 预览界面

设备与适配选择改用轻量圆角下拉控件；底部给出离线、需要检查、待确认、目标正在输出等真实状态。新增保存适配布局入口，保留独立设备结果；选择器显示实际设备名称/尺寸或布局名称，避免暴露对象类型名。

## 验证记录

- 解决方案最终构建：0警告、0错误，artifacts/layout-upgrade-build.log。
- 完整Windows回归707项通过，artifacts/layout-upgrade-regression.log；Core适配/会话95项、配置冒烟19项通过，分别见layout-upgrade-core.log、layout-upgrade-smoke.log。
- 收尾完善选择器模板、创建前校验、同方向布局撤销后的视图恢复和状态文案后，相关专项40项再次通过，artifacts/layout-upgrade-targeted.log。707是此前一次完整运行，40是最后一次专项运行，不将两者相加。
- 已检查真实WPF截图：artifacts/creator-layout-editor-1100.png、creator-layout-editor-1320.png、creator-layout-preview.png。分别覆盖1100×740、1320×900编辑器及1160×780预览。设备使用离线替身，测试文字故意叠在时钟上以证明独立图层可编辑，未声称这些截图来自双D92。
- git diff --check通过；本轮未进行真实Windows多DPI验证。

回归中曾因保存限制过宽阻止已有“输出期间保存名称”行为，现仅阻止运行中修改关联组，最终完整回归已通过。另一次构建与正在运行的测试争用DLL，等待测试结束后按顺序重建成功，不是用户应用文件损坏。截图发现的选择器对象类型名显示已通过补齐WPF模板选择器修正并重新截图。

测试均隔离使用临时项目和设备偏好，不覆盖用户配置。

## 主要修改文件

- Core/Models/ThemeProject.cs、ThemeScene.cs：编辑源V2、布局深拷贝/校验及实际画布大小。
- Core/Themes/LegacyThemeAdapter.cs、EditableLayoutAdapter.cs：全部布局转换及可编辑适配结果。
- Core/Output/OutputSessionManager.cs：组租约、冲突码与释放。
- App/ViewModels/CreatorLayoutVersions.cs、CreatorViewModel.cs、CreatorNavigationTools.cs、CreatorProjectManagement.cs、CreatorFolderRepair.cs：布局管理、历史、视图状态和素材修复。
- App/ViewModels/CreatorDevicePreviewViewModel.cs、ManagedDevicesViewModel.cs：适配回调、真实状态及关联保存/应用。
- App/Views/CreatorView.xaml、CreatorDevicePreview.xaml及代码、CreatorDevicePreviewEntry.cs、ManagedDevicePanel.xaml；App/Themes/Controls.xaml：真实WPF入口与样式。
- Library/ThemeEditorViewport.cs、ThemeTransferService.cs、ThemeExportInspection.cs；Infrastructure/Configuration/DevicePreferenceStore.cs：全部布局资产、视图与设备偏好。
- tests/DeskDisplay.DisplayTests/MultiLayoutTests.cs、CreatorDevicePreviewTests.cs、Program.cs：存储、几何、虚拟冲突、真实主窗口工作流与截图验证。

Core/App/Library/Infrastructure路径分别位于src/DeskDisplay.Core、src/DeskDisplay.App、src/DeskDisplay.Library、src/DeskDisplay.Infrastructure。

本轮没有发布、重启用户应用、提交或推送；没有修改HID报文、驱动或固件。macOS UI和原生输出行为仍需在Mac仓库单独接入验收。
