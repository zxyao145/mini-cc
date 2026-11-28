---
title: 图片存储
description: 了解 MiniCC 如何下载和保存文章图片，配置本地目录或 Cloudflare R2 等 S3 兼容存储，以及两者之间的迁移方法。
---

保存文章后，API 会在后台自动下载正文里的图片，独立存放在文件系统或对象存储里，而不塞进数据库。默认目录是 `data/images`（相对于 API 内容根目录）；在 Compose 部署中，对应 API 数据卷里的 `/app/data/images`。

请确保一个存储目录或存储桶只对应一个 MiniCC 数据库，不要多个实例混用。后台清理任务会把数据库里不再引用的图片视作孤立文件自动删除。

## 配置项

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `Images__Mode` | `Store` | `Store` 由 API 自行下载并持久化图片；`ImageProxy` 切换回旧版图片代理 |
| `Images__Delivery` | `Authenticated` | `Authenticated` 需登录或携带 API 密钥才能看图；`Public` 允许匿名访问 |
| `Images__ApiUrl` | `/api/images` | API 拼接图片链接时使用的地址。本地开发请设为 `http://localhost:5001/api/images` |
| `Images__PublicBaseUrl` | 空 | 公开存储桶的访问域名，例如 `https://images.example.com`。需同时启用 `Delivery=Public` 和 S3 存储 |
| `Images__AllowPrivateNetworks` | `false` | 是否允许从内网、环回及链路本地地址下载图片 |
| `Images__Storage__Provider` | `Local` | 存储后端：`Local` 或 `S3` |
| `Images__Storage__LocalPath` | `data/images` | 本地图片目录，相对于 API 内容根目录 |
| `Images__Storage__S3__ServiceUrl` | 空 | S3 端点地址。Cloudflare R2 填 `https://<account_id>.r2.cloudflarestorage.com` |
| `Images__Storage__S3__Bucket` | 空 | 存储桶名称 |
| `Images__Storage__S3__Region` | `auto` | 区域标识。Cloudflare R2 必须设为 `auto` |
| `Images__Storage__S3__AccessKeyId` | 空 | 访问密钥 ID，建议通过环境变量传入 |
| `Images__Storage__S3__SecretAccessKey` | 空 | 访问密钥，建议通过环境变量传入 |

## 谁能看到图片

| 配置 | 图片地址 | 访问权限 |
| --- | --- | --- |
| `Images__Delivery=Authenticated`（默认） | 经由 MiniCC API，如 `/api/images/<hash>.png` | 需登录或持有 API 密钥 |
| `Images__Delivery=Public` | 经由 MiniCC API，如 `/api/images/<hash>.png` | 拿到链接的人都能查看 |
| `Images__Delivery=Public`，配合 `Images__PublicBaseUrl` 和 S3 存储 | 你的存储桶域名，如 `https://images.example.com/<hash 前缀>/<hash>.png` | 拿到链接的人可直接从存储桶读取 |

## 本地存储

开箱即用的默认方案就是本地存储，不用做任何修改。如果想在配置里显式写出来，可以在 `minicc-api.environment` 中添加：

```yaml
Images__Storage__Provider: Local
Images__Storage__LocalPath: data/images
Images__Delivery: Authenticated
```

## Cloudflare R2

