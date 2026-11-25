# 主窗口拖动与缩放修复（2026-10-24）

## 原因与改动

真实 HWND 的 WM_NCHITTEST 查询复现：原窗口左边缘返回 HTCLIENT=1，而非 HTLEFT=10。SidebarNavigation 声明 IsHitTestVisibleInChrome=True，并覆盖到缩放带，导致左边缘被客户区控件拦截。右边缘、底边和标题空白的基线结果分别为11、15、2，不能据此断言所有拖动功能都被关闭。

同时旧 MinWidth=1100、MinHeight=740 限制缩小范围。保留 WindowStyle=None、ResizeMode=CanResize 和原 SystemCommands 绑定，未改成不可缩放窗口。

MainWindow.xaml 调整：

- 根 Border 增加7 DIP内边距，加原1 DIP边框，为四边留8 DIP专用缩放带，交互导航不再覆盖。
- WindowChrome.ResizeBorderThickness=8，CaptionHeight=56，与新增边距及原48 DIP标题行一致。
- UseAeroCaptionButtons=False，继续使用原自定义系统按钮；标题栏命名并显式透明背景。
- 最小尺寸降低到960×640，初始1320×900不变。仍有合理下限，不宣称可以缩小到任意尺寸。

拖动请使用最顶部标题栏空白处；页面内部不是窗口拖动区。最大化时应先还原，再从边缘调整大小。

## 验证

- 新增 MainWindowChromeTests：创建独立真实WPF窗口，原生查询标题、四边、四角；验证1000×680尺寸、系统按钮客户区命中、最大化及还原。共13项。
- artifacts/chrome-before.log：旧左边缘=1，基线工作区1920×1040，窗口1320×900。
- artifacts/chrome-after.log：修复后八方向正确、标题=2。最终含按钮命中检查的13项并入完整回归；当前环境显示比例150%，几何查询使用真实屏幕像素。
- artifacts/chrome-regression.log：完整875项通过，退出码0。
- artifacts/chrome-build.log：0警告0错误。

测试没有自动拖动用户窗口或改变系统DPI，不能把原生命中与尺寸检查扩称为人工鼠标拖拽验收。没有修改HID协议、驱动、固件或新增设备输出测试。

## 交付

版本0.1.0-preview.20261024.4，ZIP：artifacts/releases/D92Studio-0.1.0-preview.20261024.4-win-x64.zip。

SHA256：69FE96F029A26B04C3BC331DF86C703BCD373CCFD6CB52FAD66482271C35AD23。

已正常退出旧窗口、安装并打开固定目录版本；控件树核对版本及D92已连接，进程16432 Responding=True。settings.json在成功安装前后SHA相同，见artifacts/chrome-installed.json，旧app备份保留。

首次更新时在暂存校验尚未结束前提前启动了旧版，文件占用导致Move-Item拒绝替换；原app完整保留。正常关闭该旧进程，等待安装完成后再启动，重试已成功。没有强杀、丢弃编辑或覆盖用户配置。启动工具曾短暂未发现窗口，重新枚举后确认正常打开。

修改文件：src/DeskDisplay.App/Views/MainWindow.xaml、tests/DeskDisplay.DisplayTests/MainWindowChromeTests.cs（新）、tests/DeskDisplay.DisplayTests/Program.cs。未提交或推送，保留原有工作区变更。
