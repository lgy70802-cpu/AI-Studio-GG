# D92 Studio Windows / macOS 客户端 API 约定

版本：当前实现快照 1.0，2026-10-13。本文对照 Studio 服务端源码和 Windows 客户端编写；描述已实现行为，不将后续计划当成可调用接口。API 路径版本为 `/api/v1`。

## 连接与通用规则

- 基址是服务器 origin，例如 `https://studio.example.com`；以下路径都从 origin 根目录解析。示例域名不代表已部署服务。
- 同机开发基址为 `http://127.0.0.1:3000`。Windows 客户端仅允许 HTTPS，或 loopback 主机的 HTTP；跨电脑必须配置实际 HTTPS 地址。
- 当前 Nest 服务仅监听 `127.0.0.1`，不能直接通过 Windows 局域网 IP 访问。要让另一台 Mac 连接，需另行部署 HTTPS 反向代理/服务器，或使用开发隧道；不要把 Mac 的 localhost 当成 Windows。
- JSON 字段使用 camelCase；日期为 ISO 8601；UUID、SHA-256 和资源版本都是字符串。成功响应直接返回业务对象，没有统一 `data` 外壳。
- 原生客户端请求不需要 Origin；浏览器写请求只接受配置的 `ADMIN_ORIGIN`。普通用户使用 `Authorization: Bearer <token>`，管理员使用独立 Cookie `d92_admin`，两者不能互换。
- GET 正常为 200；POST 注册、登录、退出、收藏正常为 201；DELETE 收藏正常为 200。POST JSON 使用 `Content-Type: application/json`。
- 参数验证使用 whitelist + forbidNonWhitelisted；不要发送未约定字段。当前 DTO 中共用的 `type`、`view` 不是客户端目录的有效功能开关，客户端不要依赖它们。

## 接口目录

| 方法 | 路径（均在 `/api/v1` 下） | 身份要求 | 成功响应 |
|---|---|---|---|
| GET | `/health/live` | 无 | `{ "status": "ok" }` |
| GET | `/health/ready` | 无 | `{ "status": "ok", "database": "up" }`；数据库不可用返回 503 |
| POST | `/client/auth/register` | 无 | UserSession |
| POST | `/client/auth/login` | 无 | UserSession |
| GET | `/client/auth/me` | Bearer | UserProfile |
| POST | `/client/auth/logout` | Bearer | `{ "loggedOut": true }` |
| GET | `/client/resources` | all 无；favorites/owned 需 Bearer | ResourcePage |
| GET | `/client/resources/catalog/options` | 无 | CatalogOptions |
| GET | `/client/me/library` | Bearer | UserLibrary |
| POST | `/client/me/favorites/{id}` | Bearer | `{ "favorite": true }` |
| DELETE | `/client/me/favorites/{id}` | Bearer | `{ "favorite": false }` |
| GET | `/client/resources/{id}/thumbnail` | 无 | 图片二进制 |
| GET | `/client/resources/{id}/preview` | 无 | 图片二进制 |
| GET | `/client/resources/{id}/download` | 免费可匿名；其他需有效权益 | 原始图片二进制 |

`id` 必须是 UUID。目录只返回已发布 `STATIC_WALLPAPER`。暂无独立详情 GET：客户端详情页使用目录条目中的元信息及 previewUrl；不要杜撰 `/resources/{id}` 详情接口。

## 账号

注册/登录请求相同：

```json
{ "email": "user@example.test", "password": "example-password-only" }
```

示例是说明数据，不是实际账号。邮箱最多 254 字符；密码 DTO 限制 8–72 字符，服务端同时限制 UTF-8 字节数不超过 72。客户端提前检查 UTF-8 字节长度，不截断密码。服务端邮箱转小写，密码不做空白裁切。

UserSession：

```json
{
  "token": "opaque-session-token",
  "expiresAt": "2026-11-12T00:00:00.000Z",
  "profile": {
    "id": "11111111-1111-4111-8111-111111111111",
    "email": "user@example.test",
    "isVip": false,
    "vipExpiresAt": null
  }
}
```

UserProfile 与 `profile` 对象同形。token 是不透明会话令牌，不是 JWT，客户端不要解析权限。默认会话约 30 天，可由服务器配置更改；以 expiresAt 和 `/auth/me` 为准。用户停用、会话过期或退出后返回 401。没有 refresh-token 接口，失效后需要重新登录。

Windows 使用当前用户 DPAPI 加密令牌，并按服务器地址隔离；Mac 应使用 Keychain 并按服务器地址隔离。不得把令牌、明文密码或本地 VIP 标志写入 Git、日志或普通配置文件。退出时尝试撤销服务端会话，并清除本机令牌；本地已下载文件不会因此删除。

