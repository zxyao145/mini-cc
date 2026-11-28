---
title: Introduction
description: What MiniCC is, how the pieces fit together, and where to start.
---

MiniCC (**M**ini web **c**lipping & content **c**ollection) is a self-hosted app for clipping and reading the web. Clip articles with the Chrome extension, subscribe to RSS and Atom feeds, and keep everything in a personal library on your own server where you can read, highlight, and organize at your own pace.

It began as a lightweight alternative to [Omnivore](https://github.com/omnivore-app/omnivore), built with ASP.NET Core on the backend and Next.js on the frontend.

:::note
The web interface is in Simplified Chinese. Throughout these docs, each UI label is shown in Chinese followed by its English translation—for example, **收件箱** (Inbox).
:::

## What a deployment contains

| Component | Role |
| --- | --- |
| API | ASP.NET Core backend that stores articles, refreshes feeds, downloads images, and handles sign-in and API keys. |
| Web app | Next.js frontend for reading, searching, and organizing your library. |
| nginx | Reverse proxy serving both the web app and `/api` on port 5000. |
| Database | PostgreSQL 16 with zhparser for Chinese word segmentation, or SQLite. |
| Chrome extension | Extracts clean article text directly in your browser and sends it to your server. |

MiniCC supports two image modes long term. The default `Store` mode downloads article images to local disk or S3-compatible object storage. `ImageProxy` serves and caches them on demand through signed proxy URLs.

## Where to start

1. [Installation](/en/docs/installation/): spin up the full stack with Compose, or run from source.
2. [Browser extension](/en/docs/extension/): build the extension and connect it to your server.
3. [Usage guide](/en/docs/usage/): get around the library, reader, highlights, and tags.

Before exposing MiniCC to the internet or moving images to object storage, check the [configuration reference](/en/docs/configuration/) and [image storage](/en/docs/images/) guide.