1. 在 Cloudflare 控制台新建一个 R2 存储桶。
2. 创建 R2 API 令牌，赋予 **Object Read & Write** 权限，并将作用范围限定在这个存储桶。具体步骤可参考 [R2 S3 API 设置](https://developers.cloudflare.com/r2/get-started/s3/) 与 [R2 API 令牌权限](https://developers.cloudflare.com/r2/api/tokens/)。
3. 在运行 Compose 的宿主机上导出凭证：

   ```bash
   export R2_ACCESS_KEY_ID="your-r2-access-key-id"
   export R2_SECRET_ACCESS_KEY="your-r2-secret-access-key"
   ```

4. 在 `minicc-api.environment` 里填好以下配置，Compose 会自动把上一步导出的环境变量传入容器：

   ```yaml
   Images__Storage__Provider: S3
   Images__Storage__S3__ServiceUrl: https://<account_id>.r2.cloudflarestorage.com
   Images__Storage__S3__Bucket: your-bucket
   Images__Storage__S3__Region: auto
   Images__Storage__S3__AccessKeyId: ${R2_ACCESS_KEY_ID:?Set R2_ACCESS_KEY_ID}
   Images__Storage__S3__SecretAccessKey: ${R2_SECRET_ACCESS_KEY:?Set R2_SECRET_ACCESS_KEY}
   Images__Delivery: Authenticated
   ```

如果希望浏览器直接从 R2 加载图片，先在存储桶设置里开启 `r2.dev` 域名（适合测试）或绑定自定义图片域名（详见 [R2 公开存储桶](https://developers.cloudflare.com/r2/buckets/public-buckets/)），然后设置 `Images__Delivery=Public` 和 `Images__PublicBaseUrl=https://images.example.com`。开启后，任何知道图片链接的人都能直接访问。

如果只是想让图片支持免登录查看，又不想把存储桶直接对外公开，可以设置 `Images__Delivery: Public` 同时将 `Images__PublicBaseUrl` 留空，这样图片仍由 MiniCC API 统一中转提供。

## 下载规则

- 后台每 30 秒处理一批图片，每次最多涵盖 10 篇文章；全局并发上限为 4 张，同一域名最多同时下载 2 张。
- 每张图片从拿到下载名额到读取完毕限时 30 秒，最多跟随 5 次重定向，单张大小不超过 20 MB。
- 支持 JPEG、PNG、GIF、WebP、AVIF 和 SVG，系统会直接按文件内容识别格式。
- 遇到临时网络错误时，依次在 1 分钟、10 分钟、1 小时、6 小时后重试，之后每 24 小时重试一次；累计失败 10 次后标记为失败。
- 不再被引用的本地文件或 S3 对象会保留 24 小时宽限期，期满后才会被清理。
- 从旧版本升级上来时，系统会先分批为存量文章补录图片记录，再陆续拉取原图。

## 网络要求

图片下载需要服务器能够直接访问外网 HTTP(S)。为了直接连接到校验通过的 IP 地址，下载器会绕过系统代理以及 `HTTP_PROXY`、`HTTPS_PROXY` 等代理环境变量。如果你的服务器只能通过代理上网，`Store` 模式下的图片下载将会失败。

出于安全考虑，下载器默认拦截环回地址、内网地址和链路本地地址。只有在确实需要剪藏内网站点文章时，才建议开启 `Images__AllowPrivateNetworks=true`。请注意，打开该选项后，保存包含内网图片链接的页面可能会让 API 服务器去请求你内网中的其他服务。

## 在本地和 S3 之间迁移

1. 先停掉 API（或至少暂停图片处理），做好备份。
2. 完整复制整个图片目录树，务必保留两字符的哈希子目录结构。下面以 [rclone](https://rclone.org/s3/) 为例，先演练预览，确认无误再正式同步：

   ```bash
   rclone sync --dry-run /path/to/images r2:your-bucket
   rclone sync /path/to/images r2:your-bucket
   ```

3. 文件同步完成后，再修改配置切换到目标存储后端。

若要从 R2 迁回本地，把命令里的源和目标对调即可。

## 旧版图片代理

启用旧版代理需要为 API 配置 `Images__Mode=ImageProxy` 以及签名密钥 `ImageProxy__SignatureKey`。使用 Compose 时，先导出 `IMAGE_PROXY_SECRET`，再叠加上 `deploy/docker-compose.image-proxy.yaml` 启动即可。这份覆盖文件会自动配好上述变量，并拉起代理容器和对应的 nginx 路由：

```bash
export IMAGE_PROXY_SECRET="your-long-random-secret"
podman compose -f deploy/docker-compose.yaml -f deploy/docker-compose.image-proxy.yaml up -d --build
```

如果是 SQLite 部署，把第一份 Compose 文件换成 `deploy/docker-compose.sqlite.yaml`。
