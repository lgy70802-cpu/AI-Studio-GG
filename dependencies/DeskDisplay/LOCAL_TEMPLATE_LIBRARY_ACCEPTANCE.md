# 本地可编辑模板库 V1

日期：2026-10-23。分支：feature/multi-device-foundation。

## 入口与内容

创作中心 → **从模板创作** → 选择封面 → 预览 → **使用此模板**。

内置6类、横竖各一项，共12项：

| 类别 | 编辑内容 | 本轮状态 |
|---|---|---|
| 极简数字时钟 | 时间、日期、星期 | 保留并补真实封面 |
| 壁纸时钟 | 自选静态背景、时钟、日期、底板 | 新增 |
| 工作信息卡 | 时钟、日期、提醒文字、分隔形状 | 新增 |
| 复古相机时钟 | 现有CameraClock插件、日期；竖屏组合独立数字时间 | 新增，要求插件已启用 |
| 极简文字 | 文字、强调形状 | 保留 |
| 图片与标题 | 待指定图片槽位、标题、底板 | 保留，明确需要选择图片 |

每次创建都是新项目，保留可编辑ThemeLayer与独立横竖布局。封面由真实ThemeScene/SceneView渲染为冻结小图；封面是快照，大预览继续读取系统时间。没有把效果图当背景，没有用合成JPEG替代可编辑时钟，也没有新增资源资产数据库。

## 背景与插件

- 壁纸时钟可以从现有资源库的静态资源选择背景，或选择本地PNG/JPEG文件。未选择时为明确的纯色背景，可正常创作。
- 本地文件保留真实路径；资源库素材复用AssetId、Path、云端资源标识与来源字段。横竖布局各自持有背景图层引用，使用Uniform完整显示。这里只读取本地已有资源，不请求云端商城或伪造权益。
- 背景异步加载；加载期间/失败时禁用创建并显示真实原因。更换/清除会取消过期加载，关闭编辑器释放订阅和预览引用。
- 相机模板只复用已安装且启用的现有复古相机组件，不自动修改插件设置。停用时给出管理入口并禁用创建；启用后实际CameraScene参与模板预览，UsesScene已包含模板预览以保持走秒/既有动画调度。没有重新开发翻页算法。
- 原相机组件为超宽画面，竖屏模板按其原生比例等比容纳，再独立组合数字时间与日期，不旋转/拉伸原组件，不宣称已有新的竖屏拟物面板。
- 用户自定义模板的保存、管理和存储沿用原服务，不替换或删除原模板。

## 验证

- 完整Windows回归774项通过：artifacts/template-library-regression.log。
- Core适配/会话95项、配置冒烟19项通过：template-library-core.log、template-library-smoke.log。
- 封面复用缓存、依赖不可用占位区等收尾后，模板专项38项再次通过：template-library-tests.log；最终构建0警告、0错误：template-library-build.log。774与38是分次运行，不相加。
- 设备页逻辑表面专项27项通过：template-library-device-page.log；git diff --check通过。

测试使用临时项目、实际PNG和隔离设置，未覆盖用户主题、插件配置或资源库。封面按编辑器实例复用，插件启用状态改变才重建，避免每次进页面重复渲染；大预览仍实时走秒。

新增专项逐一检查全部模板的新项目/图层身份和横竖边界；验证真实封面、实时数字时钟、背景加载与正式保存、创建副本独立、缺图拦截、插件启停与模板内走秒、自定义模板保留。旧“6项”数量断言改为检查每种模板都有横竖版本及明确依赖元数据，保留旧图片槽位缺失检查。

真实WPF截图：template-library-clock.png、template-library-wallpaper.png、template-library-camera-disabled.png、template-library-camera-portrait.png。截图来源为隔离测试窗口，图中的纯色小PNG是实际测试素材，不是用户壁纸。申请横屏窗口1320×900、相机竖屏窗口1100×740；本机远程桌面会限制原生窗口大小，实际图像尺寸以PNG为准，不冒充真实显示器多尺寸或DPI验收。

完整回归曾在既有设备页截图尺寸断言处失败：原生桌面将窗口压小，生成的控件截图只有1028×749。该测试已改为明确设置1100×740/1320×900逻辑控件表面，保持原控件与内容断言，专门验证离屏布局而不依赖桌面工作区；设备页专项重新通过。没有调整系统DPI或放宽应用能力声明。

## 文件

- src/DeskDisplay.Core/Models/ThemeTemplates.cs：模板元数据、三类新增组合与独立横竖几何；相机比例读取原组件。
- src/DeskDisplay.App/ViewModels/CreatorTemplateLibrary.cs：真实封面、背景与资产引用、异步取消、插件依赖与独立创建。
- src/DeskDisplay.App/ViewModels/CreatorProjectManagement.cs、CreatorViewModel.cs：原入口、创建命令、模板走秒与生命周期接入。
- src/DeskDisplay.App/Views/CreatorView.xaml、CreatorView.xaml.cs：封面选择、依赖状态、背景选择与选中项定位。
- tests/DeskDisplay.DisplayTests/TemplateLibraryTests.cs、CreatorManagementTests.cs、Program.cs：新专项及原回归整合。
- tests/DeskDisplay.DisplayTests/ManagedDevicePageTests.cs：明确逻辑布局尺寸，消除远程桌面限制对既有尺寸检查的干扰。

## 边界

本轮仅本地模板，不做云端模板发布、商城、支付、在线插件执行或AI转换。未发送真实HID命令、修改设备协议/驱动/固件；新USB路径仍不支持视频。没有本轮实体相机模板、高帧率或macOS认证。未发布新包、提交或推送代码；运行中的用户软件保持原版本，没有强制重启。使用新模板需要之后正常关闭旧窗口并启动新构建。
