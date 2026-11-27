# 十款数字时钟模板

日期：2026-10-25。

新增极简白、午夜蓝、薄荷绿、奶油暖色、珊瑚粉、琥珀终端、紫色夜航、专注分栏、日历卡片、清晨青蓝十种设计。每款含独立横竖布局，在原模板库中提供横/竖两个入口，共新增20个模板卡片。

入口：创作中心→从模板创作。复用已有真实场景预览、复制源项目、图层编辑和保存机制，不新增资产数据库。数字时间/日期/星期读取系统时间，背景与强调线/卡片为可编辑形状；无外部素材与插件依赖。原模板和用户自定义模板保留。

数字时钟采用Consolas及紧凑容器；时间格式含24小时、带秒、12小时制等，按实际字体度量检查边框。位置与尺寸均按逻辑画布宽高计算，横竖布局分别排版，没有旋转横屏充当竖屏。

新增源码：src/DeskDisplay.Core/Models/DigitalClockTemplates.cs。注册：ThemeTemplates.cs。自动化验证及总览截图生成：TemplateLibraryTests.cs。

构建和回归日志：artifacts/digital-templates-build.log、artifacts/digital-templates-regression.log。最终构建0警告0错误，专项159项通过，完整Windows回归1046项通过（退出码0）。实际WPF总览图artifacts/digital-clock-templates.png已核对。新版包20261025.4已打包，包含此前20261025.3时钟边框修复；SHA256 16547103BAF157BB546C415726BEF246DAD5E1EA5BCBFA3D34CF4B2C856B01E1。尚未安装，当前旧版进程2852仍在运行，保留此前保存关闭请求，待用户确认后一次安装最新版，不强制关闭编辑。此次无新增HID实体输出验收，不扩大既有视频能力结论。


## 安装完成

用户确认保存关闭并等待旧进程正常退出后，已安装并打开0.1.0-preview.20261025.4。settings.json安装前后SHA256相同，旧app备份保留，安装记录artifacts/digital-templates-installed.json。界面版本已核对。本次包含.3边框修复与十款模板，不需要单独安装.3。
