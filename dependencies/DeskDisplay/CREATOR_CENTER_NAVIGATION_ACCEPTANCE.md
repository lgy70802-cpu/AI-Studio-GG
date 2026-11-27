# 创作中心画布导航与状态恢复验收（2026-10-21）

本轮按第1、2、4、5项实施；没有扩展图片裁剪编辑或设备应用确认页。

## 画布外元素找回

入口：编辑器底部“项目工具 → 定位画布外元素 / 将选中元素移回画布”，画布和图层右键菜单也提供移回操作。

定位循环选中部分或全部超出画布的图层，展开所属组合、同步图层列表并将视口滚动到可达位置。完全位于画布外的内容仍受输出画布裁剪，应使用“移回画布”使其可见；定位不擅自改变坐标、解锁或显示隐藏内容。

移回保留原尺寸、旋转和组合成员相对位置；大于画布的对象按外接范围居中，不自动缩小。组合包含锁定成员时不整体移动。修改进入同一撤销步骤。

## 置顶与置底

入口：画布或图层列表右键 → 置顶/置底。快捷键Ctrl+Home/Ctrl+End（仅编辑器非文字输入区域）。右键未选中的图层时先同步选中。组合按一个块移动，组内顺序保持，多个选中对象保留相对次序；锁定成员阻止该组合重排。操作可撤销，原上下移动和拖动排序保留。

## 滚轮缩放与空格平移

在中央画布区域滚动鼠标滚轮，围绕鼠标所在位置缩放；缩放仍限制25%—300%。滚动范围允许时保持鼠标下设计坐标位置不变，视口边界处受ScrollViewer合法滚动范围限制。素材列表和属性面板滚轮行为保持原用途。

按住空格再按左键拖动画布，或沿用中键拖动。平移只改变视口，不改变图层位置或撤销历史；释放鼠标、失去捕获、窗口失去激活时正确结束。输入框中空格继续用于输入。四角“默认等比、Shift自由缩放、Alt绕过吸附”不变。

## 编辑视图恢复

按项目ID和横竖布局分别保存缩放、归一化滚动位置、选中图层ID及主选中图层。重新打开主题或新建编辑器实例时恢复；单独选中的组合成员不会被误扩展为整组选中，已删除的图层ID被忽略。切换布局及撤销/重做方向切换时恢复相应视图。

状态位于Themes/EditorState/<项目ID>-<方向>.json，独立于.theme.json、草稿、模板、历史版本和原设置。状态变化合并250ms后原子保存，相同状态不重复写入；正常关闭会等待最新一项写入。异常退出可能丢失最后一个合并周期的视图变化，不影响正式源。坏状态读取时跳过并保留，后续正常用户视图操作可能重写该偏好文件。视图状态变化不标记源项目未保存，不增加撤销记录。

## 验证证据

- Release构建0警告/错误；配置SmokeTests通过。
- 新增CreatorNavigationTests覆盖：画布外组合找回/一次撤销、组置顶置底/锁定、状态磁盘持久化、横竖独立、精确组合成员选中、新实例恢复、损坏/非法状态、失效ID、真实WPF缩放锚点、空格左键捕获和关闭写入。
- 修复测试发现的关闭保存时序：Flush等待当前最新任务，避免窗口卸载时新的状态使先前等待任务失效。
- 更新旧右键菜单检查，改为校验真实命令绑定而非固定7个菜单项；增加功能后不以旧固定数量作为功能失败。
- 日志：artifacts/creator-navigation-final-build.log、creator-navigation-tests.log、creator-navigation-regression.log、creator-navigation-smoke.log、creator-navigation-publish.log。
- 实际WPF窗口截图：tests/DeskDisplay.DisplayTests/bin/Release/net8.0-windows10.0.19041.0/creator-navigation-state.png，已查看。测试使用独立临时主题目录和素材，不写真实用户主题库。
- 当前Windows测试环境的实际系统DPI仍为96；本轮不宣称真实不同DPI多屏体验或长时间鼠标操作已验收。

## 主要文件

新增：ThemeEditorViewport.cs、CreatorNavigationTools.cs、CreatorViewport.cs、CreatorNavigationTests.cs。

修改：CreatorViewModel.cs、CreatorView.xaml/.cs、ThemeCanvas.cs、CreatorProductivityTests.cs、CreatorAdvancedTests.cs。保留Core场景、HID报文、设备设置、视频USB限制、插件和资源库原有实现。本轮没有发送真实HID命令。

运行包：0.1.0-preview.20261021.8。最终完整回归562项通过（退出0），Release0警告/错误，配置SmokeTests通过。
