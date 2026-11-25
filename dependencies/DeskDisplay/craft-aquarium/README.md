# 静水 · 动态鱼缸 V1.1

## V1.1 自然游动

- 尾部、背鳍和腹鳍采用 20 帧预生成形变动画，帧间混合平滑播放；摆动从身体向尾端增强，头部与眼睛保持稳定。游得慢时摆尾也变慢，短暂停留时保留轻微鳍动。
- 每条鱼以 12–22 秒的柔和过渡改变游动高度，过渡两端的速度和加速度均归零；偶尔在水域内部减速悬停约 2–3 秒，再缓慢加速。边缘转向优先，鱼体始终留在安全水域。
- 针对当前背景加入建筑、珊瑚遮挡：远景鱼在建筑及珊瑚后方，中景鱼在建筑前、部分近处珊瑚后方，近景鱼在最前方。拱门暗部和珊瑚枝条间的水域保留开口。遮挡轮廓为当前背景的近似深度分区，并非三维场景。
- 鱼素材增加克制的暖粉色光照、上亮下暗与景深色调，初始化时一次完成。动画与遮挡都缓存，不逐帧生成纹理或读取像素。
- 配置新增 naturalMotion、warmFishLight、occlusion。更换背景路径后默认不使用旧背景的遮挡；如果用新图覆盖原文件名，应将 occlusion 设为 false 或重新制作 Occlusion.js 中的分区。
- 继续使用 Canvas 2D 和同一 Scene/Renderer；浏览器 rAF 隐藏暂停，Craft Worker 在有效输出时持续运行。未新增设置菜单、交互功能或依赖。

验证：`node --test tests/scene.test.mjs` 包含八小时运动模拟、路线连续性及悬停恢复；`node tests/natural-motion.cjs` 检查头部稳定/尾部运动、石墙/拱门/珊瑚遮挡、三层合成顺序、缓存复用和销毁。每条鱼固定缓存 20 帧，当前素材通常约 12–20 MB 的 RGBA 帧数据（随随机尺寸变化），另外保留两张背景大小的遮挡层；不是每帧增长的缓存。

纯 HTML/CSS/JavaScript + Canvas 2D，无运行时 npm 依赖、无 WebGL/Three.js。使用用户提供的 2557×615 背景原图。独立插件 ID，不修改时钟、股票插件或 WPF 工程。

## 运行

直接双击项目内 `index.html` 或 `preview.html`，也可运行“打开鱼缸.cmd”用默认浏览器打开。file:// 下 index.html 自动跳转到图片内嵌的离线预览，不需要服务器。鼠标正常显示。浏览器 F11 可全屏；页面没有设置菜单、鼠标控件或滚动条。默认 contain 完整显示背景，非同宽高比窗口会留深色边，不拉伸。

开发时在本目录执行 `npm run preview`，访问 http://127.0.0.1:5188 。通过 HTTP 打开的 `index.html` 加载独立 ES modules。修改代码或配置后执行 `npm run build` 更新双击预览和插件目录。构建与测试无需 npm install。

本机也可直接使用 Node（不依赖 npm 命令）：`node tools/serve.mjs` 启动开发预览；`node tools/build.mjs` 构建；`node --test tests/scene.test.mjs` 运行运动测试。

## Craft 插件

构建产物：`release/Aquarium-V1.1/com.mirabox.streamdock.aquarium.mPanelPlugin/`。退出 Craft 后，将整个 .mPanelPlugin 文件夹复制到 `%APPDATA%\HotSpot\MiraBox Craft\plugins\`，重新打开 Craft，在“静水鱼缸 / Aquarium”中添加“动态鱼缸”。先确认该路径是本机实际使用的插件目录。构建不会自动安装，也不会修改正在使用的插件。

默认组件大小 2557×615，实际输出跟随 Craft 的 ActionGeometry。选择目标显示设备由 Craft 完成，不假定“显示器 2”。1.0.1 默认使用现有时钟插件的 JSON setImage + JPEG data URL 接口；保留 outputTransport: 'binary' 供确认支持的宿主使用。WebSocket 缓冲超过 1 MiB 则丢帧，断开时销毁场景，重连后等待 willAppear。预览和 Craft 调用同一 Aquarium → Scene → Renderer；不复制鱼缸运动代码。

## 项目目录与完整代码

```text
craft-aquarium/
  index.html                 开发入口
  preview.html               可双击预览（生成）
  src/
    aquariumConfig.js        配置
    Aquarium.js              生命周期、rAF、帧率、暂停恢复
    Scene.js                 共享场景状态
    Renderer.js              静态背景缓存、适配、绘制顺序
    Fish.js                  路线、漂浮、悬停、转向与姿态
    FishSprites.js           预缓存尾鳍形变与暖色光照
    Occlusion.js             背景专属建筑、珊瑚遮挡
    BubbleSystem.js          24 个循环复用粒子
    WaterLight.js            缓慢平移缩放的 PNG 焦散
    LightRay.js              4 束柔和顶光
    assetLoader.js           预加载、缓存、结构化日志
    CraftTarget.js           Craft 生命周期与有界帧传输
    CraftClock.js            隐藏宿主的后台 Worker 时钟
    main.js                  预览 / Craft 入口
  assets/
    background/aquarium-bg.jpg
    fish/fish01.png … fish05.png
    fish/atlas.png           生成的原始透明图集
    fish/PROMPTS.md          素材生成记录
    effects/caustics.png
  tools/build.mjs            无依赖构建
  tools/serve.mjs            本地服务器
  tools/prepare-assets.cjs   开发素材裁切工具
  tests/scene.test.mjs       运动与八小时模拟测试
  tests/browser.cjs          浏览器与 Craft 模拟测试
  release/Aquarium-V1.1/       可复制的插件包
