# 创作中心属性区整理

日期：2026-10-25。

仅调整CreatorView.xaml的属性展示顺序与折叠分组，不改模型、渲染、设备或保存逻辑。

- 外观（颜色/透明度）移到时钟设置之后、文字样式之前，减少寻找常用颜色的滚动距离。
- 图片设置首先显示替换、填充与裁剪入口；精确裁剪位置、重置和亮度归入裁剪与亮度，路径归入素材信息。
- 保留原外观/文字样式/图片设置/位置与尺寸等展开组名称，以及原Command、Click、Binding和IsEnabled控制。新增组默认折叠，已有工作区展开状态可继续识别。
- 所有参数与保存、撤销逻辑不变。图层锁定、多选属性与插件入口保持原有行为。

构建0警告0错误；creator-usability专项51项通过，日志artifacts/inspector-ux-build.log、artifacts/inspector-ux-tests.log。实际WPF测试截图creator-acceptance-landscape-100.png已查看，为隔离测试画面，不代表实体D92验收。上轮完整Windows921通过，本轮仅XAML分组调整后运行适用专项，未重复全套。

已打包0.1.0-preview.20261025.2，包artifacts/releases/D92Studio-0.1.0-preview.20261025.2-win-x64.zip，SHA256 1A936FDF2144D9CD0C71CBC9D44E660D2731F5F0B26BD8115A3E7659579A9954。

用户确认保存关闭后，等待旧进程正常退出，已安装并打开0.1.0-preview.20261025.2。settings.json安装前后SHA256相同、旧app备份保留；新进程2852响应正常，安装记录artifacts/inspector-ux-installed.json。没有强制终止或丢弃编辑。

