 'use client';
import { useEffect, useRef, useState } from 'react';
import { TagWithArticleCount } from '@/types';
import { tagApi } from '@/lib/api';
import { BatchAction } from '@/lib/library-mutations';
import { useAppHeaderOffset } from '@/hooks/useAppHeaderOffset';
import styles from './BatchToolbar.module.scss';
export const actionLabels: Record<BatchAction, string> = { markRead: '标记已读', markUnread: '标记未读', favorite: '收藏', unfavorite: '取消收藏', archive: '归档', unarchive: '移回收件箱', addTags: '添加标签', removeTags: '移除标签', trash: '删除', restore: '恢复', purge: '永久删除' };
export default function BatchToolbar({ selected, loaded, trash, busy, processed, onSelectAll, onCancel, onAction }: {
  selected: number; loaded: number; processed: number; trash: boolean; busy: boolean; onSelectAll: () => void; onCancel: () => void; onAction: (action: BatchAction, tagIds?: string[]) => void;
}) {
  const ref = useRef<HTMLElement>(null); useAppHeaderOffset(ref);
  const [tagAction, setTagAction] = useState<'addTags' | 'removeTags' | null>(null);
  const [tags, setTags] = useState<TagWithArticleCount[]>([]); const [tagIds, setTagIds] = useState<string[]>([]); const [error, setError] = useState('');
  useEffect(() => {
    if (!tagAction) return;
    let active = true;
    tagApi.getTags().then(value => { if (active) setTags(value); }).catch(() => { if (active) setError('标签加载失败，请重新打开'); });
    return () => { active = false; };
  }, [tagAction]);
  const actions: BatchAction[] = trash ? ['restore', 'purge'] : ['markRead', 'markUnread', 'favorite', 'unfavorite', 'archive', 'unarchive', 'addTags', 'removeTags', 'trash'];
  return <section ref={ref} className={styles.bar} aria-label="批量操作" aria-busy={busy}>
    <div className={styles.summary}><strong>已选 {selected} 篇</strong><span>仅限当前已加载的 {loaded} 篇</span><button disabled={busy} onClick={onSelectAll}>全选已加载</button><button disabled={busy} onClick={onCancel}>取消</button></div>
    <div className={styles.actions}>{actions.map(action => <button key={action} disabled={busy || !selected} onClick={() => {
      if (action === 'addTags' || action === 'removeTags') { setTagAction(action); setTagIds([]); setError(''); }
      else onAction(action);
    }}>{actionLabels[action]}</button>)}</div>
    {busy && <p role="status">处理中 {processed}/{selected}…</p>}
    {tagAction && <fieldset disabled={busy} className={styles.tags}><legend>{actionLabels[tagAction]}</legend>
      {tags.map(tag => <label key={tag.id}><input type="checkbox" checked={tagIds.includes(tag.id)} disabled={tagIds.length >= 20 && !tagIds.includes(tag.id)} onChange={() => setTagIds(current => current.includes(tag.id) ? current.filter(id => id !== tag.id) : [...current, tag.id])} />{tag.name}</label>)}
      {error && <p role="alert">{error}</p>}{!tags.length && !error && <p>暂无标签</p>}
      <button disabled={!tagIds.length || busy} onClick={() => { onAction(tagAction, tagIds); setTagAction(null); }}>应用</button><button onClick={() => setTagAction(null)}>关闭</button>
    </fieldset>}
  </section>;
}
