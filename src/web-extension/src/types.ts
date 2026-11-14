import type { ExtractedPage } from '@minicc/content-handler';

export const DEFAULT_API_URL = 'http://localhost:5000';

export interface SavePageMessage {
  action: 'savePage';
  content: ExtractedPage;
}

export type SavePageResponse =
  | { success: true }
  | { success: false; error: string };
