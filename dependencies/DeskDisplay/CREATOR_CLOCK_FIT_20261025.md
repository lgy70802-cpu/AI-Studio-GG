# 数字时钟边框贴合

日期：2026-10-25。

- 新建数字时钟不再使用画布80%宽度；依据当前字体、字号、字重、数字固定advance、字符间距和时间格式计算边框，并在当前画布居中。窄画布的新时钟会先等比减小字号，现有主题不自动变更。
- ThemeTextMetrics.MeasureClock与SceneView.Theme的固定数字advance规则一致，使用AM/PM两种字符串预留较宽后缀，并留少量字体/阴影边距。没有按秒自动改框。
- 右侧时钟设置增加适应内容，保持原边框中心与旋转，复用Change事务，支持撤销。锁定图层禁用；越过现有几何上限时拒绝并提示，不产生无效源项目。
- 不改现有四角默认等比/Shift自由缩放行为，也不改Core主题格式、HID或保存模型。

验证日志：artifacts/clock-fit-build.log、artifacts/clock-fit-regression.log。构建0警告0错误；完整Windows回归925项通过，退出码0。新增检查覆盖新时钟紧凑边框、旋转中心保留、走秒不变宽以及单次撤销恢复。


包：artifacts/releases/D92Studio-0.1.0-preview.20261025.3-win-x64.zip，SHA256 2C9D308BEED81913A669E218F0A17E022ED315E35DB1EF47A237AAE2C7A95E7B。尚未安装，等待用户保存关闭当前客户端；不强制终止用户编辑。

安装跟进：已随20261025.4安装打开，包含十款模板；无需单独安装.3。settings SHA安装前后相同，用户原主题未覆盖。
