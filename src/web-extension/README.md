# MiniCC Chrome Extension

A Chrome extension for saving articles to your MiniCC read-later application.

## Features

- Extract page title, content, and author automatically
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
   - Select the `src/web-extension` folder

## Development

- `pnpm dev` - Watch mode for development
- `pnpm build` - Build for production
- `pnpm lint` - Run ESLint

## Usage

1. Click the MiniCC extension icon while on any webpage
2. The extension will automatically extract the page content
3. Click "Save Article" to save it to your MiniCC application
4. Use "Settings" to configure the API URL if needed

## Configuration

The default API URL is `https://localhost:5000`. You can change this in the extension settings.

## API Endpoint

The extension sends POST requests to `/api/Articles/content` with the following payload:

```json
{
  "url": "https://example.com",
  "title": "Article Title",
  "originalContent": "HTML content...",
  "author": "Author Name",
  "contentType": "text/html"
}
```