# MiniCC Chrome Extension

A Chrome extension for saving articles and links to your MiniCC read-later application.

## Features

- Extract the loaded page with `@mozilla/readability`, using a cloned DOM
- Sanitize readable HTML with DOMPurify and preserve the original HTML archive
- Save articles to MiniCC via REST API
- Save video pages and other pages without a readable article as links with available metadata
- Copy Markdown with YAML front matter to the clipboard
- Configurable API endpoint
- Clean, modern interface
- TypeScript + SCSS + pnpm workflow

## Installation

1. Install dependencies:
   ```bash
   pnpm install
   ```

2. Build the extension:
   ```bash
   pnpm build
   ```

3. Load the extension in Chrome:
   - Open Chrome and go to `chrome://extensions/`
   - Enable "Developer mode"
   - Click "Load unpacked"
   - Select the `src/web-extension/dist` folder
   - After rebuilding, reload the extension and the article tab

## Development

- `pnpm dev` - Watch mode for development
- `pnpm build` - Build for production
- `pnpm lint` - Run ESLint
- `pnpm typecheck` - Check TypeScript
- `pnpm test` - Run extraction and background messaging tests

## Usage

1. Open the MiniCC extension on a webpage.
2. Click **Save Article** or **Save Link**. The detected page type is highlighted as the suggestion; **Save Article** is unavailable when the page has no readable content. Use the gear icon in the top-right corner to configure the API URL and a key from the MiniCC account page.
3. Click **Copy Markdown** to copy the page to your clipboard independently of saving it to MiniCC.

## Configuration

The default API URL is `http://localhost:5000` (Compose/nginx). For the current manual local setup, use `http://localhost:5001`. Do not append `/api`.

The background worker reads the API key from extension storage and sends it in `X-Api-Key`; the content script only extracts the page. Image proxy signing stays on the server. The website provides reading and management, with no URL collection form.

## API Endpoints

The extension sends POST requests to `/api/articles/content` with the following payload:

```json
{
  "url": "https://example.com",
  "title": "Article Title",
  "originalContent": "<html>Original page HTML...</html>",
  "readableContent": "<article>Sanitized article HTML...</article>",
  "author": "Author Name",
  "summary": "Article summary",
  "imageUrl": "https://example.com/cover.jpg"
}
```

For links, `POST /api/articles/link` requires `url` and `title` and accepts `author`, `summary`, and `imageUrl`.