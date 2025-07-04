(function() {
  "use strict";
  class ContentExtractor {
    extractTitle() {
      var _a;
      const titleElement = document.querySelector("title") || document.querySelector('[property="og:title"]');
      if (titleElement) {
        return ((_a = titleElement.textContent) == null ? void 0 : _a.trim()) || "";
      }
      return document.title || "";
    }
    extractAuthor() {
      var _a;
      const authorSelectors = [
        '[property="article:author"]',
        '[name="author"]',
        '[name="dc.creator"]',
        ".author",
        ".byline",
        '[rel="author"]'
      ];
      for (const selector of authorSelectors) {
        const element = document.querySelector(selector);
        if (element) {
          if (element.tagName === "META") {
            return element.content;
          }
          const text = (_a = element.textContent) == null ? void 0 : _a.trim();
          if (text) return text;
        }
      }
      return void 0;
    }
    extractMainContent() {
      const bodyContent = document.documentElement.outerHTML;
      return bodyContent ?? "";
    }
    cleanContent(html) {
      const tempDiv = document.createElement("div");
      tempDiv.innerHTML = html;
      const elementsToRemove = tempDiv.querySelectorAll(
        "script, style, nav, header, footer, .sidebar, .navigation, .comments"
      );
      elementsToRemove.forEach((el) => el.remove());
      return tempDiv.innerHTML;
    }
    extract() {
      return {
        title: this.extractTitle(),
        content: this.extractMainContent(),
        author: this.extractAuthor(),
        url: window.location.href
      };
    }
  }
  chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
    if (request.action === "extractContent") {
      try {
        const extractor = new ContentExtractor();
        const content = extractor.extract();
        sendResponse({ success: true, content });
      } catch (error) {
        sendResponse({
          success: false,
          error: error instanceof Error ? error.message : "Unknown error"
        });
      }
    }
    return true;
  });
})();