```

所有源文件均为完整实现，不需要从聊天记录复制代码。

## 核心实现

- 设计坐标默认 2557×615，可在配置修改；整个场景随目标窗口等比缩放。默认像素比上限 1、总绘制像素上限 400 万，以控制资源开销。
- 绘制顺序：静态背景 → 焦散 → 2 条远景鱼 → 建筑/珊瑚遮挡 → 4 条中景鱼 → 前景珊瑚遮挡 → 气泡 → 2 条近景鱼 → 顶部光束。背景只在尺寸改变时重建，不随动画移动。
- 默认开场即分散放置 8 条鱼，避免长时间空画面；`spawnFromEdges: true` 可启用左右边缘进入。每条鱼有独立速度、漂浮幅度、频率、相位。每 5–12 秒改变目标速度，用时间相关指数平滑。
- 边界留有鱼体宽度与减速距离，转向 500–800ms，先减速停顿，再以 cos 缩放穿过侧身姿态，再朝反方向加速；只需要朝右素材。
- 三层透明度、大小、速度不同；模糊、光色与尾鳍形变只在初始化生成缓存素材，运行中只混合相邻动画帧。
- PNG 焦散约 0.085 透明度，以 2px/s、0.5px/s 平移，缩放 1–1.03。光束摆动不超过 1°。24 个气泡在两侧稀疏上升。
- 浏览器主循环 requestAnimationFrame，默认 30 FPS，可选 24/60，使用 deltaTime。长停顿限制 dt 避免恢复瞬移。浏览器 visibilitychange 隐藏即停，恢复重置时基。Craft 的隐藏宿主并不代表显示设备隐藏，故使用独立 Worker 时钟驱动同一场景，每次消息需主线程确认，最多一个待处理 tick；移除全部组件停止计时，销毁时 terminate Worker 并回收 Blob URL。dispose 同时清理 rAF、监听器、ResizeObserver、连接和重试。
- 图片一次预加载；无每帧 new Image / 数组创建 / 不受控随机生成。Canvas 光效缓存复用。Craft 编码所需 Blob/ArrayBuffer 为短期对象，队列有界。

## 配置

修改 `src/aquariumConfig.js` 后 `npm run build`。可配置 `background`、`backgroundFit`（contain/cover）、`fishImages`、`maxFPS`、`fishCount`、`fishSpeed`、`spawnFromEdges`、`bubbles`、`bubbleCount`、`caustics`、`lightRays`、`debug`。资源使用包内相对路径；替换图片后也需重新构建双击预览和 Craft 版本。构建嵌入图片字节，避免 file:// 的跨源画布污染导致 Craft 无法编码输出；开发服务器仍按路径读取图片。开启 debug 才在左上角显示 FPS / 鱼数 / 气泡数。

`outputFPS` 控制外接屏输出频率，不能超过 maxFPS；默认 JPEG 0.88（背景不透明），JSON setImage 兼容传输。实际 CPU、帧率受 Craft 编码、USB/网络传输和设备性能影响。

## 验证与边界

`npm test`：八小时虚拟时间的连续运动、边界、对象池稳定性、24/30/60 FPS 位移一致性和转向。该模拟不是八小时实机运行或内存证明。

`node tests/browser.cjs`：本机 Edge + Playwright，检查双击入口、无脚本错误、尺寸适配、隐藏暂停/恢复、销毁、实际帧率，以及隐藏且 rAF 不触发的 Craft 宿主、JSON 图像解码、背景可见性和 Worker 停止清理。测试工具默认使用本机 Codex Playwright 路径，可通过 PLAYWRIGHT_PATH 指定其他安装。

尚需 D92 实机验证：持续数小时内存/CPU/GPU、真实热插拔及输出帧率。2026-09-23 已直接检查本机 Craft 的 QtWebEngine 6.4.3 宿主，确认有效输出实例的 document.hidden 为 true，已针对这一行为修复。宿主持续发送画面不等于已验证物理屏幕的最终显示与长时间性能。

缺失单条鱼素材会复用其余成功加载的鱼；焦散缺失关闭该层；背景缺失回退深色底并输出结构化日志；全部鱼素材缺失时停止启动并记日志。正常运行不显示 UI。

素材裁切仅开发时需要仓库 craft-stocks 的 @napi-rs/canvas。已交付 PNG，无需重新执行素材脚本。AI 鱼素材由内置 imagegen 生成，提示词与来源见 assets/fish/PROMPTS.md；背景为用户提供图片。

## 本次验证记录（2026-09-22）

- Edge 无头浏览器、2557×615：30 FPS 档约 30.01 FPS；24 FPS 档约 23.90；60 FPS 档约 56.19，60 档在此测试环境未跑满。
- 连续 60 秒、两次强制 GC 后的 JS 堆：1,483,996 → 1,515,040 字节，约增加 31 KB；不包含完整 GPU/原生图像内存，不代表已证明长期无泄漏。
- 八小时虚拟时间模拟及其他 3 项运动测试全部通过。8 条鱼 / 24 个气泡对象池保持恒定，所有鱼都转向且保持边界内。
- 直接打开 HTML、四种窗口尺寸、页面隐藏暂停与恢复、dispose、模拟 Craft 启停/调整大小/二进制 JPEG 帧/缓冲背压均通过，无未捕获脚本错误。
- Craft 导出曾受 file:// 图片跨源限制影响，已通过构建时内嵌图片修复并复测通过。

交付包：`release/Aquarium-V1.1.zip` 包含插件文件夹和可直接打开的“浏览器预览.html”；`release/Aquarium-V1.1-Source.zip` 包含完整源码、素材、构建脚本及测试。2026-09-23 已备份并更新本机安装的鱼缸插件到 1.1.0；D92 实机长时间运行与热插拔尚未验证。

## V1.1 验证记录（2026-09-23）

- 6 项运动测试通过，包含八小时虚拟时间运行；新增路线连续性、悬停恢复和关闭自然游动的回退检查。
- 实际绘制检查通过：尾鳍帧变化、头部区域保持一致、石墙遮挡、拱门开口、前景珊瑚以及三层合成次序正确；缓存对象在更新中复用，销毁后释放。
- Edge 预览 30 FPS 档约 30 FPS；一分钟两次 GC 后 JS 堆增加 35,160 字节。这不包括原生 Canvas/GPU 内存，也不作为长期无泄漏保证。
- Chrome 四个本地入口、浏览器隐藏暂停恢复及 Craft 隐藏/rAF 不触发条件下的发送与清理回归均通过。
- 已单独重载本机 Craft 鱼缸插件；只读验证确认 8 条鱼各有 20 帧缓存，遮挡开启，场景时间与发送时间持续增长，保留用户当前 1938×466 组件尺寸。实际宿主输出截图为 craft-v1.1-live.png；这不是物理屏幕截图，也未把浏览器测得的 30 FPS 当作 Craft 输出帧率。
- 升级前备份：%APPDATA%/HotSpot/MiraBox Craft/plugin-backups/com.mirabox.streamdock.aquarium.mPanelPlugin-before-1.1.0-20260923-130650。

`node tests/craft-live.mjs` 可只读验证已安装并运行的 V1.1，默认本机调试端口 23518（可通过 CRAFT_DEBUG_PORT 修改），同时保存宿主渲染图。

## 本地入口修复

修复直接在 Chrome 中打开根目录 index.html 时 ES module 被 file:// 跨源限制拦截的黑屏：现在自动进入离线预览。移除 cursor:none，恢复鼠标；启动失败时显示具体错误。插件目录的 index.html 单独打开时也可预览，收到 Craft 连接后停止预览并切换至插件模式。

`node tests/entrypoints.cjs` 验证四个入口的实际非黑画面、动画推进、可见鼠标和无脚本错误；设置 BROWSER_CHANNEL=chrome 可在 Chrome 复测（默认 Edge）。

## 1.0.1 Craft 黑屏修复（2026-09-23）

直接从运行中的 Craft 调试接口观察旧实例：hidden=true、running=false、time=0、lastSent 尚未设置，证实浏览器隐藏暂停逻辑阻断了 Craft 输出。现在 Craft 用 Worker 时钟，初始化立即发送首帧，默认 JSON setImage 与已有时钟插件一致；预览仍使用 rAF 并在隐藏时暂停。

修复后单独重载鱼缸插件，确认同一实际 Craft 组件 hidden=true 时 running=true、时钟 active=true，time 与 lastSent 持续增长，连接保持打开。备份位于 %APPDATA%/HotSpot/MiraBox Craft/plugin-backups/com.mirabox.streamdock.aquarium.mPanelPlugin-before-1.0.1-20260923-090339。可通过 `node tools/inspect-craft.mjs` 只读检查本机 23518 调试端口上的鱼缸实例。
