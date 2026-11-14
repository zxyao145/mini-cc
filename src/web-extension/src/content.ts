import { extractPage } from './extract-page';
import { buildMarkdown } from './markdown';

const loadedUrl = location.href;

chrome.runtime.onMessage.addListener((request, _sender, sendResponse) => {
  const { action } = request;
  try {
    if (action === 'extractContent') {
      sendResponse({ success: true, content: extractPage(document, loadedUrl) });
    } else if (action === 'extractMarkdown') {
      sendResponse({ success: true, markdown: buildMarkdown(document, loadedUrl) });
    }
  } catch (error) {
    sendResponse({ success: false, error: error instanceof Error ? error.message : 'Page extraction failed' });
  }
  return false;
});
