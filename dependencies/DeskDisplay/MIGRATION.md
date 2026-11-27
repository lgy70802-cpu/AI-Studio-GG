# 把开发工作迁移到另一台 Windows 电脑

## 路线一：连这段聊天也一起交接

官方文档提供 Hand off：先连接目标主机，在两端保存同一 Git 仓库项目；聊天底部选择执行位置，再选择目标主机并 Hand off。它会转移聊天和 Git 状态，并在目标主机建立或复用 worktree。是否能看到入口，以两端客户端和工作区可用功能为准。

来源（2026-10-08 核对）：https://learn.chatgpt.com/docs/remote-connections#hand-off-a-chat-between-hosts

本项目当前没有 Git 提交，先保留本迁移包，再配置仓库基线与目标项目；不能对空仓库直接假定聊天交接会带走所有外部文件。UU 的远程连接本身不是 Codex 的主机连接，也不会自动搬迁项目或开发环境。不要用复制整个 .codex 目录的办法覆盖目标机器的凭据、配置和现有任务。

## 路线二：通过 UU 文件传输，立即接续开发

1. 将 DeskDisplay-Migration-20261008.zip 发送到目标电脑。目标电脑解压后应看到 MLB/DeskDisplay.sln、AGENTS.md 和 DEVELOPMENT_HANDOFF.md。不要只复制 exe。
2. 将 MLB 放在例如 D:\Dev\MLB。安装 Windows .NET 8 SDK（原机为8.0.425），Codex 在目标电脑独立登录。
3. 在目标电脑的 Codex 打开解压后的 MLB 目录，建立任务，粘贴文末交接提示。
4. 在项目根目录执行 `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Verify-Migration.ps1` 核对文件。需要构建和回归时加 `-Build -RunTests`。脚本不安装驱动、不启动 USB 输出。
5. 普通界面和代码开发无需接 D92；实际 USB 验证必须让执行程序的电脑能枚举到它。最可靠的是把 D92 的 USB 数据线接到新电脑。不要假设 UU 的桌面远控等同于 HID 设备转发。若设备留在原电脑，可继续在原电脑做硬件测试，新电脑负责开发。

## 应用设置

包内 migration/DeskDisplay-data/ 是源机 %APPDATA%\DeskDisplay 的独立备份，包含设置与少量日志。关闭目标电脑的 DeskDisplay 后，如目标不存在自己的配置，可将它复制到目标用户的 %APPDATA%\DeskDisplay。已有配置请先备份并合并，不直接覆盖。

同一块 D92 的已验证设置：序列号 C511D3782B05、宽1920、高462、旋转90°。新电脑上仍须刷新设备并确认选择；这些不是通用默认分辨率。Windows 的显示器身份、HID 路径以及旧诊断 JSON 中的 ImagePath 都不保证可跨机器复用。

SDK、NuGet缓存、驱动、Codex登录、原厂 DLL 和原厂安装目录不在包内。NuGet依赖由 dotnet restore 恢复。craft-* 子项目保留源码、素材和历史发布文件，若继续它们需按各自 package.json/锁文件恢复 Node 依赖。没有把本聊天逐字记录打进源码包；交接文档保存的是开发结论、证据、约束和待办，完整聊天请优先采用路线一。

## 给目标电脑 Codex 的提示

```text
这是从另一台电脑迁移来的 DeskDisplay/D92 管理软件项目，请接续开发，不要重新从零实现。
先读取 AGENTS.md、DEVELOPMENT_HANDOFF.md、MIGRATION.md、README.md，以及 artifacts/d92-wake-heartbeat-result.json。
先核对迁移清单，检查本机 .NET 8 SDK，再构建并运行回归检查。源机最后有207项检查通过，但这不等于新机已通过。
本机配置或依赖缺失时，先完成可独立进行的工作，说明具体缺项。不要沿用源电脑的绝对路径，也不要自动覆盖本机应用设置。
D92 黑屏曾由缺少唤醒/心跳造成，已补齐连接唤醒和每10秒心跳。不要再认定JPEG质量85不兼容，不要再次使用Frida附加Craft。
先确认D92是否实际连接本机；未连接时完成软件验证即可，不要声称硬件测试通过。完成后汇报迁移验收结果，再继续我的功能需求。
```
