# 创作中心新增素材与图层置顶（2026-10-24）

本轮按用户最新指令仅完成两项排序，不修改画布默认尺寸或已有主题尺寸。

## 行为

- 素材列表按 AddedAt 倒序；相同时间按后加入优先，批量导入最后加入的素材最上。继续使用现有资源库、搜索和分类过滤，不复制资产数据。
- 新导入且符合当前筛选的素材会通知编辑器，将素材列表滚至顶部；普通元数据刷新不重复触发。
- 从素材列表添加的图片/壁纸、普通组件、直接插入图片统一使用最大ZIndex+1，图层列表顶部与实际最上层一致，继续自动选中与滚入可见区域。复制到另一布局的图片也置于目标布局顶层。
- 专用“设置背景”操作仍遵循背景语义：替换原背景，或创建底层背景；模板背景规则不变。本轮修改的是普通新增图层，不按图片类型强制沉底。
- 原有项目不重排；新顺序支持撤销、重做及保存恢复。没有调整默认画布、HID协议、设备设置或素材原文件。

## 文件

- src/DeskDisplay.App/ViewModels/CreatorExperience.cs：素材倒序与稳定同时间排序、导入通知、跨布局复制置顶。
- src/DeskDisplay.App/ViewModels/CreatorViewModel.cs：普通插入置顶，显式背景参数。
- src/DeskDisplay.App/ViewModels/CreatorImageInsertion.cs：专用背景路径显式传参。
- src/DeskDisplay.App/Views/CreatorView.xaml及代码后台：MaterialList与导入后滚动。
- tests/DeskDisplay.DisplayTests/CreatorOrderingTests.cs（新增14项）、CreatorTests.cs、Program.cs。

## 验证

- 构建0警告0错误：artifacts/ordering-build.log。
- 新增专项14项通过：artifacts/ordering-tests.log。真实临时PNG导入、批量顺序、同时间稳定排序、筛选、导入通知、图片/壁纸/形状置顶、实际JPEG合成最上层、撤销重做、跨布局复制、保存恢复、画布尺寸不变。
- 完整Windows904项通过，退出码0：artifacts/ordering-regression.log。首次回归中原断言要求新增壁纸沉底，已按用户新要求改为置顶；保留真实素材ID与加载成功检查，没有放宽其他断言。
- 实体设备未新增输出测试；没有把模型测试当作人工滚动/点击验收。

## 安装

已安装并打开0.1.0-preview.20261024.6。控件树确认版本与D92已连接，进程15812 Responding=True，安装前后settings SHA相同，旧app备份保留，见artifacts/ordering-installed.json。

更新前旧窗口控件树读取为空、截图FrameArrived超时，未盲点保存或强杀。用户确认“已保存并关闭”后检查进程退出，完成安装并重新打开；新窗口控件树正常。

ZIP：artifacts/releases/D92Studio-0.1.0-preview.20261024.6-win-x64.zip。

SHA256：AD3AF2874EEE7737676A5CA4EC4900A8788ABB4C1418E7DEC86B009D410857A4。

未提交或推送，原有工作区变更保留。