## 资源目录、分类与排序

查询参数：

| 参数 | 默认 | 限制/意义 |
|---|---|---|
| page | 1 | 从 1 开始的整数 |
| pageSize | 20 | 整数 1–100；Windows 根据三屏容量取值，最多 96 |
| scope | all | all / favorites / owned |
| search | 空 | 最多 100 字符；匹配名称、说明或标签名（不区分大小写） |
| categoryId | 无 | UUID；按资源直接关联的分类筛选 |
| tagId | 无 | UUID；按标签筛选 |
| orientation | 无 | landscape / portrait；省略不筛选方向 |
| sort | recommended | recommended / newest |

示例：`GET /api/v1/client/resources?page=1&pageSize=24&orientation=landscape&sort=newest&scope=all`。

ResourcePage 包含 `items: Resource[]`、`total: number`、`page: number`、`pageSize: number`。当前服务端先统计数据库行，再过滤缺版本/包的条目，因此异常数据可能出现 total 大于可返回条目数量；分页结束不要单纯依赖 items 非空。改变筛选时回第一页并取消旧请求，只有最后一次有效请求可更新列表。

Resource 字段：

| 字段 | 类型 | 说明 |
|---|---|---|
| id / name / description | string | 资源 UUID、名称、说明 |
| type | string | 当前只返回 STATIC_WALLPAPER |
| accessPolicy | string | FREE / VIP / PURCHASE / PURCHASE_OR_VIP |
| priceMinor | integer 或 null | 最低有效商品价格，单位为货币最小单位；null 不是免费 |
| currency | string | 当前默认 CNY；不要对所有货币固定使用人民币符号 |
| version / changelog | string | 最新版本及更新说明 |
| minimumClientVersion | string | 最低客户端版本 |
| supportedDevices | string[] | 支持设备，例如 D92 |
| sha256 | string | 原包 SHA-256，64 位十六进制 |
| byteSize | integer | 原包字节数 |
| mimeType | string | image/png 或 image/jpeg |
| width / height | integer | 原图像素尺寸，未知时为 0 |
| categories | Category[] | 分类对象；顺序不保证是一级→二级 |
| tags | Tag[] | 标签对象 |
| downloadUrl / previewUrl / thumbnailUrl | string | 从服务器根解析的相对路径 |
| updatedAt | ISO 8601 string | 资源更新时间 |

Category = `{ id: string, name: string, parentId: string | null }`；Tag = `{ id: string, name: string }`。CatalogOptions = `{ categories: Category[], tags: Tag[] }`。分类/标签按 sortOrder 倒序、名称正序返回。根分类 parentId=null，子分类 parentId=根 ID。客户端依据 ID 建树，不依赖中文名称。

**当前过滤边界**：一级分类不会自动包含只关联二级分类的资源，后台需同时关联父/子，或后续扩展接口；方向目前按 PACKAGE 素材尺寸阈值过滤，横屏 width≥1000 且 height≤1000（历史未知尺寸也归横屏），竖屏 width≤1000 且 height≥1000。它不是严格的 width>height 判定，方图/小图可能被排除，多版本历史素材也可能影响筛选。Mac 先兼容此行为，不将其当成长期通用方向定义。

recommended 按 sortOrder、updatedAt、id 倒序；newest 按 updatedAt、id 倒序。目前没有热度排序。newest 是最近更新，不是独立的首次上线时间。卡片显示比例是 UI 规则：横屏 4:1、竖屏 1:4；素材完整等比显示，不拉伸。

## 收藏与权益同步

UserLibrary：

```json
{ "resourceIds": [], "hasVip": false, "favoriteIds": [] }
```

resourceIds 是当前有效、未撤销、已开始的单资源权益 ID；可能含重复值，客户端按集合处理。hasVip 表示存在有效 VIP 套餐权益；favoriteIds 为当前用户的云端收藏。此接口没有订单明细、离线许可证或已下载路径。

`scope=owned` 当前只列出有效**单资源**权益关联的已发布条目，**不展开 VIP 全目录**。VIP 可用判断需要目录策略与 UserLibrary.hasVip 联合展示，最终下载仍由服务端裁决。POST 收藏为 upsert（重复收藏可成功），DELETE 为 deleteMany（重复取消可成功）。云端收藏和本地收藏是不同存储，不自动覆盖本地收藏。

服务端下载规则：FREE 允许；VIP 要求有效 VIP 套餐权益；PURCHASE 要求有效单资源权益；PURCHASE_OR_VIP 任一满足即可。有效性同时要求 startsAt≤当前时间、revokedAt=null，以及 expiresAt=null 或 expiresAt>当前时间。本地 isVip、按钮启用状态、缓存文件都不能替代服务端授权。

