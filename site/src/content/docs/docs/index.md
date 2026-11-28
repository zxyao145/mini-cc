---
title: 简介
description: MiniCC 是什么、由哪些部分组成，以及该从哪一页开始读。
---

MiniCC（Mini Cut Collection）是一个可以自托管的网页剪藏与阅读应用。你用 Chrome 扩展保存网页，订阅 RSS 和 Atom，然后在自己服务器上的文章库里阅读、高亮和整理。项目最初是 [Omnivore](https://github.com/omnivore-app/omnivore) 的一个精简实现，后端用 ASP.NET Core，前端用 Next.js。

## 组成部分

一套 MiniCC 部署包含这些部分：

| 组件 | 作用 |
| --- | --- |
| API | ASP.NET Core 服务。保存文章，刷新订阅，下载图片，处理登录和 API 密钥。 |
| Web 应用 | Next.js 应用，用来阅读和管理文章库。 |
| nginx | 在 5000 端口同时提供 Web 应用和 `/api`。 |
| 数据库 | PostgreSQL 16（带 zhparser 中文分词）或 SQLite。 |
| Chrome 扩展 | 在你的浏览器里提取正文，再发送到服务器。 |

文章里的图片由 API 在后台下载，存到本地目录或 S3 兼容的对象存储。旧版图片代理服务也还能用，作为可选模式保留。

## 从哪里开始

1. [安装部署](/docs/installation/)：用 Compose 启动全部服务，或者从源码运行。
2. [浏览器扩展](/docs/extension/)：安装扩展，连接到你的服务器。
3. [使用指南](/docs/usage/)：文章库、阅读器、高亮和标签怎么用。

准备放到公网，或者想把图片存进对象存储，先读 [配置参考](/docs/configuration/) 和 [图片存储](/docs/images/)。
