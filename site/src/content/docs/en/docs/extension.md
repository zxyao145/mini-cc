---
title: Browser extension
description: Build and load the MiniCC Chrome extension, connect it with an API key, and save articles, links or Markdown.
---

MiniCC collects pages through a Chrome extension. The extension extracts the article in your own browser and sends the result to your server; the server never fetches the page itself. That is why you can also save pages that require you to sign in.

## Install the extension

The extension is not in the Chrome Web Store yet. Build it from source and load it in developer mode.

1. From the repository root, install the dependencies and build the extension:

   ```bash
   pnpm install --frozen-lockfile
   pnpm build:ext
   ```

2. Open `chrome://extensions/` and turn on **Developer mode** in the top-right corner.
3. Click **Load unpacked** and select `src/web-extension/dist`.

After you rebuild the extension, reload it on the extensions page and refresh any article tabs that are already open.

## Connect to your server

The extension uses an API key to reach your server.

1. In MiniCC, open account settings, go to the **API 密钥** (API Keys) tab, click **新建密钥** (New key), then click **复制** (Copy). The first start already created a key named `default`, which you can use instead. Each account can have up to 10 keys.

   ![The API Keys tab in account settings](../../../../../../assets/browser-extension-apikey.png)

2. Click the extension icon in the browser toolbar, click the gear icon, enter the API URL and the key, and click **Save**.

   The API URL is the address that serves `/api`, without `/api` itself. With Compose it is `http://localhost:5000`; when you run the API from source it is `http://localhost:5001`. On your own domain, use that address, for example `https://minicc.example.com`.

## Save a page

Open the page you want to keep and click the extension icon. The popup has three buttons and highlights the one that suits the page.

![The extension popup with Save Article, Save Link and Copy Markdown](../../../../../../assets/browser-extension.png)

**Save Article** extracts the readable content in your browser and saves it with the title, author, summary and cover. The button is disabled when the page has no article content.

**Save Link** saves only the address, title, summary and cover. Use it for videos and for pages without article text. YouTube videos, Shorts and playlists use YouTube's own title and thumbnail.

**Copy Markdown** copies the article, or a single Markdown link, to the clipboard with YAML front matter: title, URL, date, and the author and page tags when available. It does not save anything to MiniCC.

## Saving the same page again

MiniCC recognizes a page by its normalized URL, which ignores tracking parameters such as `utm_*`. When you save a page that is already in your library, MiniCC updates that article instead of creating a duplicate.

- An article in the trash is restored.
- Tags, notes, favorite and archive state are kept.
- If the article text is unchanged, reading progress and highlights stay as they are.
- If the text changed, MiniCC replaces it and your reading position starts over. Existing highlights are matched against the new text the next time you open the article. Highlights whose text is gone are marked, and their notes remain.

For a feed article that only has a summary, open the original page and click **Save Article** to get the full text.
