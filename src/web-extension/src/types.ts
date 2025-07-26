export interface ContentFetchResult {
  url: string;
  title: string;
  originalContent: string;
  summary?: string;
  author?: string;
  contentType?: string;
}

export interface ExtractedContent {
  title: string;
  content: string;
  author?: string;
  url: string;
}