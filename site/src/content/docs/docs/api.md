---
title: API
description: 用 API 密钥从脚本访问 MiniCC，以及主要接口一览。
---

Web 应用用 Cookie 登录。脚本和其他工具可以在请求头 `X-Api-Key` 里带上 API 密钥。密钥在账户设置的 **API 密钥**标签页里创建，每个账号最多 10 个。

```bash
curl -H "X-Api-Key: <你的 API 密钥>" "http://localhost:5000/api/articles?view=all"
```

## 接口一览

| 分类 | 接口 |
| --- | --- |
| 文章 | `GET /api/articles`，`GET` / `PUT` / `DELETE /api/articles/{id}`，`PATCH /api/articles/{id}/state`，`POST /api/articles/{id}/restore`，`POST /api/articles/batch` |
| 剪藏 | `POST /api/articles/content`，`POST /api/articles/link` |
| 阅读 | `PUT /api/articles/{id}/progress`，`POST /api/articles/{id}/progress/reset`，`GET` / `PUT /api/account/reader-preferences` |
| 标签 | `GET` / `POST /api/tags`，`PUT` / `DELETE /api/tags/{id}`，`GET /api/tags/{id}/articles`，`POST /api/articles/{id}/tags`，`DELETE /api/articles/{id}/tags/{tagId}` |
| 高亮 | `GET /api/highlights`，`POST /api/articles/{id}/highlights`，`PUT` / `DELETE /api/highlights/{id}`，`PUT /api/highlights/{id}/anchor` |
| 订阅 | `POST /api/feeds/preview`，`GET` / `POST /api/feeds`，`PATCH` / `DELETE /api/feeds/{id}`，`POST /api/feeds/{id}/refresh`，`POST /api/feeds/{id}/trash-articles` |
| 回收站 | `DELETE /api/trash` |

## 查询文章列表

`GET /api/articles` 返回 `{ items, total, nextCursor }`，按游标分页。常用参数：

| 参数 | 说明 |
| --- | --- |
| `view` | `inbox`（默认）、`all`、`favorites`、`archive`、`trash` |
| `q` | 搜索关键词，最长 200 个字符 |
| `status` | `unread`、`reading`、`read`，多个值用逗号分隔，取并集 |
| `tags` | 标签 ID，多个用逗号分隔，最多 10 个，文章需同时带有全部标签 |
| `source` | `extension` 或 `feed` |
| `contentKind` | `link`：只返回保存为链接的条目 |
| `feedId` | 只看某个订阅源 |
| `savedFrom` / `savedTo` | 保存时间范围，包含起点，不包含终点 |
| `sort` | `saved_desc`、`saved_asc`、`read_desc`、`published_desc` |
| `cursor` / `pageSize` | 上一页返回的 `nextCursor`；每页 1–50 条，默认 20 |

## 修改文章状态

收藏、归档和已读状态都用显式的值设置，重复请求结果相同：

```bash
curl -X PATCH -H "X-Api-Key: <你的 API 密钥>" -H "Content-Type: application/json" \
  -d '{"isFavorite": true, "readingStatus": "read"}' \
  "http://localhost:5000/api/articles/<文章 ID>/state"
```

批量接口 `POST /api/articles/batch` 一次最多处理 200 篇文章，返回成功的 ID 和失败原因。

## 完整参考

API 在 Development 环境运行时（例如用 `dotnet run` 启动），可以在 <http://localhost:5001/scalar> 查看完整的接口文档。
