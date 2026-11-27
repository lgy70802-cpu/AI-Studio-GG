# 小屏宝PixBuddy 组件与插件开发

组件开发分支为 `codex/pixbuddy-components-20261127`，从 0.2.13 提交 `e9a9f25` 建立。新版前端目录为 `d-studio`。Google AI Studio 导入仓库时选择此分支，运行目录使用 `d-studio`。

## 运行示例

```powershell
npm --prefix d-studio ci
npm --prefix d-studio run dev
```

打开 `http://localhost:3000/?component-preview`。根目录安装依赖后，可在开发服务器运行期间执行 `node tools-component-check.cjs`，使用本机 Microsoft Edge 检查尺寸、数字稳定性、配置恢复和减少动态效果。开发预览提供四种设备尺寸、模拟时间、实时时间、暂停、秒数、12/24小时制、颜色、大小及配置保存。该入口仅在 Vite 开发环境启用。

翻页时钟的显示组件位于 `d-studio/src/components/widgets/FlipClock.tsx`，样式位于同目录的 `FlipClock.css`。开发预览和配置示例位于 `d-studio/src/dev/ComponentPreview.tsx`。复制这套文件结构即可开始下一个组件。

## 接口与生命周期

显示组件通过 props 接收时间、配置、方向和动画开关。定时器由宿主维护，组件不另起时钟，保证编辑器、暂停画面、缩略图和后台设备渲染使用同一时间源。新增定时器、监听器和请求时在卸载或关闭实时模式时清理。异步数据请求支持取消和过期结果丢弃。

尺寸由父容器决定，图层位置、旋转、透明度和缩放由编辑器管理。组件内部使用 em、百分比或容器单位。数字采用固定卡片宽度与等宽数字，切换 11、20、59、00 时整体边界保持稳定。动效遵循系统减少动态效果设置。

翻页时钟 props 包含 `hours`、`minutes`、`seconds`、`showSeconds`、`period`、`portrait` 和 `animate`。时间字符串由宿主处理时区和12/24小时制，`period` 可传 AM 或 PM。组件自身没有网络依赖。

## 编辑器接入

1. 在 `src/types/editor.ts` 沿用或扩展元素类型。现有时钟使用 `kind='clock'`、`style='flip'`，配置保存在 `sec`、`h24`、`tz`、`size`、`color` 等字段。
2. 在 `src/data/editorCatalog.ts` 的 `makeElement` 提供默认值，加入素材或模板目录。当前可从编辑器的时间区域添加“机械翻页钟”，也可选择“翻页时钟 · 星夜”模板。
3. 在 `EditorArtworkRenderer.tsx` 通过元素参数调用显示组件。首页、编辑器和 `DeviceRenderer` 共用这条渲染链，后台 WebView2 使用同一份前端资源。
4. 在编辑器属性面板提供配置。已有时钟配置面板支持秒数、制式、时区、颜色和大小，图层继续支持拖动、缩放、撤销和重做。
5. 如果增加新的元素类型，同时更新 `ThemeEditorV2.tsx` 的 JSON 导入校验和草稿校验，确保旧工程可读、新工程能保存并再次打开。

保留源主题的资源 ID 和编辑会话身份。保存期间的新修改继续保留，配置缺失时应用默认值。删除配置字段前检查旧版本工程的使用方式。

## 插件接入

插件配置定义在 `src/data/themes.ts` 的 `PluginConfig`，注册列表为 `INITIAL_PLUGINS`，显示入口为 `PluginWidgets.tsx`，参数界面为 `PluginCenterView.tsx`。新增配置使用可选字段并提供默认值。

本示例为现有时钟插件增加可选 `clockStyle`，取值 digital 或 flip。旧配置缺失该字段时使用数字时钟；插件配置面板可以切换翻页样式，复用同一显示组件。插件中心目前仍包含原型演示，真实系统数据与插件设备输出链路需要逐项接入和验证。插件配置当前属于会话内演示，重启恢复与真实设备插件叠加仍需接入。设备输出验证使用编辑器时钟图层。

CPU、音乐、天气等数据由独立数据适配器提供，显示组件接收明确的加载、就绪、错误状态和数据。浏览器预览注入模拟数据，Windows 实现放在 C# 宿主与 `desktopBridge.ts`，并通过同一数据结构返回。设备断开时保留最近数据并显示连接状态。API 凭据由宿主安全处理。

## 验收

- 四种尺寸为 1920×462、462×1920、1280×480、480×1280。
- 检查秒数开关、12/24小时制、午夜、时区和暂停恢复。
- 比较不同数字组合的边界，检查横竖屏的全部数字位于画面内。
- 验证图层添加、拖动、缩放、删除、撤销、保存、退出和重新打开。
- 验证同源主题独立保存，保存中的新修改和草稿恢复。
- 执行 `npm --prefix d-studio run lint`、`npm --prefix d-studio run build` 与 `./build.ps1`。
- 使用真实 D69/D92 检查输出、拔插重连和方向切换，使用另一台电脑检查首次安装、覆盖升级和托盘。

## Google AI Studio 首次提示词

```text
请在小屏宝PixBuddy 现有工程中开发【组件名称】。
先阅读 docs/COMPONENT_DEVELOPMENT.md、FlipClock.tsx、FlipClock.css、
ComponentPreview.tsx，以及 editorCatalog.ts、types/editor.ts、EditorArtworkRenderer.tsx。
新版运行目录是 d-studio，请沿用 React、TypeScript、Tailwind CSS 和现有紫白色界面。

先列出所需修改文件，再实现组件显示、参数配置、默认值和开发预览。
组件接收宿主提供的数据，时钟和暂停状态由宿主管理。
提供四种设备尺寸，覆盖配置保存恢复、旧配置默认值和卸载清理。
扩展现有图层类型时保持旧工程可打开，并同步导入和草稿校验。
使用模拟数据验证浏览器表现，注明需要 Windows 桥接和真实设备验证的部分。
完成后运行 lint 和 build，并说明接入入口与验证结果。
```

AI Studio 修改完成后提交到本开发分支，通过代码审查和本机设备验证后再合并。ZIP 导出也可用于本地逐文件合入。
