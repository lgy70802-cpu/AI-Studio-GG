# 创作中心素材入口与使用流程验收

日期：2026-10-25。

## 实现

- 将左侧壁纸/我的素材合并为图片，继续读取现有LibraryAssets，保留搜索、方向、来源、两级分类和导入排序，不新增资产数据库。
- 导入按钮明确为导入到素材库；点击资源缩略图添加普通Image图层，保持新图层置顶。独立选择背景图片继续复用原InsertImageFilesAsync背景路径，遵守锁定与撤销规则。
- 旧工作区记录中的壁纸/我的素材映射到图片入口，不重排或改写已保存主题图层；旧MaterialTab程序调用仍可识别。
- 设备预览增加看效果、选设备、检查并应用三个步骤。首步隐藏设备表单和应用按钮，第二步配置目标，第三步显示现有应用命令。
- 返回或点击步骤只切换界面显示，保留同一ViewModel/Scene/目标与适配参数。实际应用仍由原能力检查、目标确认、会话冲突与独立结果机制控制；没有自动输出。
- 无设备及视频继续原预览回退路径；D92 HID不支持VideoScene的边界不变。保存仍是编辑器的独立操作，进入流程不自动保存源项目。

## 验证状态

构建0警告0错误，完整Windows回归921项通过（退出码0）。设备流程专项重跑通过，截图已核对；测试截图更新前补充UpdateLayout，避免捕获步骤切换中间帧。日志：artifacts/material-ux-build.log、artifacts/material-ux-regression.log、artifacts/material-ux-device.log。

新增自动化检查覆盖统一素材入口、旧入口映射、真实背景入口，以及三步可见性、实际应用命令和返回后目标/适配参数保留。旧图片名称选择测试改为按真实素材ID定位，以继续验证图层选择行为。

没有新增实体HID验收。未提交或推送，原有工作区变更保留。

## 安装

已安装并打开0.1.0-preview.20261025.1，安装settings.json的SHA256前后相同，旧app备份保留。记录artifacts/material-ux-installed.json。包见artifacts/releases/D92Studio-0.1.0-preview.20261025.1-win-x64.zip，SHA256 C5A05A25D410375B695302765C2BC764CDB8742935EE914BFEDA1BEC82160DEE。截图来自隔离WPF测试，不是实体屏验证。

