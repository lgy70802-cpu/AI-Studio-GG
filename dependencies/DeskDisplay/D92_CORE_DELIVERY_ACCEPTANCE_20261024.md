# D92 核心交付验收（2026-10-24）

本轮完成内容契约、真实服务链路验收和固定入口测试版。沿用现有 WPF/MVVM、Scene 渲染、HID 协议与用户配置。未提交或推送 Git；当前分支为 feature/multi-device-foundation，原有工作区变更保留。

## 1. 内容契约

- `D92_CONTENT_CONTRACT_V1.md` 固定 REST 静态资源、可编辑 ThemeProject、ZIP 清单三种独立版本，列出实际枚举、素材引用、云端文件不再分发和外部插件边界。
- `src/DeskDisplay.Library/StudioResourceContract.cs` 校验 UUID、类型、MIME、长度、SHA、权限策略、最低版本及三个同源同资源路径；不接受协议相对路径、外链和未知权限策略。
- `StudioApiClient.DownloadAsync` 下载前重新检查兼容性。现有服务端权限、下载长度与 SHA 校验继续生效。
- 当前服务分发能力仍是静态 PNG/JPEG；没有新增云端主题、付费购买或外部插件安装能力。

## 2. 真实发布到 Windows 链路

运行 `tools/Verify-StudioPipeline.mjs`，使用相邻 `d92-studio-admin` 已安装依赖，建立随机端口、随机凭据、独立 PostgreSQL 和私有存储。执行现有 Prisma migrations、管理员 seed、真实 Nest API 登录、PNG 上传及发布。没有读取或覆盖后台 `.env`、开发数据库和客户端个人资源库。

Windows 使用生产 StudioApiClient、LibraryViewModel、CreatorViewModel、ThemeProjectStore、SceneJpegRenderer，完成以下 7 项检查：目录身份与 SHA、下载、损坏缓存恢复、云端来源保留、可编辑主题保存、真实时钟前后 JPEG 变化、云端原图不打入导出包。

证据：

- `artifacts/studio-pipeline-run.log`
- `artifacts/studio-pipeline-3f056914-2b0a-404e-8a5e-b5309dac5bd1/windows/result.json`
- 同目录 `pipeline.theme.json`、`preview.jpg`、`usb-ready-90.jpg`、`references-only.d92theme`

资源 ID：`c7e56f40-1e72-4c7f-b36c-2f13092a9a20`。

这是管理 REST 集成验收，未声称浏览器页面点击验收。完成后独立 API 和 PostgreSQL 已关闭，不修改用户在线资源服务器设置。不要分享整个 artifacts 目录，其中保留隔离数据库和测试存储；交付包不含它们。

## 3. 实体 D92

只读枚举返回序列号 C511D3782B05，ReportLength=1025。正常关闭旧客户端后，使用已有 UsbDemo `--theme` 加载本次云端下载后保存的可编辑源，按现有 settings 的 1920×462、90° 参数运行 40 秒，未修改协议或设备设置。

- 日志 `artifacts/pipeline-hardware.log`：40 帧、1 次连接、Error=null、最终 Stopped，退出码 0。
- JPEG 元数据为 462×1920，源布局 Landscape、旋转 90°，沿用现有设备输出方式。
- `artifacts/pipeline-hardware-result.json` 记录 settings SHA 前后相同。
- 用户已确认本轮实体画面方向、文字及秒数均正常；该人工确认与上述传输日志分别记录。
- 本轮未进行拔插、双实体设备、长时间或视频测试。

## 4. 测试与安装交付

| 检查 | 结果 | 证据 |
|---|---|---|
| Debug 构建 | 0 警告、0 错误 | artifacts/distribution-build.log |
| Windows 完整回归 | 842 项通过，含 13 项契约检查 | artifacts/distribution-regression.log |
| 适配及会话 | 95 项通过，虚拟设备 | artifacts/distribution-adaptation.log |
| 配置 | 19 项通过 | artifacts/distribution-smoke.log |
| 独立真实 API 链路 | 7 项通过 | artifacts/studio-pipeline-run.log |
| 安装文件事务 | 7 项通过 | artifacts/distribution-packaging-tests.log |
| 自包含 Release 发布 | 成功 | artifacts/distribution-publish.log |

安装事务覆盖首次安装、更新清除旧文件、保留旧版本备份、保留其他数据、损坏包不替换、未列文件及路径遍历拒绝。测试假 exe 仅作字节文件，未执行；没有注入 Move 失败来验证所有恢复分支。

版本：`0.1.0-preview.20261024.1`（Windows x64，自包含 .NET 8）。

- ZIP：`artifacts/releases/D92Studio-0.1.0-preview.20261024.1-win-x64.zip`
- SHA256：`E560C27DE8ED0F9FF3D6195D50DBC98AE7F18C574E95E871E9BE084CA2D99B79`
- 已安装到 `%LOCALAPPDATA%\D92Studio\app\DeskDisplay.App.exe`。
- 桌面 `D92 Studio.lnk` 已核对指向上述固定路径。
- 正常打开后的控件树显示版本号、D92 已连接；进程 14092，Responding=True。
- 窗口截图两次失败（FrameArrived/window capture timeout）；本轮不提供伪造截图或声称视觉逐像素验收。

安装先校验清单与文件哈希，再暂存、替换；保留旧 app 备份，用户 APPDATA 配置/资源库不覆盖。备份需用户确认新版稳定后自行清理，卸载也保留备份。安装包未做发布者代码签名；SHA 校验不等同签名。尚未在无厂商环境的干净电脑验收。

## 5. 复现

在项目根目录使用 PowerShell 7：

```powershell
dotnet build DeskDisplay.sln -p:BaseOutputPath=E:/Codex/MLB/MLB-DeskDisplay/artifacts/distribution-build/
& artifacts/distribution-build/Debug/net8.0-windows10.0.19041.0/DeskDisplay.DisplayTests.exe
& artifacts/distribution-build/Debug/net8.0/DeskDisplay.AdaptationTests.exe
& artifacts/distribution-build/Debug/net8.0/DeskDisplay.SmokeTests.exe
pwsh -File tools/Test-Packaging.ps1
node tools/Verify-StudioPipeline.mjs --admin-root E:/Codex/MLB/d92-studio-admin --client E:/Codex/MLB/MLB-DeskDisplay/artifacts/distribution-build/Debug/net8.0-windows10.0.19041.0/DeskDisplay.DisplayTests.exe
```

真实服务验收要求相邻后台依赖及 embedded-postgres 已安装；不会自动发送 HID。发布新包需新版本号，现有发布目录不会覆盖：`pwsh -File tools/Publish-WindowsTest.ps1 -Version <新版本>`。

## 后续边界

云端可编辑主题/依赖授权下载、外部插件签名与权限隔离、Mac 实现、代码签名与干净电脑安装验收、双实体设备长期验证仍是后续工作。当前不把静态资源发布测试扩称为完整主题/插件商城。

