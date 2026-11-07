import { DEFAULT_API_URL, type SaveArticleMessage, type SaveArticleResponse } from './types';

async function saveArticle(message: SaveArticleMessage): Promise<SaveArticleResponse> {
  const { apiUrl = DEFAULT_API_URL, ak } = await chrome.storage.sync.get(['apiUrl', 'ak']);
  if (!ak) throw new Error('Configure your API key in Settings first.');

  const response = await fetch(`${apiUrl.replace(/\/+$/, '')}/api/articles/content`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Api-Key': ak },
    body: JSON.stringify(message.content),
  });
  if (!response.ok) throw new Error(`HTTP ${response.status}: ${await response.text()}`);
  return { success: true };
}

chrome.runtime.onMessage.addListener((message: SaveArticleMessage, _sender, sendResponse) => {
  if (message.action !== 'saveArticle') return false;
  saveArticle(message).then(sendResponse).catch((error: unknown) => {
    sendResponse({ success: false, error: error instanceof Error ? error.message : 'Failed to save article' });
  });
  return true;
});
