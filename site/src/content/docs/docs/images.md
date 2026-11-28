---
title: 图片存储
description: MiniCC 如何下载和保存文章图片，本地存储与 Cloudflare R2 等 S3 兼容存储的配置，以及在两者之间迁移。
---

保存文章后，API 在后台下载正文里的图片，存到数据库之外的地方。默认存到本地目录 `data/images`（相对于 API 内容根目录），在 Compose 部署里就是 API 数据卷中的 `/app/data/images`。

一个存储目录或存储桶只给一个 MiniCC 数据库用。清理任务会把数据库里没有引用的图片当作无用文件删掉。

## 配置项

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `Images__Mode` | `Store` | `Store` 由 API 下载并保存图片；`ImageProxy` 使用旧版图片代理 |
| `Images__Delivery` | `Authenticated` | `Authenticated` 要求登录或 API 密钥才能看图；`Public` 允许匿名访问 |
| `Images__ApiUrl` | `/api/images` | API 生成图片链接时用的地址。本地开发时设为 `http://localhost:5001/api/images` |
| `Images__PublicBaseUrl` | 空 | 公开存储桶的域名，例如 `https://images.example.com`。需要 `Delivery=Public` 并使用 S3 存储 |
| `Images__AllowPrivateNetworks` | `false` | 是否允许从内网、环回和链路本地地址下载图片 |
| `Images__Storage__Provider` | `Local` | `Local` 或 `S3` |
| `Images__Storage__LocalPath` | `data/images` | 本地图片目录，相对于 API 内容根目录 |
| `Images__Storage__S3__ServiceUrl` | 空 | S3 端点。R2 用 `https://<account_id>.r2.cloudflarestorage.com` |
| `Images__Storage__S3__Bucket` | 空 | 存储桶名 |
| `Images__Storage__S3__Region` | `auto` | 区域。R2 必须用 `auto` |
| `Images__Storage__S3__AccessKeyId` | 空 | 访问密钥 ID，请通过环境变量提供 |
| `Images__Storage__S3__SecretAccessKey` | 空 | 访问密钥，请通过环境变量提供 |

## 谁能看到图片

| 配置 | 图片地址 | 访问权限 |
| --- | --- | --- |
| `Images__Delivery=Authenticated`（默认） | MiniCC API，例如 `/api/images/<hash>.png` | 需要登录或 API 密钥 |
| `Images__Delivery=Public` | MiniCC API，例如 `/api/images/<hash>.png` | 知道地址的人都能打开 |
| `Images__Delivery=Public`，加上 `Images__PublicBaseUrl` 和 S3 存储 | 你的存储桶域名，例如 `https://images.example.com/<hash 前缀>/<hash>.png` | 知道地址的人都能直接从存储桶打开 |

## 本地存储

默认配置就是本地存储，不用改。想写明的话，在 `minicc-api.environment` 里加上：

```yaml
Images__Storage__Provider: Local
Images__Storage__LocalPath: data/images
Images__Delivery: Authenticated
```

## Cloudflare R2

1. 在 Cloudflare 控制台创建一个 R2 存储桶。
2. 创建 R2 API 令牌，权限选 **Object Read & Write**，范围限定到这个存储桶。参见 [R2 S3 API 设置](https://developers.cloudflare.com/r2/get-started/s3/) 和 [R2 API 令牌权限](https://developers.cloudflare.com/r2/api/tokens/)。
3. 在部署机器上导出凭证：

   ```bash
   export R2_ACCESS_KEY_ID="your-r2-access-key-id"
   export R2_SECRET_ACCESS_KEY="your-r2-secret-access-key"
   ```

4. 在 `minicc-api.environment` 里加上存储配置。Compose 会把上一步导出的变量代入容器。

   ```yaml
   Images__Storage__Provider: S3
   Images__Storage__S3__ServiceUrl: https://<account_id>.r2.cloudflarestorage.com
   Images__Storage__S3__Bucket: your-bucket
   Images__Storage__S3__Region: auto
   Images__Storage__S3__AccessKeyId: ${R2_ACCESS_KEY_ID:?Set R2_ACCESS_KEY_ID}
   Images__Storage__S3__SecretAccessKey: ${R2_SECRET_ACCESS_KEY:?Set R2_SECRET_ACCESS_KEY}
   Images__Delivery: Authenticated
   ```

想让图片直接从 R2 公开访问，先在存储桶设置里启用 `r2.dev` 地址（适合测试）或绑定一个图片专用域名，见 [R2 公开存储桶](https://developers.cloudflare.com/r2/buckets/public-buckets/)。然后设置 `Images__Delivery=Public` 和 `Images__PublicBaseUrl=https://images.example.com`。这样一来，任何知道图片地址的人都能访问它。

只想公开图片、又不想开放存储桶的话，设置 `Images__Delivery: Public` 并让 `Images__PublicBaseUrl` 留空，图片仍由 MiniCC API 提供。

## 下载规则

- 每 30 秒处理最多 10 篇文章的图片。全局最多同时下载 4 张，同一主机最多 2 张。
- 每张图片的时限是 30 秒，从拿到下载名额开始算，包括读取响应内容的时间。最多跟随 5 次重定向，单个文件不超过 20 MB。
- 支持 JPEG、PNG、GIF、WebP、AVIF 和 SVG，按文件内容判断格式。
- 临时错误在 1 分钟、10 分钟、1 小时、6 小时后重试，之后每 24 小时一次。失败 10 次后标记为失败。
- 没被引用的本地文件或 S3 对象，过了 24 小时的宽限期才会被清理。
- 从旧版本升级时，已有文章会先分批补录图片记录，然后再开始下载。

## 网络要求

图片下载需要直接访问外网的 HTTP(S)。为了连接到它校验过的 IP 地址，下载器会绕过系统代理和 `HTTP_PROXY`、`HTTPS_PROXY` 这类环境变量代理。所以如果服务器只能通过代理上网，`Store` 模式下的图片会下载失败。

下载器默认拒绝环回、内网和链路本地地址。确实需要保存内网站点的文章时，才设置 `Images__AllowPrivateNetworks=true`。打开之后，保存一个带内网图片地址的页面，就可能让 API 服务器去连接你内网的服务。

## 在本地和 S3 之间迁移

1. 停止 API，或者至少停止图片处理，然后备份。
2. 复制整个图片目录树。对象键里包含两位字符的哈希目录，要原样保留。下面用 [rclone](https://rclone.org/s3/) 举例，先预览，再同步：

   ```bash
   rclone sync --dry-run /path/to/images r2:your-bucket
   rclone sync /path/to/images r2:your-bucket
   ```

3. 复制完成后，再把存储配置切换到目标位置。

从 R2 迁回本地时，把源和目标对调即可。

## 旧版图片代理

API 需要 `Images__Mode=ImageProxy` 和签名密钥 `ImageProxy__SignatureKey`。用 Compose 时，导出 `IMAGE_PROXY_SECRET`，启动时再加上 `deploy/docker-compose.image-proxy.yaml`。这个文件会设置好上面两项，并加入代理服务和对应的 nginx 路由：

```bash
export IMAGE_PROXY_SECRET="your-long-random-secret"
podman compose -f deploy/docker-compose.yaml -f deploy/docker-compose.image-proxy.yaml up -d --build
```

SQLite 部署把第一个文件换成 `deploy/docker-compose.sqlite.yaml`。
