# 软件更新发布

设置页的“软件更新”显示实际程序集版本。填写发行方提供的 HTTPS 清单地址后点击“检查更新”，有新版时显示说明及安装包大小。点击“备份并安装更新”后，程序下载并校验安装包，保存原生状态，备份配置和旧程序，退出并调用安装器覆盖安装；安装器成功后重新打开原工作区。

## 发布步骤

```powershell
./package-release.ps1 -Version 0.2.3 -UpdateBaseUrl https://updates.example.com/mirabox -ReleaseNotesFile ./release-notes.txt
```

将生成的安装包与 `update.json` 发布到该 HTTPS 目录。在客户端填写 `https://updates.example.com/mirabox/update.json`。安装包先上传完成，最后原子替换清单，避免用户下载到尚未上传完成的文件。未指定 UpdateBaseUrl 时只生成 `update-template.json`，其中示例地址需要替换。

清单格式如下，sha256 和 bytes 必须与实际安装包一致。

```json
{
  "version": "0.2.3",
  "notes": "本次更新说明",
  "url": "https://updates.example.com/mirabox/MiraBoxStudioNext-0.2.3-win-x64-Setup.exe",
  "sha256": "安装包的64位SHA256十六进制值",
  "bytes": 70275218
}
```

当前协议发布稳定的三段数字版本。相同版本或更旧版本不会触发升级。清单和安装包均要求 HTTPS，更新源由发行方管理。普通用户需要能读取这些地址；私有 GitHub 仓库的授权凭据不应分发到客户端。

## 数据与恢复

- 安装范围和目录从当前已安装程序的卸载信息读取，更新时沿用当前用户或所有用户模式。开发目录和未安装便携目录需要先使用安装包安装。
- 备份保存在 `%LOCALAPPDATA%/MiraBoxStudio.Next/Updates/Backups/时间戳`；自定义工作区使用其 Updates 目录。
- 完整备份目录包含配置快照、浏览器偏好、`previous-program.zip` 和 `backup.complete`。安装日志为 `install.log`。
- 安装失败时退出程序，按 `recovery.txt` 使用旧程序 ZIP 恢复程序目录。配置快照可用于恢复，素材继续保存在工作区。当前提供可恢复备份，尚未提供自动回滚界面。
- 取消下载或校验失败时不会启动安装器。旧的有效安装包缓存不会被失败下载覆盖。
- 自定义主题的未保存修改请先在编辑器保存；更新备份也包含本地草稿和预设，但编辑器的手动保存仍是正式工程写入入口。

`tests/UpdateChecks` 验证版本、协议、清单限制、分片下载、摘要、长度、取消和缓存保留。真实覆盖升级回归使用隔离工作区，并且仅在显式启用调试端口时允许本机 HTTP 回环测试源；普通运行要求 HTTPS。生产更新地址尚需由发行方配置。安装包当前为未签名内测版。
