# MiniCC Chrome Extension

A Chrome extension for saving articles to your MiniCC read-later application.

## Features

- Extract the loaded page with `@mozilla/readability`, using a cloned DOM
- Sanitize readable HTML with DOMPurify and preserve the original HTML archive
- Save articles to MiniCC via REST API
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

1. Click the MiniCC extension icon while on an article webpage
2. The extension will automatically extract the page content
3. Click "Save Article" to save it to your MiniCC application
4. Use "Settings" to configure the API URL and an API key from the MiniCC account page

## Configuration

The default API URL is `http://localhost:5000` (Compose/nginx). For the current manual local setup, use `http://localhost:5001`. Do not append `/api`.

The background worker reads the API key from extension storage and sends it in `X-Api-Key`; the content script only extracts the page. Image proxy signing stays on the server. The website provides reading and management, with no URL collection form.

## API Endpoint

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