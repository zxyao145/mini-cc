---
title: API
description: 使用 API 密钥通过脚本或第三方工具调用 MiniCC，以及核心接口一览。
---

Web 应用通过 Cookie 维持登录；如果是写脚本或对接外部工具，在请求头 `X-Api-Key` 里带上 API 密钥即可。密钥可以在账户设置的 **API 密钥**标签页中创建，每个账号最多 10 个。

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

`GET /api/articles` 采用游标分页，返回结构为 `{ items, total, nextCursor }`。常用查询参数如下：

| 参数 | 说明 |
| --- | --- |
| `view` | 视图：`inbox`（默认）、`all`、`favorites`、`archive`、`trash` |
| `q` | 全文检索关键词，最长 200 个字符 |
| `status` | 阅读状态：`unread`、`reading`、`read`，支持逗号分隔多选（取并集） |
| `tags` | 标签 ID，逗号分隔，最多 10 个（文章须同时包含所选全部标签） |
| `source` | 来源：`extension` 或 `feed` |
| `contentKind` | 传 `link` 仅返回保存为链接的条目 |
| `feedId` | 按单个订阅源筛选 |
| `savedFrom` / `savedTo` | 保存时间范围（左闭右开，包含起点、不含终点） |
| `sort` | 排序方式：`saved_desc`、`saved_asc`、`read_desc`、`published_desc` |
| `cursor` / `pageSize` | 传入上一页返回的 `nextCursor`；每页 1–50 条，默认 20 |

## 修改文章状态

收藏、归档和已读状态都采用显式传值，接口天然幂等，重复调用结果完全一致：

```bash
curl -X PATCH -H "X-Api-Key: <你的 API 密钥>" -H "Content-Type: application/json" \
  -d '{"isFavorite": true, "readingStatus": "read"}' \
  "http://localhost:5000/api/articles/<文章 ID>/state"
```

批量接口 `POST /api/articles/batch` 单次最多处理 200 篇文章，响应中会分别列出成功的 ID 以及每条失败记录的原因。

## 完整参考

当 API 运行在 Development 环境下（例如直接用 `dotnet run` 启动）时，可以访问 <http://localhost:5001/scalar> 查看完整的交互式接口文档。
