---
title: 备份与升级
description: 备份 MiniCC 的数据库和本地图片，安全升级到新版本，以及排查常见问题。
---

## 备份

数据库和本地图片请务必一起备份。如果只恢复了数据库却丢了图片文件，系统会尝试重新回源下载，而原网站上的图片可能早已失效。

- **PostgreSQL**：直接备份 `pgdata` 卷，或使用 `pg_dump` 导出逻辑备份：

  ```bash
  podman exec db pg_dump -U postgres mini_cc > mini_cc.sql
  ```

- **SQLite**：先停掉 API，再把数据库文件连同残留的 WAL 文件一起拷走。如果宿主机装有 SQLite 命令行工具，也可以在不停止服务的情况下热备：

  ```bash
  sqlite3 /path/to/mini_cc.db ".backup /path/to/backup.db"
  ```

- **本地图片**：在 Compose 部署中位于 `/app/data/images`，对应 PostgreSQL 版的 `api-data` 卷或 SQLite 版的 `sqlite-data` 卷。如果图片存在 Cloudflare R2，则由 R2 自身的保留或复制策略负责保护。

## 升级

1. 先做好数据库和本地图片目录的备份。PostgreSQL 版导出数据库并备份 `api-data`；SQLite 版停掉 API 后备份 `sqlite-data`。
2. 用所选 Release 的部署文件更新 `deploy/`，保留自己的配置。把 [安装命令](/docs/installation/) 中的 `MINICC_VERSION` 换成新标签，并使用原 Compose 项目和数据库参数启动。已有 PostgreSQL 部署迁到 GHCR 时，请传 `--database postgres`。如果继续在本地构建，使用新版本源码和原先的基础 Compose 文件执行 `up -d --build`。

3. API 启动后会自动跑完数据库迁移。运行 `podman logs -f minicc-api` 可以实时查看迁移和图片下载进度。

如果新版本同时更新了浏览器扩展，把该 Release 的扩展 ZIP 解压到原来的扩展目录，然后在 `chrome://extensions/` 刷新扩展。

### 从旧版图片代理升级

新版本默认改为由 API 自行下载并持久化图片。首次启动时，后台任务会自动剥离旧的代理地址前缀，再陆续拉取图片，日志里会打印补录条数和下载汇总。

迁移期间请先留着旧的 `image-proxy-data` 卷：不少原站图片可能已经下线，这个卷里存着的或许是仅剩的副本。

如果中途需要回退到旧版本，先停掉新版本服务，恢复升级前备份的数据库，再挂回原来的代理缓存卷启动旧版本即可。旧版本依赖备份里记录的签名图片链接。

## 常见问题

### API 启动时卡在数据库迁移

如果上一次迁移进程被强行中断，数据库里可能残留 `__EFMigrationsLock` 锁记录，导致 API 启动后一直干等。先停掉 API，确认没有迁移进程在跑，然后清掉这张表即可：

```sql
DELETE FROM "__EFMigrationsLock";
```

### 改了配置却没有生效

用 Compose 部署时，修改环境变量后重新运行原来的部署命令，让 API 容器读入新值。从本地构建 Web 镜像时，修改 `NEXT_PUBLIC_API_URL` 后需要带上 `--build` 重新构建。

### 扩展保存文章失败

先核对扩展设置里的 API URL：这里要填的是提供 `/api` 服务的根地址，末尾不要带 `/api`。再检查一下所用密钥是否仍然存在于账户设置的 **API 密钥**列表中。
