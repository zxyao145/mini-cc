---
title: RSS feeds
description: Follow RSS and Atom feeds in MiniCC, set default tags, and pause, refresh or unsubscribe.
---

MiniCC follows RSS and Atom feeds. Feed articles land in the same library as the pages you save with the extension, and you can read, highlight and tag them the same way.

![Article list filtered to one RSS feed](../../../../../../assets/rss.png)

## Add a feed

1. Click **管理订阅** (Manage subscriptions) in the sidebar, then **添加订阅** (Add subscription).
2. Enter an RSS or Atom address. You can also enter a website address, and MiniCC looks for the feed link on that page.
3. Click **预览订阅** (Preview), check the recent entries, choose default tags, and click **确认添加** (Confirm).

The first time you add a feed, MiniCC imports up to 20 recent entries. After that it checks for new entries in the background every 30 minutes, or at the feed's own interval (such as an RSS `ttl`) when that is longer. After a failed fetch, the retry interval grows from 30 minutes up to 24 hours.

Default tags apply only to articles imported from then on. Changing them does not touch articles already imported.

## Browse feed articles

Feed articles appear in **收件箱** (Inbox) and under the **订阅** (Subscriptions) group in the sidebar. When you set the library's **来源** (Source) filter to RSS, a **订阅源** (Feed) filter appears so you can show a single feed. Your other filters stay as they are.

Some feeds only provide a summary, and those articles are marked **仅摘要** (Summary only). Open the original page and click **Save Article** in the extension to get the full text.

## Manage feeds

The **管理订阅** (Manage subscriptions) page shows each feed's status: normal, **已暂停** (Paused) or **更新失败** (Update failed). For each feed you can:

- Edit the title and default tags. The feed address is read-only.
- **立即刷新** (Refresh now): check for new entries right away. A paused feed cannot be refreshed.
- **暂停** (Pause) / **恢复** (Resume): a paused feed is not checked for updates.
- **取消订阅** (Unsubscribe): stop updates and keep every imported article.
- **删除已导入文章** (Delete imported articles): move the articles imported from this feed to the trash. Articles that also came from another feed, or that you saved with the extension, are skipped.

When a feed later changes an entry you already imported, MiniCC does not overwrite your copy. Its text, tags and reading state stay as they are.
