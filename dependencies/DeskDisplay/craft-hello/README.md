# D92 开发体验插件

这是独立的 Craft 插件工程，使用 Vue + Canvas，不属于 WPF 应用构建。

## 体验
1. 运行开发服务，然后重启 Craft 以发现新插件。
2. 找到「D92 开发体验」，拖入「你好，D92」。
3. 检查秒数、修改文字与颜色、拖动调整尺寸。
4. 移除并重新添加组件，检查显示正常。

开发预览：http://127.0.0.1:5173/preview.html

开发服务必须保持运行；正式打包前先完成 Craft 实机验证。
常规命令：pnpm install、pnpm run dev。npm 环境也可使用 npm install、npm run dev。

## 文件
- src/render.ts：预览与插件共用绘制函数
- src/plugin/actions/action1.ts：每秒刷新及实例清理
- src/pages/actions/action1.vue：属性设置
- src/manifest.cjs：插件名称、默认值

默认 600×240 是组件初始尺寸，绘制始终使用 Craft 提供的 ActionGeometry。
只向 com.mirabox.streamdock.deskdisplayhello.mPanelPlugin 目录安装。