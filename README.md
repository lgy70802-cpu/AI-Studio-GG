# MiraBox Studio Next

用于 Windows 上 MiraBox D69 / D92 屏幕的内容管理与控制软件。桌面宿主使用 .NET 8 / WPF / WebView2，主要界面沿用本仓库原有的 D Studio React 原型；设备按真实序列号独立输出。

## 已接通的功能

- 图片、视频导入与管理副本；收藏、重命名、回收与恢复。
- 原型主题编辑器、文字/图片/形状图层、多规格布局、时钟、日期与星期。
- 视频背景、视频图层与原型 CSS 动效合成；同次会话暂停继续保持视频时间轴。方向切换或重启后视频从头播放。
- 图片、视频、主题混播；每项时长、顺序/随机、暂停与上下项。
- 原生后台定时、执行记录、一次性去重、冲突与缺失目标处理。
- 每台设备独立的软件画面调暗、黑帧待机及恢复；不把这些操作当作背光亮度或硬件断电控制。
- 含素材依赖的完整备份与单工程包；导入前校验、冲突 ID 重映射、恢复日志与上一份有效快照。
- 渲染心跳、超时、异常重建、系统电源事件处理及启动恢复。

插件真实数据、账号/支付、真实在线商店和真实桌面录屏仍未接入。相应区域保留原型展示并标明示例或未支持。功能核对详见 [docs/FEATURE_AUDIT.md](docs/FEATURE_AUDIT.md)。1123 HTML 不属于本版本源码或发布内容。

## 构建与运行

需要 Windows 10/11、.NET 8 SDK、Node.js 22.12 或以上版本，以及 Microsoft Edge WebView2 Runtime。运行构建好的程序需要 .NET 8 Windows Desktop Runtime。

```powershell
npm ci --ignore-scripts
npm --prefix d-studio ci
./build.ps1
./artifacts/current/MiraBox.App.exe
```

构建前请关闭正在使用该输出目录的程序。`build.ps1` 构建前端与宿主，输出到 `artifacts/current`。`frontend` 是保留的早期参考代码，当前构建入口为 `d-studio`。

默认数据目录为 `%LOCALAPPDATA%/MiraBoxStudio.Next`。`MIRABOX_DATA_DIR` 可指定独立工作区，不同工作区使用不同进程互斥标识。不要将日常数据目录、WebView2 用户目录、备份包或密钥加入 Git。

## 备份与迁移

请先保存编辑器中的修改。设置页提供“导出完整备份”“导出工程包”和“导入或恢复备份包”。包包含真实素材字节和 SHA256 清单，不只是引用原路径的 JSON。

- **合并导入**保留现有内容；冲突主题、列表与定时生成新 ID，关联引用一起重映射，工程编辑标识独立。
- **恢复备份**替换索引及设置，保留已有素材文件，先暂停设备输出和定时，供用户核对后恢复。
- 缺少素材时导出会报出缺失依赖，不把不完整包伪装成成功备份。
- JSON 损坏时优先恢复上一份有效快照并保留损坏原件。设备/调度状态无法启动时提供原生恢复窗口。
- 仅导出 `.d92theme.json` 是工程描述，跨电脑迁移请使用包含依赖的 `.mirabox` 工程包。

换电脑后，同一设备的序列号仍是绑定依据；更换实际设备时应在列表、定时界面重新绑定，不能按“第二块屏幕”猜测目标。

## 测试

离线检查不向 USB 发送命令：

```powershell
foreach ($name in @('LibraryChecks','PlaylistChecks','ScheduleChecks','ScheduleHostChecks','ThemeHostChecks','PackageChecks','DisplayPowerChecks')) {
    dotnet run --project "tests/$name" -c Release
    if ($LASTEXITCODE) { throw "Failed: $name" }
}
npm --prefix d-studio run lint
dotnet run --project tests/VideoOutputChecks -c Release -- tests/fixtures/color-bars.mp4
```

`VideoOutputChecks` 使用真实 Windows 解码器及模拟传输，不访问 USB；需要可用的 MP4 解码环境。

实机脚本通过可选的 WebView2 调试端口访问实际应用，不使用浏览器模拟设备：

```powershell
Start-Process ./artifacts/current/MiraBox.App.exe -Environment @{MIRABOX_DEVTOOLS_PORT='9227'}
node tools-reliability-native.cjs
node tools-renderer-fault.cjs
node tools-renderer-fault.cjs main
node tools-reliability-soak.cjs 7200
```

这些脚本会改变连接设备的输出、创建验收素材和列表。请在测试工作区执行；不同脚本的先后依赖、双设备要求见 [docs/TESTING.md](docs/TESTING.md)。正常使用不需要开启调试端口。脚本会在 `artifacts` 写入本机证据，这些文件不纳入版本库。

## 结构

| 目录 | 用途 |
|---|---|
| `d-studio` | 当前 React 原型界面、编辑器及后台场景画布 |
| `src/MiraBox.App` | 新桌面桥接、设备会话、合成、备份和恢复；保留部分 V67 引擎代码 |
| `src/MiraBox.Core` | 原生资源/会话基础模型 |
| `dependencies/DeskDisplay` | 随源码附带的显示与 USB 引擎，构建无需访问开发者原工作树 |
| `tests` | 离线检查和可重复使用的生成视频样本 |
| `tools-*.cjs` / `tools-*.ps1` | 实际 WebView2、USB 与持续运行验收工具 |

实现使用 WebView2 的[图像捕获接口](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.capturepreviewasync)及 [CDP 场景帧流](https://chromedevtools.github.io/devtools-protocol/tot/Page/#method-startScreencast)，复用原型布局。JPEG 编码在独立 STA 工作线程执行；界面预览与设备发送频率分开。合成输出有设备/主机性能上限，不能以 USB 重发次数代替新画面的合成帧率。

原生视频在列表切换间复用已暂停、清空 Source 的播放器，闲置缓存最多四个；素材、GPU 帧和读回缓冲仍在每次结束时释放，失败播放器不回池，退出时关闭缓存。该策略用于避免反复创建原生播放队列的资源增长，并由独立循环及实机采样验证。[MediaPlayer 的官方类型约定](https://learn.microsoft.com/en-us/uwp/api/windows.media.playback.mediaplayer)允许 Agile / Both 线程模型；本程序仍保持每次租用独占并串行解码。
