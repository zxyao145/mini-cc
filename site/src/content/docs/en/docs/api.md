---
title: API
description: Authenticate scripts and external tools with an API key, and browse the core MiniCC endpoints.
---

While the web app uses cookies for sign-in, scripts and external tools can pass an API key via the `X-Api-Key` header. Create keys under the **API 密钥** (API Keys) tab in account settings; each account supports up to 10 keys.

```bash
curl -H "X-Api-Key: <your-api-key>" "http://localhost:5000/api/articles?view=all"
```

## Endpoints

| Area | Endpoints |
| --- | --- |
| Articles | `GET /api/articles`, `GET` / `PUT` / `DELETE /api/articles/{id}`, `PATCH /api/articles/{id}/state`, `POST /api/articles/{id}/restore`, `POST /api/articles/batch` |
| Clipping | `POST /api/articles/content`, `POST /api/articles/link` |
| Reading | `PUT /api/articles/{id}/progress`, `POST /api/articles/{id}/progress/reset`, `GET` / `PUT /api/account/reader-preferences` |
| Tags | `GET` / `POST /api/tags`, `PUT` / `DELETE /api/tags/{id}`, `GET /api/tags/{id}/articles`, `POST /api/articles/{id}/tags`, `DELETE /api/articles/{id}/tags/{tagId}` |
| Highlights | `GET /api/highlights`, `POST /api/articles/{id}/highlights`, `PUT` / `DELETE /api/highlights/{id}`, `PUT /api/highlights/{id}/anchor` |
| Feeds | `POST /api/feeds/preview`, `GET` / `POST /api/feeds`, `PATCH` / `DELETE /api/feeds/{id}`, `POST /api/feeds/{id}/refresh`, `POST /api/feeds/{id}/trash-articles` |
| Trash | `DELETE /api/trash` |

## List articles

`GET /api/articles` uses cursor-based pagination and returns `{ items, total, nextCursor }`. Common query parameters:

| Parameter | Description |
| --- | --- |
| `view` | `inbox` (default), `all`, `favorites`, `archive`, or `trash` |
| `q` | Search query, up to 200 characters |
| `status` | `unread`, `reading`, or `read`; comma-separated to match any |
| `tags` | Comma-separated tag IDs (up to 10); articles must include all of them |
| `source` | `extension` or `feed` |
| `contentKind` | Pass `link` to return only items saved as links |
| `feedId` | Filter to a single feed |
| `savedFrom` / `savedTo` | Save-date range (start inclusive, end exclusive) |
| `sort` | `saved_desc`, `saved_asc`, `read_desc`, or `published_desc` |
| `cursor` / `pageSize` | `nextCursor` from the previous page; 1–50 items per page (default 20) |

## Change article state

Favorite, archive, and reading status take explicit values, making requests idempotent when retried:

```bash
curl -X PATCH -H "X-Api-Key: <your-api-key>" -H "Content-Type: application/json" \
  -d '{"isFavorite": true, "readingStatus": "read"}' \
  "http://localhost:5000/api/articles/<article-id>/state"
```

`POST /api/articles/batch` accepts up to 200 articles per call and returns both the IDs that succeeded and per-item reasons for any failures.

## Full reference

When the API runs in the Development environment (such as via `dotnet run`), you can browse the interactive Scalar API reference at http://localhost:5001/scalar.
