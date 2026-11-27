# 主题切换到静态壁纸的 USB 会话转交

## 已确认原因

用户点击壁纸详情“显示到 D92”后，日志显示 D92HidRenderTarget 无法取得设备锁：设备已有输出会话。托管主题仍在输出，旧 UsbOutputViewModel 尝试重新打开同一设备。此前只处理了旧 USB 到托管主题的转交，缺少反向流程。

## 修改

UsbOutputViewModel 在配置校验、支持能力检查和设置保存成功后，开 USB 前调用 ReleaseManagedOutputAsync。应用入口将其绑定到 ManagedDevicesViewModel.ReleaseForLegacyUsbAsync。

反向流程只允许稳定且唯一的相同 USB 序列号，使用本管理器精确 SessionId 停止对应会话并等待结束；没有持有会话时不操作外部租约，不停止其他设备。忙碌、歧义或会话变化时拒绝继续。原硬件协议、设备锁和 Craft 保护不变。不支持 USB 视频的限制不变。失败信息现在展示具体异常原因。

仍采用先停后启，不保证切换后启动失败自动回滚。没有发送新的实体设备命令进行自动验收。

## 验证

新增测试覆盖反向转交、其他设备继续、外部租约保持、回调失败不开 USB、正确序列号且等待回调后再开启传输。构建和完整回归见 artifacts/wallpaper-handoff-build.log、artifacts/wallpaper-handoff-regression.log。安装包20261025.12。

最终包改为20261025.13（.12为中间包不安装）。首轮新增测试发现已停止历史快照误当活跃会话，已修复；第二轮原生窗口最大化断言失败，未改窗口代码或放宽断言，保留wallpaper-handoff-regression-window.log。正在重跑完整回归。

最终结果：构建0警告0错误，完整Windows回归1153项通过（退出码0）。客户端退出后已安装并打开.13，settings哈希一致，旧app备份保留。安装记录artifacts/wallpaper-handoff-installed.json。
