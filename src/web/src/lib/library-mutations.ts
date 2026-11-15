import { ArticleListItem } from '@/types';
import { invalidateLibraryCaches } from './library-cache';
export type BatchAction = 'markRead' | 'markUnread' | 'favorite' | 'unfavorite' | 'archive' | 'unarchive' | 'addTags' | 'removeTags' | 'trash' | 'restore' | 'purge';
export interface BatchResult { succeeded: string[]; failed: { id: string; reason: string }[] }
export interface LibraryMutation { ids?: string[]; changes?: Partial<ArticleListItem>; remove?: boolean }
export function notifyLibraryMutation(mutation: LibraryMutation = {}) {
  invalidateLibraryCaches();
  window.dispatchEvent(new CustomEvent('minicc:library-mutation', { detail: mutation }));
}
