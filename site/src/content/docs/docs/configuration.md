---
title: 配置参考
description: MiniCC API 的配置文件、环境变量，以及数据库、文章库、登录和 Web 应用的设置项。
---

## 配置方式

API 从 `src/server/appsettings.yaml` 读取默认配置，文件里每一项都有中英文注释。开发环境另外加载 `appsettings.Development.yaml`。

部署时一般用环境变量覆盖配置。层级之间用两个下划线连接，例如 YAML 里的 `Library:TrashRetentionDays` 对应环境变量 `Library__TrashRetentionDays`。用 Compose 部署时，把变量写进 `minicc-api` 的 `environment`：

```yaml
minicc-api:
  environment:
    Library__TrashRetentionDays: 60
    Library__UndoWindowSeconds: 15
```

改完执行 `podman compose -f deploy/docker-compose.yaml up -d`，让 API 容器用新配置重新启动。

## 环境变量

| 变量 | 默认值 | 说明 |
| --- | --- | --- |
| `Database__Provider` | `postgres` | `postgres` 或 `sqlite`，不区分大小写 |
| `MiniCC_Db` | 见下文 | 数据库连接串。SQLite 的相对路径以 API 内容根目录为基准。 |
| `Library__TrashRetentionDays` | `30` | 回收站保留天数，范围 1–365 |
| `Library__UndoWindowSeconds` | `10` | 删除后撤销按钮显示的秒数，范围 3–60 |
| `DataProtection__KeysPath` | 空 | 登录 Cookie 加密密钥的保存目录 |
| `Images__*` | | 图片下载、访问和存储，见[图片存储](/docs/images/) |
| `ImageProxy__SignatureKey` | 空 | 旧版图片代理的签名密钥，只在 `ImageProxy` 模式下需要 |

## 数据库

`Database:Provider` 决定使用哪种数据库。连接串按这个顺序读取：

1. 环境变量 `MiniCC_Db`
2. PostgreSQL 读 `ConnectionStrings:DefaultConnection`，SQLite 读 `ConnectionStrings:Sqlite`

默认值：

| 数据库 | 默认连接串 |
| --- | --- |
| PostgreSQL | `Host=127.0.0.1;Port=15432;Database=mini_cc;Username=postgres;Password=postgres` |
| SQLite | `Data Source=data/mini_cc.db` |

SQLite 的默认路径相对于 API 内容根目录，API 启动时会自动创建所在目录。两个 Compose 文件都已经设置好 `MiniCC_Db`，一般不需要改。

API 每次启动都会自动执行数据库迁移。

## 文章库

| 配置项 | 默认值 | 范围 | 作用 |
| --- | --- | --- | --- |
| `Library:TrashRetentionDays` | 30 | 1–365 | 文章在回收站里保留的天数，到期后由后台任务永久删除 |
| `Library:UndoWindowSeconds` | 10 | 3–60 | 删除文章后撤销按钮的显示时长 |

API 启动时会检查这两项，超出范围就无法启动。

## 登录与安全

`DataProtection:KeysPath` 是登录 Cookie 加密密钥的保存目录。两个 Compose 文件都把它设为 API 数据卷里的 `/app/data/data-protection-keys`。用其他方式部署时，请把它设为一个持久化目录；否则 API 重启后密钥会丢失，所有人都要重新登录。

`AllowedHosts` 限制请求头里允许的主机名，多个用分号分隔，不写端口。默认 `*` 允许任意主机。

`AesKeys` 是加密数据库中 API 密钥所用的 AES 密钥。每一项有 `Id`、`Key` 和 `IV`，后两者都是 Base64 编码。已经保存的 API 密钥要靠原来的条目才能解密，不要删除或修改正在使用的条目。

## Web 应用

Web 应用只有一个配置：`NEXT_PUBLIC_API_URL`，即 API 的地址。它在构建时写进页面，修改后必须重新构建。

- Compose 部署：改 `minicc-web` → `build.args` 里的值，默认 `http://localhost:5000/api`。
- 从源码运行：写在 `src/web/.env.local` 里，默认 `http://localhost:5001/api`。

## 日志

API 用 Serilog 输出日志：写到控制台，同时在工作目录的 `logs/` 下按小时滚动写文件，错误日志单独写一份。用 Compose 部署时，`podman logs minicc-api` 就能看到控制台日志。
