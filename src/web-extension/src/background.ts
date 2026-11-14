import { DEFAULT_API_URL, type SavePageMessage, type SavePageResponse } from './types';

async function savePage(message: SavePageMessage): Promise<SavePageResponse> {
  const { apiUrl = DEFAULT_API_URL, ak } = await chrome.storage.sync.get(['apiUrl', 'ak']);
  if (!ak) throw new Error('Configure your API key in Settings first.');

  const { kind, ...content } = message.content;
  const endpoint = kind === 'article' ? 'content' : 'link';
  const response = await fetch(`${apiUrl.replace(/\/+$/, '')}/api/articles/${endpoint}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-Api-Key': ak },
    body: JSON.stringify(content),
  });
  if (!response.ok) throw new Error(`HTTP ${response.status}: ${await response.text()}`);
  return { success: true };
}

chrome.runtime.onMessage.addListener((message: SavePageMessage, _sender, sendResponse) => {
  if (message.action !== 'savePage') return false;
  savePage(message).then(sendResponse).catch((error: unknown) => {
    sendResponse({ success: false, error: error instanceof Error ? error.message : `Failed to save ${message.content.kind}` });
  });
  return true;
});
