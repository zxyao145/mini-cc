import { extractArticle } from './extract-article';

chrome.runtime.onMessage.addListener((request, _sender, sendResponse) => {
  if (request.action !== 'extractContent') return false;
  try {
    sendResponse({ success: true, content: extractArticle(document) });
  } catch (error) {
    sendResponse({ success: false, error: error instanceof Error ? error.message : 'Content extraction failed' });
  }
  return false;
});
