# 验收与复现

所有实机脚本都通过真实 WebView2 的桥接请求操作设备，会改变输出。不要对日常工作区无准备地运行整套验收。

## 准备

1. 执行根目录与 `d-studio` 的 `npm ci`，再执行 `build.ps1`。
2. 创建测试数据目录；通过 `MIRABOX_DATA_DIR` 指定它。连接 D69 和 D92 各一台。
3. 正常关闭同一工作区的旧进程，以 `MIRABOX_DEVTOOLS_PORT=9227` 启动构建后的程序。
4. 创建视频导入样本：

```powershell
New-Item -ItemType Directory -Force artifacts/video-qa | Out-Null
Copy-Item tests/fixtures/color-bars.mp4 artifacts/video-qa/验收_视频.mp4
```

## 分阶段脚本

| 顺序 | 脚本 | 作用 |
|---|---|---|
| 1 | `tools-video-native.cjs` | 文件选择器导入、预览、视频输出、暂停、创建图片/视频列表 |
| 2 | `tools-theme-native.cjs` | 创建动态主题，验证双设备布局和秒钟 |
| 3 | `tools-theme-editor-ui.cjs` | 编辑器真实保存/应用/复制/预览，并创建后续所用工程 |
| 4 | `tools-reliability-native.cjs` | 视频背景+时钟+动效、画面控制、电源事件模拟、备份、创建综合列表 |
| 5 | `tools-renderer-fault.cjs` | 强制终止一个后台渲染页并检查自动恢复 |
| 6 | `tools-renderer-fault.cjs main` | 强制终止主界面渲染页并检查恢复 |
| 7 | `tools-power-schedule.cjs` | 开机启动开关往返、软件待机/恢复试运行 |
| 8 | `tools-reliability-soak.cjs 7200` | 两小时三类混播、逐次状态、停滞和画布数量检查 |

`tools-video-lifetime-native.cjs 10` 对已连接设备执行十轮真实视频/图片快速替换，并检查视频进度与暂停继续；会改变实际输出。`tools-reliability-soak.cjs 7200 run-name` 可将不同版本的证据保存在独立子目录，避免覆盖失败或中断记录。

部分脚本使用测试主题名和既有测试 ID；重复运行前检查当前数据和本地草稿提示。历史工具保留用于单项复验，不保证任意顺序都可从空目录运行。

`tools-package-ui.cjs` 与 `tools-ui-audit.cjs` 使用 9228 的独立测试工作区。不要在两个程序中同时启动同一物理设备输出。迁移验收会恢复到暂停状态，布局验收只读取界面。

`node tools-font-audit.cjs` 也使用 9228，要求该工作区全部输出暂停。它禁用字体缓存并阻断 Google Fonts，检查八种原型字重从本地 WOFF2 真实载入，随后解除阻断。分发的字体及来源、校验值、OFL 位于 `d-studio/public/fonts`。

`node tools-editor-layout-audit.cjs` 在同一暂停工作区打开已保存工程，只检查三种宽度的保存/应用/设备预览按钮边界，不执行这些按钮。`--autostart` 最小化静置检查应保持调试器断开，至少观察七分钟；`desktop-output.jsonl` 不存在代表没有产生重载或输出诊断事件，应按零事件处理。

`SceneRenderChecks` 需要交互式 Windows 和 WebView2，但不访问 USB，也不打开外部调试端口。构建 Web 后执行：

```powershell
dotnet run --project tests/SceneRenderChecks -c Release -- artifacts/current/Web `
  "$env:LOCALAPPDATA/MiraBoxStudio.Next" qa-composite-theme artifacts/reliability-qa/scene-normalmode.json
```

要求指定工作区存在该已保存的合成工程。检查直接渲染两个型号的后台画布，各观察八秒，验证新画面持续变化及最终协议光栅。

## 长时间采样

`tools-reliability-soak.cjs` 每约 5 秒记录一次设备状态到 `artifacts/reliability-qa/long-soak.jsonl`，每分钟写进度，最终生成摘要。它检查：

- 三种内容均出现，并达到合理切换次数；
- 连接状态与播放意图；连续失败超过 20 秒或动态预览长期不变会失败；
- 后台画布数量有界；
- 采样不存在大于 30 秒的长中断。

资源采样与设备状态分开执行，并明确指定被测宿主 PID：

```powershell
./tools-video-resources.ps1 -Seconds 7200 -HostProcessId 12345 `
  -OutputFile artifacts/reliability-qa/long-resources.json -KeepSystemAwake
```

采样按实际 WebView2 用户数据目录识别相关进程，并用 PID+进程创建时间区分复用，避免把无关进程累计到 CPU。CPU 按逻辑处理器数归一化。`-KeepSystemAwake` 仅是验收进程的临时系统保持唤醒请求，不改变应用或系统电源计划；这不构成真实休眠实测。

## 故障与数据检查

- `PackageChecks` 使用 64 MiB 依赖文件，移走原目录后恢复到新目录，核对依赖、冲突引用、校验失败与事务日志回滚。
- `DisplayPowerChecks` 检查真实编码像素、协议光栅尺寸、黑帧和亮度恢复，不访问 USB。
- `dotnet run --project tests/VideoOutputChecks -c Release -- tests/fixtures/color-bars.mp4 12` 额外执行十二轮双解码器并发开关；热身后句柄增长超过 200 会失败。默认一次运行仍是原有 12 项检查。重复模式最后的强制 GC 仅用于独立测试进程的诊断，正式应用不强制 GC。可选 `--native-lifetime` / `--image-lifetime` 第三个参数用于分别隔离原生解码及静态编码，不访问 USB。
- 异常结束实机测试应先保存当前索引；只终止已确认路径及 PID 的被测程序，重新启动后核对正式内容和列表状态。
- 缺失素材测试只能临时移动自建测试素材；无论断言成功与否都在 `finally` 恢复文件。
- 不自动拔插设备、不改变 USB 驱动、不主动休眠整台用户电脑。实际拔插及真实系统休眠另作实体补验。

原型独立 `.d92theme.json` 描述文件不会自动附带素材。迁移测试必须用 `.mirabox` 包，并检查新工作区的本地素材文件及视频解码，不能只看导入成功提示。
