# 当前 UI 迁移标准

生效日期：2026-11-05，以用户最新上传的单文件 HTML 为唯一视觉与交互主基准。

## 权威输入

- 原文件：`C:/Users/Administrator/Desktop/D92 Studio 多设备原型 V6.3.html`
- 原样存档：`v63-html-authoritative/Prototype.html`
- SHA256 与本轮修改前备份位置：`v63-html-authoritative/source.json`
- 以前的 React ZIP、旧 HTML 和截图只用于历史对照；有冲突时服从本文件。

## 转换规则

读取最终 CSS（包括后续覆盖）以及实际页面函数，逐项转换成 WPF。保留 .NET 8、MVVM、现有资源、主题、时钟、视频、插件及设备会话服务。不复制原型的模拟连接、模拟成功和假资源数据。

原型模拟尺寸、主题设计坐标、设备真实输出尺寸分别处理。不得为视觉对齐修改 HID 协议、心跳、分包和硬件能力限制。

## 已核对的基础尺寸

- 标准桌面侧栏 204px；1100px 以下原型侧栏 170px。
- 顶栏 74px；页面左右 32px、顶部 28px。
- 正文 14px；页面标题 26px；区块标题 18px。
- 导航左右内边距 15px，上下 11px，行间 margin 3px。
- 首页单设备与多设备分别由 overviewSingle / overviewMulti 定义，不能同时叠加呈现。

## 验收状态

正在转换，尚未完成全页视觉验收。构建、自动化检查与真实窗口截图分别记录，不能相互替代。
