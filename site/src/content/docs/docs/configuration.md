---
title: 配置参考
description: MiniCC API 的配置文件与环境变量说明，涵盖数据库、文章库、登录安全与 Web 应用设置。
---

## 配置方式

API 默认从 `src/server/appsettings.yaml` 读取配置，文件里每一项都配有中英文注释；本地开发时还会额外加载 `appsettings.Development.yaml`。

实际部署时，推荐直接通过环境变量覆盖默认项。层级之间用双下划线连接，比如 YAML 里的 `Library:TrashRetentionDays` 对应的就是环境变量 `Library__TrashRetentionDays`。使用 Compose 部署时，把变量写进 `minicc-api` 的 `environment` 段即可：

```yaml
minicc-api:
  environment:
    Library__TrashRetentionDays: 60
    Library__UndoWindowSeconds: 15
```

改完后重新执行 `podman compose -f deploy/docker-compose.yaml up -d`，让 API 容器带着新配置重启。

## 环境变量

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `Database__Provider` | `postgres` | 数据库类型，可选 `postgres` 或 `sqlite`（不区分大小写） |
| `MiniCC_Db` | 见下文 | 数据库连接串。使用 SQLite 且填相对路径时，以 API 内容根目录为基准 |
| `Library__TrashRetentionDays` | `30` | 回收站文章保留天数，范围 1–365 |
| `Library__UndoWindowSeconds` | `10` | 删除文章后撤销提示的显示秒数，范围 3–60 |
| `DataProtection__KeysPath` | 空 | 登录 Cookie 加密密钥的存放目录 |
| `Images__*` | | 图片下载、访问控制与存储配置，详见[图片存储](/docs/images/) |
| `ImageProxy__SignatureKey` | 空 | 图片代理的签名密钥，仅在 `ImageProxy` 模式下需要 |

## 数据库

`Database:Provider` 用来指定数据库类型，连接串按以下优先级读取：

1. 环境变量 `MiniCC_Db`
2. 配置文件中的 `ConnectionStrings:DefaultConnection`（PostgreSQL）或 `ConnectionStrings:Sqlite`（SQLite）

默认连接串：

| 数据库 | 默认连接串 |
| --- | --- |
| PostgreSQL | `Host=127.0.0.1;Port=15432;Database=mini_cc;Username=postgres;Password=postgres` |
| SQLite | `Data Source=data/mini_cc.db` |

SQLite 的默认路径相对于 API 内容根目录，启动时如果目录不存在会自动创建。两份 Compose 文件都已配好 `MiniCC_Db`，一般无需改动。

API 每次启动时都会自动执行数据库迁移。

## 文章库

| 配置项 | 默认值 | 范围 | 说明 |
| --- | --- | --- | --- |
| `Library:TrashRetentionDays` | 30 | 1–365 | 文章在回收站中保留的天数，到期后由后台任务永久清理 |
| `Library:UndoWindowSeconds` | 10 | 3–60 | 删除文章后底部撤销按钮的停留时长 |

API 启动时会校验这两项配置，一旦超出允许范围就会拒绝启动。

## 登录与安全

`DataProtection:KeysPath` 指定登录 Cookie 加密密钥的保存目录。两份 Compose 文件默认都将它指向 API 数据卷里的 `/app/data/data-protection-keys`。如果采用其他方式部署，请务必指定一个持久化目录；否则 API 每次重启密钥都会重置，所有已登录的浏览器都要重新登录。

`AllowedHosts` 用来限制请求头 `Host` 允许的主机名，多个域名用分号隔开，不要带端口号。默认值 `*` 表示不限制。

`AesKeys` 存放用于加密数据库中 API 密钥的 AES 密钥，每条记录包含 `Id`、`Key` 和 `IV`（后两项为 Base64 编码）。已经生成的 API 密钥依赖当时加密它的那条记录来解密，切勿随意删除或改动正在使用的条目。

## Web 应用

Web 应用只有一个配置项：`NEXT_PUBLIC_API_URL`（API 地址）。它会在构建阶段写进前端代码，修改后必须重新构建镜像或重新编译：

- **GHCR 镜像**使用同源的 `/api`。从本地构建 Compose 镜像时，可以修改 `minicc-web` → `build.args` 下的配置，默认值为 `http://localhost:5000/api`。
- **从源码运行**：写在 `src/web/.env.local` 中，默认值为 `http://localhost:5001/api`。

## 日志

API 使用 Serilog 记录日志：除了输出到控制台，还会在工作目录的 `logs/` 下按小时滚动写入文件，错误日志会单独归档一份。使用 Compose 部署时，运行 `podman logs minicc-api` 即可查看控制台日志。
