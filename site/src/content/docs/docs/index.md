---
title: 简介
description: 了解 MiniCC 是什么、系统由哪些部分组成，以及上手的推荐顺序。
---

MiniCC（**M**ini web **c**lipping & content **c**ollection）是一款可自托管的网页剪藏与阅读应用。平时看到想留下的文章，用 Chrome 扩展一键保存；常看的博客和站点，直接用 RSS / Atom 订阅；所有内容都收拢在你自己服务器的文章库里，随时阅读、高亮、写笔记。

项目最初是 [Omnivore](https://github.com/omnivore-app/omnivore) 的精简替代实现，后端基于 ASP.NET Core，前端基于 Next.js。

## 组成部分

一套完整的 MiniCC 部署包含以下五个部分：

| 组件 | 作用 |
| --- | --- |
| API | ASP.NET Core 后端服务，负责保存文章、定时刷新订阅、下载图片，以及登录认证和 API 密钥管理。 |
| Web 应用 | Next.js 前端界面，用来日常阅读、检索和整理文章库。 |
| nginx | 反向代理，在 5000 端口统一对外提供 Web 应用和 `/api` 接口。 |
| 数据库 | 可选 PostgreSQL 16（内置 zhparser 中文分词）或 SQLite。 |
| Chrome 扩展 | 直接在浏览器里提取当前页面正文，再发送到你的服务器。 |

MiniCC 长期支持两种图片模式。默认的 `Store` 模式将文章图片下载到本地目录或 S3 兼容的对象存储。`ImageProxy` 模式通过签名链接按需代理并缓存图片。

## 从哪里开始

1. [安装部署](/docs/installation/)：用 Compose 一键拉起全套服务，或者从源码本地运行。
2. [浏览器扩展](/docs/extension/)：构建并加载扩展，填上密钥连到你的服务器。
3. [使用指南](/docs/usage/)：熟悉文章库、阅读器、高亮笔记和标签的日常用法。

如果打算把服务放到公网，或者想把图片迁到对象存储，建议顺手看看 [配置参考](/docs/configuration/) 和 [图片存储](/docs/images/)。
