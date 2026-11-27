# D92 Studio 整体功能与体验优化验收（2026-10-21）

发布版本：0.1.0-preview.20261021.10。沿用 C# / .NET 8 / WPF / MVVM 与现有场景、资源库和输出服务。本轮没有改 HID 报文、驱动、固件或用户配置，没有发送实体 HID 命令。

## 八项实施结果

| 项目 | 本轮实际工作 | 验证与边界 |
|---|---|---|
| 1 性能 | 首页推荐、在线缩略图和创作封面改为后台解码；并发4、最长边960、共享缓存最多72项/估算64MiB；卸载释放控件引用；资源库轮播过滤使用字典；素材筛选缓存；项目与草稿JSON后台解析 | 真实图片解码、缓存复用、损坏后重试及UI线程调度通过。缓存预算仅限新增预览缓存，不是进程总内存上限 |
| 2 创作操作 | Shift+方向键移动10设计单位、Ctrl+D复制；补充画布悬停操作提示；平移、图层拖动、裁剪检查鼠标捕获失败，失去捕获结束平移 | 保留默认等比、Shift自由缩放、Alt绕过吸附；当前桌面不能取得原生鼠标捕获，已验证失败时安全复位，真实手感需人工检查 |
| 3 保存恢复 | 关闭等待草稿任务；草稿文件名与项目ID一致性校验，异常草稿保留并显示提示；读取解析移出UI线程 | 真实草稿恢复为新ID副本，正式源保留；损坏与身份不匹配文件不删除；沿用原子写入、源历史和素材修复 |
| 4 资源体验 | 取消请求的分类/标签不再提前提交；后续刷新纳入等待任务；读取过程中搜索可合并为最新请求；详情返回恢复滚动位置；下载失败不误报为列表连接失败 | 快速切换的延迟响应专项、真实空状态、横竖资源、导航/详情恢复与500份资源三屏分页通过 |
| 5 下载更新 | 同一失败任务原位重试；汇总进行中/完成/待重试；清理结束记录不删除资源；校验后导入前再次检查取消；下载并显示按钮遵守USB可用性 | 成功/失败/重试与本地导入通过；沿用SHA256/长度校验、当前页更新及取消；任务记录为本次会话，不声称断点续传 |
| 6 轮播 | 主题轮播显示当前主题、下一候选、倒计时，增加暂停/继续自动切换；暂停时实时场景继续更新、可手动下一项；主轮播页显示主题轮播状态 | 模拟Windows输出、真实DispatcherTimer既有检查和新增暂停恢复检查通过；下一候选可能被兼容性检查跳过；不声称USB实机长测通过 |
| 7 素材接入 | 创作素材随原资源库下载/修改刷新；显示本地/云端/缺失/动态USB限制；满100层时保留素材窗口并给出提示 | 已下载云端图加入可编辑主题保持CloudResourceId；不新增资产数据库；时钟和插件仍使用现有组件入口，不伪造云端插件能力 |
| 8 云端联调 | 启动已有本地API与真实PostgreSQL；验证横竖列表、预览、哈希下载、导入、版本替换及本地名称/收藏/轮播身份保留；数据库API测试覆盖上传发布、用户收藏、权益授予/过期/撤销与私有下载保护 | 本机静态壁纸链路通过；没有真实支付/生产云服务/云端时钟主题插件分发验收；测试直接建立临时权益，不代表购买完成 |

## 验证记录

- 改造前Release构建：0警告/0错误。既有完整回归在离屏窗口的鼠标捕获断言处失败，未把基线称为全绿。
- 修复鼠标捕获失败的应用状态处理，测试明确区分可捕获与安全复位；日志保留 `BOUNDARY: Native mouse capture unavailable`，不把降级路径冒充真实拖动验证。
- 最终应用Release构建：0警告/0错误，`artifacts/client-experience-final-build.log`。
- 完整DisplayTests：623项PASS，退出0，包含本地真实API检查；`artifacts/client-experience-regression.log`。
- 新增体验专项：21项PASS，退出0，`artifacts/client-experience-module.log`。
- 完整回归后仅增加截图等待/真实缩略图断言；Studio专项最终30项PASS，退出0，`artifacts/client-experience-live.log`。500份资源本机样本27ms，属于隔离数据与本机时序，不承诺生产所有设备速度。
- 配置SmokeTests通过：`artifacts/client-experience-smoke.log`。
- 后端原仓库未修改源码或.env；真实数据库集成1项（含多条HTTP/权益断言）、单元6项、workspaces类型检查通过。日志在 `E:/Codex/MLB/d92-studio-admin/.test-postgres/client-experience-{integration,unit,typecheck}.log`。
- 发布日志：`artifacts/client-experience-publish.log`。

