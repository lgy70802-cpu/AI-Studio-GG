# D92 内容契约 V1（2026-10-24）

本文固定当前源码已实现的格式，供Windows、Mac与Web任务共同使用。它不是新商城、通用插件安装器或新HID协议。

## 三种独立版本

| 对象 | 版本及序列化 | 当前行为 |
|---|---|---|
| REST目录 | `/api/v1`，camelCase | 当前仅分发已发布STATIC_WALLPAPER；PNG/JPEG原文件 |
| 可编辑源 | ThemeProject.Version=1或2，PascalCase，枚举为整数 | 1含Landscape/Portrait；2增加LayoutVersions/SelectedLayoutId；未知版本拒绝 |
| 本地主题包 | manifest.json Version=1，PascalCase | ZIP内project.theme.json、manifest.json及assets/；与源Version独立 |

不得把ThemeDocument的适配/输出契约版本当成源项目或ZIP清单版本。源码为实际权威：Core/Models/ThemeProject.cs、Core/Themes、Library/ThemeTransferService.cs。

## 资源目录与下载

沿用D92_STUDIO_CLIENT_API_CONTRACT.md的账号、目录和权限规则。当前清单至少含id/name/type/version/sha256/byteSize/mimeType/downloadUrl/previewUrl/thumbnailUrl/categories/tags/updatedAt；附带width/height、accessPolicy、minimumClientVersion、supportedDevices等字段。

- id为非空UUID，type必须STATIC_WALLPAPER；mimeType仅image/png或image/jpeg，最大25MiB。SHA256是原下载字节，不是缩略图。
- 三个素材路径固定为`/api/v1/client/resources/{id}/{download|preview|thumbnail}`；拒绝绝对URL、协议相对URL、跨资源ID、遍历和未知路径。未来签名外链需另立契约，不能悄悄放宽。
- accessPolicy仅FREE/VIP/PURCHASE/PURCHASE_OR_VIP；未知值拒绝，不默认免费。客户端本地标记不授予服务端下载权。
- 非空minimumClientVersion必须可解析为.NET数字版本；资源产品version独立，不能拿资源版本比较客户端版本。
- 下载前重新检查客户端/设备声明，下载后验证长度/SHA；失败只清理本次临时文件，损坏缓存须重新下载。
- 当前真实上传/发布/下载链路仅免费静态图通过；VIP/付费购买、云端主题/动态包/插件安装均不列为已完成。

## 可编辑主题

- 一个Theme含多个独立Layout。尺寸为设计单位，不代表设备像素；按实际目标宽高与布局容差选择/适配，不能固定所有素材为4:1或把2557×615等同4:1。
- 图层保留唯一Id、Kind、X/Y/Width/Height、ZIndex、Visible、Locked、Opacity、Rotation、GroupId/GroupName、素材与组件配置。
- ThemeElementKind数值：0 Wallpaper、1 Image、2 DigitalClock、3 Date、4 Weekday、5 Text、6 Rectangle、7 Ellipse、8 StockPlugin、9 CameraClock。ThemeOrientation：0 Landscape、1 Portrait。ThemeAssetOrigin：0 Unknown、1 Local、2 Cloud。
- 文字可选VerticalAlignment：0 Top、1 Center、2 Bottom，缺省Center；AutoGrowTextHeight缺省false。文字、字体、字号、对齐、字间距等参数可恢复；数字时钟使用真实系统时间。
- AssetId是本机资源库身份，不跨电脑当作云端身份；CloudResourceId/AssetOrigin保留云端来源，不能用重命名或重新选择文件绕过授权。Path是路径后备引用，裸源中的Windows绝对路径不保证在Mac可用。
- Mac可共享Core与契约规则，不能直接复用WPF字体度量、Windows句柄或驱动；新文字排版仍需Mac视觉回归。

## `.d92theme`本地包

根目录必须且仅包含project.theme.json、manifest.json与清单列出的assets文件。文件名为`assets/{lowercase-SHA256}.{png|jpg|jpeg|mp4}`。manifest为`{Version:1, ProjectId:UUID, Files:[{Path,Bytes,Sha256}]}`。

清单ProjectId须与源Id一致，每个条目大小/哈希须匹配；单素材最多128MiB、总素材512MiB、JSON最多2MB、条目数/重复名/绝对路径/遍历按现有导入器拒绝。导入产生新项目Id与独立目录，不覆盖已有项目。文件仅数据，不执行内容。

云端素材不嵌入包，仅保留身份引用并统计遗漏；另一台电脑需从被授权的来源重新解析。不能用本地导出包代替付费资源分发或离线许可证。

## 插件依赖与未来边界

当前只有内置StockPlugin与CameraClock组件，主题保存其Kind与配置，运行时必须安装且启用对应内置组件。不把Craft脚本/ZIP、任意DLL或下载文件自动执行；包里不含插件可执行代码。

未来外部插件分发必须另行确定并实现插件ID、包版本、最低客户端/平台/设备能力、文件清单哈希、签名格式与可信密钥、权限清单、隔离及撤销策略。签名规范和外部执行器尚未实现，本文不猜测算法或用“有SHA”冒充发布者签名。

## 共同验收

1. 服务端上传并发布真实图像，客户端取回同一资源ID、大小与SHA。
2. 下载损坏缓存后可恢复，云端身份在本机库与主题保存中保留。
3. 背景加时钟/文字仍可编辑，输出JPEG随时间变化，导出不会夹带云端原文件。
4. 离线JPEG生成不等于实体D92显示验收；多设备/设备断开及Mac分别验证。
5. 后端当前拒绝的资源类型保持拒绝，外部插件仍不可执行。
