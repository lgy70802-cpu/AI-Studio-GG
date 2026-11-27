# 小屏宝 HTML 插件 API v1

在插件中心选择「导入插件」，选中含有 `manifest.json` 的目录。可以直接导入 [示例时钟](../examples/pixbuddy-clock)。

- 清单须包含 `PixBuddyApiVersion: 1`，以及名称、版本、作者、描述和包内 HTML 入口。沿用现有副屏操作清单，声明唯一的操作 UUID、`SecondaryScreen` 和默认画布尺寸。示例提供完整字段。
- HTML 在 `window` 上监听 `pixbuddy-start`。事件的 `detail.ActionGeometry` 提供目标画布的宽高，`detail.hour12` 提供已保存的时间制式。`Settings` 中的布尔值、字符串和数字会出现在配置表单中，并在启动时通过同一个事件传入。
- 使用 `window.pixBuddy.submitFrame(canvas.toDataURL('image/jpeg', 0.85))` 提交与目标画布同尺寸的画面。也可提交 PNG。宿主最多接收每秒 10 帧。
- 依赖和素材随插件目录交付。配置变更后，宿主会重新启动设备上的插件实例。

Craft 使用自己的入口和消息协议。当前覆盖单一 SecondaryScreen 操作、HTML SDK 1 / API 1.0、初始化、会话内设置读写和 JPEG／PNG 图像消息。Woodland Clock 的时间制式由小屏宝保存。Craft 的独立属性面板和二进制帧协议尚未接入。
