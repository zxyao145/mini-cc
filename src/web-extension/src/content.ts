import { ExtractedContent } from "./types";

class ContentExtractor {
  private extractTitle(): string {
    const titleElement =
      document.querySelector("title") ||
      (document.querySelector('[property="og:title"]') as HTMLMetaElement);

    if (titleElement) {
      return titleElement.textContent?.trim() || "";
    }

    return document.title || "";
  }

  private extractAuthor(): string | undefined {
    const authorSelectors = [
      '[property="article:author"]',
      '[name="author"]',
      '[name="dc.creator"]',
      ".author",
      ".byline",
      '[rel="author"]',
    ];

    for (const selector of authorSelectors) {
      const element = document.querySelector(selector) as HTMLMetaElement;
      if (element) {
        if (element.tagName === "META") {
          return element.content;
        }
        const text = element.textContent?.trim();
        if (text) return text;
      }
    }

    return undefined;
  }

  private extractMainContent(): string {
    // const contentSelectors = [
    //   'article',
    //   '[role="main"]',
    //   '.content',
    //   '.post-content',
    //   '.entry-content',
    //   '.article-content',
    //   'main'
    // ];

    // for (const selector of contentSelectors) {
    //   const element = document.querySelector(selector);
    //   if (element) {
    //     return this.cleanContent(element.innerHTML);
    //   }
    // }

    // const bodyContent = document.body.innerHTML;
    // return this.cleanContent(bodyContent);
    const bodyContent = document.documentElement.outerHTML;
    return bodyContent ?? "";
  }

  private cleanContent(html: string): string {
    const tempDiv = document.createElement("div");
    tempDiv.innerHTML = html;

    const elementsToRemove = tempDiv.querySelectorAll(
      "script, style, nav, header, footer, .sidebar, .navigation, .comments"
    );
    elementsToRemove.forEach((el) => el.remove());

    return tempDiv.innerHTML;
  }

  extract(): ExtractedContent {
    return {
      title: this.extractTitle(),
      content: this.extractMainContent(),
      author: this.extractAuthor(),
      url: window.location.href,
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
        error: error instanceof Error ? error.message : "Unknown error",
      });
    }
  }
  return true;
});
