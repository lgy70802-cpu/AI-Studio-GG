# 请求 30 FPS 视频实机测试

2026-10-28，用户明确要求直接测试30 FPS，并确认退出客户端。使用已验证D92 C511D3782B05、1920×462、旋转90°、1025字节Report。原有HID协议、整帧串行发送、心跳、设备租约和超时均保留。

## 结果

| 指标 | 结果 |
|---|---:|
| 请求FPS | 30 |
| 时间 | 60.0302秒 |
| 完整JPEG发送数 | 692 |
| 实际主机完成FPS | 11.5275 |
| 平均JPEG大小 | 119030字节 |
| 取帧平均 / P95 | 15.81 / 31.69 ms |
| 合成、旋转及JPEG编码平均 / P95 | 45.81 / 86.63 ms |
| HID写入平均 / P95 | 21.50 / 58.92 ms |
| Failed / Error | false / null |

素材为30FPS不同图案和帧号、约3秒循环的真实MP4；不是重复发送一张JPEG。用户回复画面“正常”，独立保存user-confirmation.json。没有精密测量面板实际刷新率，不宣称已支持30 FPS。原始结果中的PhysicalDisplayConfirmed、HighFrameRateValidated不自动改写。

同素材先做5秒离线预检：58帧 / 5.02秒，约11.6FPS。当前串行取帧、CPU读回、WPF合成旋转和JPEG编码链有明显处理成本；离线和实机处理速率接近不能证明USB可达30 FPS，也不能认定硬件极限为11.5FPS。下一步需优化帧处理，并用独立预编码JPEG基线隔离传输上限后重新测量。

## 可追溯证据

- 实机：`artifacts/video30-hardware-c02740c26ddf4cffab96d65734538118/result.json`、plan.json、user-confirmation.json。
- 离线：`artifacts/video30-offline-95f40c47430f4465b04a5d2d0bde8b7d/result.json`。
- 日志：`artifacts/video30-hardware.log`、`artifacts/video30-regression.log`。

仅诊断计划通过ExperimentalHighFrameRate显式允许最高30FPS；默认输出仍拒绝小于100ms的间隔，客户端安装版最高请求5FPS不变。新增默认限速及诊断30FPS上限回归检查。未修改用户设置，settings SHA256前后一致；没有重新安装客户端。本轮不含拔插、长测、睡眠、多设备或macOS验证。
