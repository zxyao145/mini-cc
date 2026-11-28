---
title: Usage guide
description: Library views and filters, reader settings, highlights and notes, tags, batch actions and the trash.
---

## Library

The **工作区** (Workspace) group in the sidebar has five views: **收件箱** (Inbox), **全部文章** (All articles), **收藏** (Favorites), **归档** (Archive) and **回收站** (Trash). Archived articles leave the inbox but still appear in All articles.

The inbox shows only unread articles by default. To see read articles there, change the **阅读状态** (Reading status) filter.

![The inbox, with search and filters at the top and article cards below](../../../../../../assets/inbox.png)

The search box at the top runs a full-text search. PostgreSQL deployments segment Chinese words; SQLite deployments match substrings. Below it are these filters:

| Filter | Options |
| --- | --- |
| **阅读状态** (Reading status) | **未读** (Unread), **阅读中** (Reading), **已读** (Read); you can pick several |
| **标签** (Tags) | Up to 10 tags; an article must have all of them |
| **来源** (Source) | **浏览器收藏** (saved with the extension) or RSS |
| **内容类型** (Content type) | **链接** (Link): entries saved as links only |
| **订阅源** (Feed) | Appears when the source is RSS; shows one feed |
| **保存时间** (Saved) | Last week, last month, or a custom date range |
| **排序** (Sort) | Recently saved, oldest saved, recently read; newest published when you view feed articles |

The page URL keeps your filters, so you can bookmark a view. Switch between **卡片视图** (Card view) and **列表视图** (List view) at the top right.

## Reader

Click an article card to open the reader.

Your reading position is saved automatically and synced across devices. Saving starts once you are actually reading: scroll a little way and stay for a few seconds, and the article becomes **阅读中** (Reading). Opening an article for a glance does not change its status. At the end of an article, a prompt offers **标记已读** (Mark as read); MiniCC never marks an article read on its own.

The toolbar has:

- **标记已读** (Mark as read) / **标记未读** (Mark as unread)
- Favorite, archive and delete
- **从头阅读** (Start over): clears the saved position without changing the reading status
- **阅读设置** (Reader settings): font size (16–28), line height, width (narrow, standard, wide), typeface (system or serif) and theme: **跟随系统** (System), **浅色** (Light), **深色** (Dark) or **护眼** (Sepia). Settings are stored in your account and follow you to other browsers. **恢复默认** (Reset) restores the defaults.
- **目录** (Contents): long articles get a table of contents. Wide screens show it beside the text; on narrow screens, open it from the toolbar.

If you read the same article to different positions on two devices, the reader asks you to choose: **跳转** (Jump) to the position saved on the other device, or **留在这里** (Stay here).

### Feed articles with only a summary

Some feeds provide only a summary, and the reader shows **仅摘要** (Summary only) at the top. Open the original page and click **Save Article** in the extension to get the full text. Tags and notes are kept.

## Highlights and notes

Select text in the reader and click **高亮** (Highlight). Click a highlight to add a note or delete it.

The **高亮笔记** (Highlights) page in the sidebar lists the highlights and notes from every article. Click an article title to open that article.

![The Highlights page](../../../../../../assets/highlight.png)

When an article's text is updated, MiniCC looks for each highlight in the new text. A highlight whose text is gone shows **原文位置已失效** (original position no longer found), and its note remains.

## Tags

Add tags from an article card or the reader. Type to search existing tags; when no tag has that name, choose **创建「name」** (Create "name") and pick a color.

The **标签** (Tags) page in the sidebar manages all tags: create, rename, recolor and delete. Deleting a tag removes it from every article and from feed default tags. The articles themselves are not affected.

![The Tags page, grouped by first letter](../../../../../../assets/tag.png)

## Batch actions

Click **选择** (Select) at the top of the library, or on desktop hover over a card and tick its checkbox. With articles selected you can mark them read or unread, favorite or unfavorite, archive, move them back to the inbox, add or remove tags, or delete them.

Batch actions apply to the articles already loaded on the page. If some articles fail, they stay selected so you can retry.

## Delete, undo and the trash

After you delete articles, an undo button appears at the bottom of the page for 10 seconds by default.

Deleted articles go to **回收站** (Trash) for 30 days, and each card shows the days remaining. From the trash you can **恢复** (Restore) an article or **永久删除** (Delete permanently). A restored article comes back with its tags, highlights, notes and reading position. **清空回收站** (Empty trash) permanently deletes everything in the trash.

The undo window and retention period can be changed on the server. See the [configuration reference](/en/docs/configuration/#library).

## Interface theme

Switch between **浅色** (Light), **深色** (Dark) and **跟随系统** (System) at the bottom of the sidebar. The reader has its own theme setting; when set to **跟随系统** (System), it follows this interface theme.
