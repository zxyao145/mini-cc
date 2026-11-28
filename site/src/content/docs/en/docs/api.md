---
title: API
description: Call MiniCC from scripts with an API key, and an overview of the main endpoints.
---

The web app signs in with a cookie. Scripts and other tools can send an API key in the `X-Api-Key` header. Create keys on the **API 密钥** (API Keys) tab of account settings; each account can have up to 10.

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

`GET /api/articles` returns `{ items, total, nextCursor }` with cursor pagination. Common parameters:

| Parameter | Description |
| --- | --- |
| `view` | `inbox` (default), `all`, `favorites`, `archive`, `trash` |
| `q` | Search text, up to 200 characters |
| `status` | `unread`, `reading`, `read`; separate several with commas to match any of them |
| `tags` | Tag IDs separated by commas, up to 10; an article must have all of them |
| `source` | `extension` or `feed` |
| `contentKind` | `link` returns only entries saved as links |
| `feedId` | Show a single feed |
| `savedFrom` / `savedTo` | Save-date range; the start is inclusive and the end exclusive |
| `sort` | `saved_desc`, `saved_asc`, `read_desc`, `published_desc` |
| `cursor` / `pageSize` | The previous page's `nextCursor`; 1–50 items per page, 20 by default |

## Change article state

Favorite, archive and reading status are set with explicit values, so repeating a request gives the same result:

```bash
curl -X PATCH -H "X-Api-Key: <your-api-key>" -H "Content-Type: application/json" \
  -d '{"isFavorite": true, "readingStatus": "read"}' \
  "http://localhost:5000/api/articles/<article-id>/state"
```

`POST /api/articles/batch` handles up to 200 articles per request and returns the IDs that succeeded and the reasons for any failures.

## Full reference

When the API runs in the Development environment, as it does with `dotnet run`, the complete reference is available in Scalar at http://localhost:5001/scalar.
