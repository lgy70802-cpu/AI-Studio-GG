# D92 视频实机短测

日期：2026-10-28。用户确认保存并关闭D92 Studio/CRAFT后执行。使用既有已验证HID协议和原型工具，未修改用户设置或安装客户端。

## 设备与素材

- 序列号 C511D3782B05；Output Report Length 1025。
- 与保存配置一致：1920×462、传输旋转90°。
- 先发送预编码红/蓝JPEG，排除视频解码开销。
- 再播放真正的MP4/H.264：1920×462、编码30FPS、90张不同图案、约3秒循环，包含可见帧号与时间标记。该样本与上一轮每250ms变化一次的离线夹具不同，本轮原图变化频率为30FPS。
- 素材生成时重新运行视频专项19项，通过；日志 artifacts/video-hardware-fixture-test.log。素材目录记录于 artifacts/video-hardware-fixture-location.txt。

## 主机侧测量

预编码JPEG每档8秒：

| 请求FPS | 完整帧数 | 完成FPS | 写入p95 ms |
| --- | --- | --- | --- |
| 1 | 8 | 1.00 | 6.90 |
| 2 | 16 | 2.00 | 9.22 |
| 5 | 39 | 4.87 | 10.07 |
| 10 | 74 | 9.23 | 7.65 |

真实MP4连续解码、JPEG编码和HID发送，每档10秒：

| 请求FPS | 完整帧数 | 完成FPS | 取帧p95 ms | 编码p95 ms | 写入p95 ms | 平均JPEG |
| --- | --- | --- | --- | --- | --- | --- |
| 5 | 49 | 4.90 | 9.48 | 41.69 | 21.76 | 119130 bytes |
| 10 | 92 | 9.20 | 9.29 | 41.34 | 21.37 | 119001 bytes |

累计137张基线JPEG、141张真实视频帧。两轮工具退出码均0，各档Failed=false；未记录传输失败。settings.json测试前后SHA256一致。

以上FPS是主机完成完整JPEG写入的计数，不是精密测得的面板刷新率。分项p95来自不同帧，不能直接相加当作整帧p95。

## 证据与目视

本次完整证据目录的绝对路径记录于 artifacts/video-hid-validation-location.txt；其中包含 preflight.json、postflight.json、transport-plan.json、transport/result.json、video-plan.json、video/result.json、日志及summary.json。保留工具原始PhysicalDisplayConfirmed=false，人工反馈另外存放，不篡改工具结果。

已发起目视确认：画面是否横向完整、文字方向正确，帧号与图案是否持续变化并循环，有无黑屏、花屏或停住。回复完成后在本节追加。

## 当前结论与边界

主机链路已在实体D92完成真实视频短时发送，约9.2完整帧/秒，无传输失败。这支持继续开发正式视频功能；仍需结合人工反馈确认实体显示结果。

尚未进行30分钟/2小时长测、视频运行时拔插恢复、睡眠唤醒、真实高码率长视频、多设备视频并行与30/60FPS验证。没有提高原有10FPS请求上限。

当前安装版仍为20261027.4，没有开放“使用视频”入口、视频组合主题或视频轮播。本次属于原型实机短测，不代表正式客户端能力已交付。

## 重新播放及人工验收通过
应用户要求，重新播放60秒：5FPS请求30秒完成141帧（4.70FPS），10FPS请求30秒完成276帧（9.19FPS），各档Failed=false，工具退出0，设置SHA256一致。
用户确认：方向和画面正常，持续变化循环，无黑屏花屏。该反馈保存为独立user-confirmation.json，不改写原始工具PhysicalDisplayConfirmed=false字段。该反馈也不表示精确测量面板刷新率。
证据目录：artifacts/video-hid-repeat-147bfb3065504ffbbe1e76e475255224（位置指针artifacts/video-hid-repeat-location.txt）。本次实体视频短测与目视验收通过；长测、拔插、休眠、多设备和正式客户端接入仍待完成。