## 预览、下载、校验与更新

- thumbnail/preview 为公开的已发布预览图片，返回实际 Content-Type 与 Content-Length；缩略图缓存 max-age=3600，预览 max-age=300。ETag 不等同于许可证，不承诺条件请求返回 304。
- preview 首选 PREVIEW，thumbnail 首选 COVER；缺预览时尝试免费原包回退，付费原包不会因回退公开暴露。
- download 返回 Content-Type、Content-Length、Content-Disposition 和原包哈希 ETag。当前没有授权链接签发、分段 Range/断点续传或下载次数接口。
- 免费下载不需要 Bearer；若带过期 Bearer，即使免费也可能得到 401，应清理失效会话后重试。
- Windows 当前下载最大 25 MiB；先验证 UUID、类型、大小、哈希及相对 URL，再流式下载到随机临时文件；过程中限制总大小，结束核对精确 byteSize 和 SHA-256，成功后原子替换缓存。失败或取消删除临时文件。
- Mac 使用相同校验和原子写入流程；将 token 限制在配置服务器 origin，不跟随重定向向其他 origin 发送凭据。拒绝外部绝对 URL 和逃逸目标目录的路径。
- 原包 URL 返回请求时的最新版本，没有 version 参数；目录读取与下载之间发布新版本会导致大小/哈希不一致。遇到此情况刷新目录后重试，绝不能跳过校验。
- 缩略图缓存至少区分服务器、资源 ID、版本；元信息不同而版本不变时仍可能陈旧。Windows 当前按服务器 origin、ID、版本和 updatedAt 缓存缩略图，后台重建封面会更新资源时间以刷新缓存。
- 用云端 resourceId 对本地资源去重；更新保留本地名称、分类、标签、收藏、轮播成员/顺序，重新读取图片尺寸并释放旧预览缓存。查看本地版本与目录版本的差异可展示更新提示；暂无独立更新查询接口。
- 当前页批量更新与完整目录批量更新不同，需在 UI 中写清范围；单项失败保留其他结果，取消停止后续项目。
- 已下载图片能在离线环境下由本地渲染链路使用，但当前尚无签名离线许可、到期校验或撤销同步。不能将现有缓存能力描述为正式付费离线授权保障。

## 错误格式与重试

通常错误：

```json
{ "error": { "code": "HTTP_401", "message": "请先登录", "requestId": "request-id" } }
```

校验错误通常 code=HTTP_400，message=请求参数无效，可带 details 字符串数组。服务端响应提供 `X-Request-Id`。Origin 拒绝的 code 为 ORIGIN_REJECTED。400 修正输入，401 清理/重新验证会话，403 显示无权益，404 刷新资源状态，409 提示账号/记录冲突，503 提示服务不可用。网络错误与 5xx 可由用户重试；哈希失败应重新获取清单，不无限重试旧版本。

**已知例外**：普通用户 auth 限流目前采用 express-rate-limit 默认响应，429 可能是文本而非统一 JSON。客户端先看 HTTP 状态及 Content-Type，再安全解析；不要假定所有错误可 JSON 解码。当前 auth 限流约 15 次/分钟/IP，配置或代理环境可能改变实际行为。

## 尚未实现

头像上传、密码找回/邮箱验证、刷新令牌、客户端版本发布查询、完整订单记录、支付、兑换码、签名离线许可、短时授权下载链接、独立资源详情和更新查询、动态壁纸/时钟主题/插件包客户端分发、插件在线执行、平台兼容性字段返回。服务端 ResourceVersion 虽存 supportedPlatforms，但当前目录没有输出此字段；Mac 不应据此宣布所有 Windows 主题/插件可安装。

## 对照源码与验收

- Studio：`apps/api/src/controllers.ts`（DTO、client/auth、client/me、client/resources）；`user-auth.ts`（会话、权益）；`app.ts`（前缀、Origin、限流、错误）；`tests/integration.ts`、`tests/user-auth.test.ts`。
- Windows：`src/DeskDisplay.Library/StudioApiClient.cs`、`StudioSessionStore.cs`；`src/DeskDisplay.App/ViewModels/CloudLibraryViewModel.cs`、`StudioAccountViewModel.cs`；`LibraryViewModel.cs`。
- 已在真实本地 PostgreSQL 验证账号、收藏、VIP/单购有效与过期/撤销拒绝；Windows 真实服务下载校验、版本替换及本地去重测试已通过。Mac UI、Keychain、网络和 HID 行为尚需 Mac 实机验收。
