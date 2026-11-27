# 给 Mac 上 Codex 的 D92 Studio 客户端对接交接

日期：2026-10-13。先读 [客户端 API 约定](D92_STUDIO_CLIENT_API_CONTRACT.md)，再读 [HID 协议](D92_HID_PROTOCOL_SPEC.md) 和 [HID 移植指南](D92_HID_MAC_PORTING_GUIDE.md)。API 账号/资源逻辑与实体 HID 传输应分别建立边界。

## 第一批实现顺序

1. 建立可配置的 server origin、HTTPS 校验、统一 HTTP 客户端与安全错误解析。另一台 Mac 的 localhost 不会访问 Windows 后台；先获得可访问的服务器地址。
2. 实现 register/login/me/logout；令牌按 origin 存 Keychain，不解析不透明 token，不共用管理员身份。启动时验证会话，401 清除令牌，退出保留本地文件。
3. 实现资源目录和分类/标签；分别传 scope、orientation、sort、page/pageSize。一级/二级分类按 ID 联动。保留目录/详情状态、筛选、页码和滚动位置，取消过时请求。
4. 使用 thumbnailUrl 绘制列表、previewUrl 绘制详情；横图 4:1、竖图 1:4，完整等比显示。FREE/VIP/PURCHASE/PURCHASE_OR_VIP 和 null 价格应如实显示。
5. 同步 UserLibrary，实现收藏/取消；理解 owned 只包含单资源权益，不能当作所有 VIP 内容。未实现支付时只显示状态，不制作会成功的购买按钮。
6. 下载原包并核验精确长度与 SHA-256，写临时文件后原子落盘，取消/失败清理。缓存按 origin+resourceId+version 区分，本地入库按 origin+resourceId 去重。
7. 更新保留用户本地名称、分类、标签、收藏和轮播顺序，刷新图片尺寸与解码缓存。下载任务记录资源、服务器、进度和结果，不跨服务器重试旧任务。
8. 先验证本地静态图预览，再按 HID 资料接入实体 D92；复用字节协议，重写平台枚举和写接口。不要直接移植 WPF/Win32 代码，也不要把软件中的视频播放当成 D92 HID 视频输出。

## 共享数据映射

| 领域对象 | Windows 对照 | Mac 需要保留的字段 |
|---|---|---|
| Resource | StudioResource | UUID、版本、策略、尺寸、哈希、包大小、预览/下载相对路径、分类与标签 |
| UserProfile/Session | StudioUserProfile/StudioAuthSession | 用户 ID、邮箱、VIP 状态/日期、token、会话到期时间 |
| UserLibrary | StudioUserLibrary | 单资源权益 ID、hasVip、云端收藏 ID |
| LocalResource | WallpaperAsset | 独立本地 ID、云端 origin/ID/版本、本地路径、用户名称/分类/标签、收藏、添加时间 |
| DownloadTask | DownloadTaskViewModel | 资源、origin、进度、Running/Completed/Failed/Cancelled、错误类别 |
| DeviceConfig | UsbOutputSettings | 所选序列号、逻辑画布宽高、像素旋转；不绑定 Windows 显示器编号 |

Windows 当前 LocalResource 未保存 origin；跨服务器同 ID 的去重仍是待改边界，Mac 新实现应加入 origin，后续再统一迁移规则。不要把 Windows 盘符路径复制成 Mac 配置。

## 最小验收矩阵

| 场景 | 必须观察的结果 |
|---|---|
| 未登录浏览 | 能看目录/预览；收藏需登录；免费可下载 |
| 注册/登录/重启 | 真正连接服务端；凭据不进入日志；Keychain 会话可恢复 |
| 过期会话/退出 | 401 正确处理；旧 token 不再获授权；本地图片保留 |
| 权益 | 无权益拒绝、有效 VIP/单购允许、过期或撤销拒绝 |
| 分类/方向/分页 | 请求参数正确；快速连续筛选最终结果不被旧请求覆盖；离开详情再返回保留状态 |
| 下载 | 长度和哈希匹配才入库；失败/取消没有残留临时文件；重复下载不新增本地副本 |
| 版本更新 | 保留用户编辑和轮播顺序；新图片尺寸与缓存刷新；版本竞争时刷新清单重试 |
| 网络离线 | UI 可操作；显示缓存/空态与实际错误；不伪造在线数据或权益 |
| D92 | 先只读识别正确设备；Report ID/长度单独核实；传输成功与实体方向/画面分别记录 |

已有本地后台测试素材带 `[验收测试]` 标记，共横屏/竖屏各 4 份、四种策略。这些是实际发布的测试图片，不是真实销售商品；没有付费交易。Mac 要访问它们需连接同一后台，而不是另建空库后期待自动同步。

## 给 Mac 的文件与启动提示

通过有权限的 GitHub 账号克隆 Windows 私有仓库后，优先提供本文件、API 约定、两份 HID 文档，以及 `src/DeskDisplay.Library/StudioApiClient.cs`、`StudioSessionStore.cs`、`src/DeskDisplay.Display/Usb/D92FrameEncoder.cs` 作为行为参考。后台服务端源码在单独项目 `d92-studio-admin`，不在 Windows 仓库中；本次把 API 约定复制到它的 docs 目录，方便两边对照。

无需复制 Windows 发布 EXE、bin/obj、真实 .env、管理员密码、DPAPI 会话文件、用户资源索引或浏览器缓存。两个 Codex 账号可以不同，仓库访问由 GitHub 权限决定。

建议给 Mac Codex 的任务：

> 请先阅读 D92_STUDIO_CLIENT_API_CONTRACT.md 与 D92_STUDIO_MAC_CLIENT_HANDOFF.md，对照已实现接口开发 macOS 的账号、静态资源目录、分类、收藏、校验下载和本地入库。明确报告没有接口的功能。先完成软件层验收，再参考两份 HID 文档接入设备，不重新探索已有字节协议，也不假设 macOS 的 HID API 与 Windows Report ID 缓冲区约定相同。

此文不锁定 Mac UI 技术栈，也不表示 macOS 实机或付费离线授权已经完成。
