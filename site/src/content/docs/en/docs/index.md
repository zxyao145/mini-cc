---
title: Introduction
description: What MiniCC is, what a deployment contains, and where to start reading.
---

MiniCC (Mini Cut Collection) is a self-hosted app for clipping and reading web pages. Save pages with the Chrome extension, follow RSS and Atom feeds, and read, highlight and organize everything in a library on your own server. It started as a minimal implementation of [Omnivore](https://github.com/omnivore-app/omnivore). The backend uses ASP.NET Core and the frontend uses Next.js.

:::note
The web interface is in Simplified Chinese. These docs give each interface label followed by an English translation, for example **收件箱** (Inbox).
:::

## What a deployment contains

| Component | Role |
| --- | --- |
| API | ASP.NET Core service. Stores articles, refreshes feeds, downloads images, and handles sign-in and API keys. |
| Web app | Next.js app for reading and managing your library. |
| nginx | Serves the web app and `/api` together on port 5000. |
| Database | PostgreSQL 16 with zhparser for Chinese word segmentation, or SQLite. |
| Chrome extension | Extracts the article in your browser and sends it to your server. |

The API downloads article images in the background and stores them in a local directory or in S3-compatible object storage. The legacy image proxy remains available as an optional mode.

## Where to start

1. [Installation](/en/docs/installation/): start every service with Compose, or run from source.
2. [Browser extension](/en/docs/extension/): install the extension and connect it to your server.
3. [Usage guide](/en/docs/usage/): the library, reader, highlights and tags.

Before you expose MiniCC to the internet or move images to object storage, read the [configuration reference](/en/docs/configuration/) and [image storage](/en/docs/images/).
