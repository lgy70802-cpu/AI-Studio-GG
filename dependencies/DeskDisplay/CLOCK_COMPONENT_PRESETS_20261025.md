# 编辑页数字时钟组件与个人样式

日期：2026-10-25。

## 功能入口

创作中心编辑页→时钟：十张两列卡片（极简等宽、纤细无衬线、浑厚数字、优雅衬线、窄体数字、宽距数字、数码管、大时分小秒、十二小时制、竖排时分）。这是单个组件入口，区别于之前的整套主题模板，不替换当前项目、背景与其他图层；插入沿用置顶选中与单次撤销。

数码管为真实矢量段码；小秒与竖排由同一SceneView渲染，不是固定JPEG或预览专用效果。仍使用ThemeLayer.GetText系统时间、既有Scene→WPF→JPEG输出链路，不修改HID协议。数码管字形固定，字体控件禁用且说明可调字号/颜色/间距。其他字体使用Windows现有字体与字重/字宽参数；不下载或分发字体文件。

右侧时钟设置可切数字样式、时间格式、秒钟、12小时制、字重和字宽。更改格式/字号后可适应内容。原四角缩放/旋转/字体颜色与保存机制保留。

## 自己创作

1. 在左侧添加一个数字时钟。
2. 调整数字样式、字体、字号、字重、字宽、颜色、透明度、间距及时间格式。
3. 右侧样式名称（沿用图层名）输入名称。
4. 点击保存为我的时钟样式。
5. 左侧展开我的时钟样式，点击名称即可在当前或其他主题中加入新时钟。

个人样式保存在APPDATA/DeskDisplay/Themes/ClockStyles/*.clock.json。只保存组件配置，不包含背景/其他图层/素材引用/设备绑定/当前输出；插入时生成新ID并按当前画布测量与居中。移除个人样式不删除主题已有图层。最多100款，文件按GUID写入、原子替换，坏文件保留并跳过。测试使用临时目录，不写用户数据。

整套主题的自定义模板功能仍保留，与个人时钟组件分开。

## 数据与兼容

ThemeLayer增加可选默认字段ClockAppearance（Standard/SevenSegment/SmallSeconds/Stacked）、FontWeightValue（默认400）、FontStretchValue（默认5）。旧主题未包含字段时保持旧效果。V1/V2配置校验同步检查枚举与范围，Clone、撤销和JSON配置保留参数。新样式需新版Windows渲染器；macOS尚未移植这些绘制效果，不能宣称旧Mac客户端已支持。

新增模型ClockComponentPresets.cs；WPF绘制DigitalClockDrawing.cs、ThemeTextMetrics与SceneView.Theme接入；CreatorClockPresets/CreatorSavedClockStyles及ClockStyleStore负责界面/个人配置持久化。原设备协议与会话生命周期未改。

## 验证

构建与日志：artifacts/clock-components-build.log。专项artifacts/clock-components-special.log，检查真实JPEG差异、插入保留主题、紧凑测量、保存源字节不变、序列化、撤销、重新打开样式、独立实例、锁定、删除不影响画布、坏文件和实际XAML绑定。构建0警告0错误；组件专项69通过、完整Windows1115项通过（退出码0），见artifacts/clock-components-regression.log。实际WPF截图artifacts/clock-component-editor.png已核对。

此次没有实体D92新样式目视验收；离线渲染不等于已验证屏幕实际效果。已安装并打开0.1.0-preview.20261025.5（安装时客户端已退出，无强制关闭）；settings SHA前后一致、旧app备份保留，见artifacts/clock-components-installed.json。新进程5388响应正常。包ZIP的SHA256为507980B524B9ACD96548ADCA621D910B35FAC3A14CD11744DE289CE535598212。