## 本轮文件

- 新增 `src/DeskDisplay.App/Views/AsyncPreviewImage.cs`、`tests/DeskDisplay.DisplayTests/ClientExperienceTests.cs`。
- 界面：HomeView.xaml、OnlineResourcesView.xaml、CreatorView.xaml/.cs、CreatorViewport.cs、ThemeCanvas.cs、CreatorCropEditor.xaml.cs、CreatorMaterialBrowser.xaml/.cs、CreatorThemePlaylist.cs、CarouselManagementView.xaml。
- ViewModel：CloudLibraryViewModel.cs、LibraryViewModel.cs、WallpaperItemViewModel.cs、CreatorExperience.cs、CreatorViewModel.cs、CreatorProjectManagement.cs、CreatorThemePlaylist.cs。
- 持久化：ThemeDraftStore.cs、ThemeProjectStore.cs。
- 测试入口与现有验收：Program.cs、CreatorNavigationTests.cs、StudioApiTests.cs。
- 本清单仅列本轮涉及文件；仓库原有其他未提交修改保持，不代表它们属于本轮。

## 截图

均来自实际WPF控件与隔离测试源，非产品背景效果图；`tests/DeskDisplay.DisplayTests/bin/Release/net8.0-windows10.0.19041.0/`：

- `client-experience-playlist.png`：主题轮播当前项/下一候选/倒计时与暂停按钮。
- `client-experience-materials.png`：真实下载素材及来源提示。黑色图片为测试生成的有效PNG，不是缺失素材假图。
- `online-download-management.png`、`online-portrait-optimized.png`：已有本地服务器明确标记的验收资源，等待真实缩略图加载后截图。

## 使用入口

- 下载与更新：在线资源 → 下载管理；失败记录重试、清除已结束记录。详情也可下载/取消；更新入口仍为“更新当前页资源”。
- 主题轮播：首页 → 轮播管理 → 主题轮播；添加已保存项目，选择输出目标，开始前进行能力确认；暂停切换保留当前实时输出。
- 素材：创作中心 → 选择素材；使用现有库中的本地或已下载云端素材。动态素材明确不适用于D92 USB。
- API使用原配置，本机验收服务为 http://127.0.0.1:3000；数据库按原 scripts/local-database.mjs 启动，未重设凭据。

## 后续仍需人工/外部环境验收

1. 实体D92组合主题轮播长测、物理拔插日志、休眠唤醒与占用。
2. 真正交互桌面中的长时间拖动/捕获与125%/150%多DPI跨屏；当前捕获不可用环境只能确认安全复位。
3. 生产TLS/云存储/真实账号部署；当前成功范围是本地静态壁纸分发，不等于完整商城及云端插件分发。
4. 大量互不相同图片、不同磁盘/网络和长期内存表现，不能用500条引用同一测试文件的筛选结果替代。

## 发布与启动

- 自包含Windows包：`artifacts/releases/D92Studio-0.1.0-preview.20261021.10-win-x64.zip`。
- SHA256：`14811E75D78E1A0569DA170230701FD9D4FBF153655EC6667D3C984C6C85D031`。
- 正常关闭旧版9580后启动新版3372，窗口标题DeskDisplay且Responding=True；未强制终止进程。
- 发布脚本使用PowerShell 7执行。Windows PowerShell 5.1因缺少Path.GetRelativePath不能完成清单生成；本轮改用现有7.6.5完成，无需重装或覆盖配置。

## 后续补验（2026-10-21）

实体D92现已完成横屏6分钟/338帧（含物理拔插恢复机器证据）与竖屏2分钟/120帧短测，用户确认视觉与秒数正常。详见D92_HARDWARE_ACCEPTANCE_20261021.md。本补验不涵盖小时级长稳、休眠/占用或竖屏再次拔插，原先的环境边界继续保留。
