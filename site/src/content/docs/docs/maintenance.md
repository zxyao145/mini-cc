---
title: 备份与升级
description: 备份 MiniCC 的数据库和图片，升级到新版本，以及处理卡住的数据库迁移。
---

## 备份

数据库和本地图片要一起备份。只恢复数据库、没有对应的图片时，已经存过的图片会重新从原网站下载，而原网站上的图片可能已经不在了。

**PostgreSQL**：备份 `pgdata` 卷，或者导出数据库：

```bash
podman exec db pg_dump -U postgres mini_cc > mini_cc.sql
```

**SQLite**：先停止 API，再把数据库文件和残留的 WAL 文件一起复制。主机上装了 SQLite 命令行工具的话，也可以在 API 运行时备份：

```bash
sqlite3 /path/to/mini_cc.db ".backup /path/to/backup.db"
```

**本地图片**：在 Compose 部署里是 `/app/data/images`，PostgreSQL 版在 `api-data` 卷中，SQLite 版在 `sqlite-data` 卷中。用 R2 时，图片文件在存储桶里，靠 R2 自己的保留或复制策略来备份。

## 升级

1. 备份数据库和本地图片目录。PostgreSQL 版导出数据库并备份 `api-data`；SQLite 版停止 API 后备份 `sqlite-data`。
2. 拉取新版本代码，用原来的 Compose 命令重新构建：

   ```bash
   git pull
   podman compose -f deploy/docker-compose.yaml up -d --build
   ```

3. API 启动时自动执行数据库迁移。可以在 API 日志里查看迁移和图片下载的进度：`podman logs -f minicc-api`。

升级后浏览器扩展可能也有更新。在仓库根目录执行 `pnpm build:ext`，然后在 `chrome://extensions/` 里重新加载扩展。

### 从使用图片代理的旧版本升级

新版本默认由 API 下载并保存图片。第一次启动时，后台任务会去掉旧的代理地址前缀，再开始下载图片，日志里能看到补录数量和下载汇总。

迁移期间请保留旧的 `image-proxy-data` 卷，有些图片在原网站上已经删了，这个卷里可能是它们仅存的副本。

要回退到旧版本，先停止新版本，恢复升级前的数据库备份，再启动旧版本，并继续使用原来的代理缓存卷。旧版本依赖备份里保存的签名图片地址。

## 常见问题

### API 启动时卡在数据库迁移

迁移进程被强行终止后，API 可能一直等待 `__EFMigrationsLock`。先停止 API，确认没有迁移在运行，然后清空这张表：

```sql
DELETE FROM "__EFMigrationsLock";
```

### 改了配置但没有生效

用 Compose 部署时，环境变量改完要重新执行 `up -d`，API 容器才会用新配置重建。`NEXT_PUBLIC_API_URL` 是构建参数，要加 `--build` 重新构建 Web 镜像。

### 扩展保存失败

检查扩展设置里的 API URL：它是提供 `/api` 的地址，本身不带 `/api`。再确认 API 密钥还在账户设置的 **API 密钥**列表里。
