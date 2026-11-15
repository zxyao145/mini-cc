'use client';
import { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/arc/button/button';
import { Checkbox } from '@/components/arc/checkbox/checkbox';
import { Popover, PopoverContent, PopoverTrigger } from '@/components/arc/popover/popover';
import { TagWithArticleCount } from '@/types';
import { tagApi } from '@/lib/api';
import { BatchAction } from '@/lib/library-mutations';
import { useAppHeaderOffset } from '@/hooks/useAppHeaderOffset';
import styles from './BatchToolbar.module.css';

export const actionLabels: Record<BatchAction, string> = { markRead: '标记已读', markUnread: '标记未读', favorite: '收藏', unfavorite: '取消收藏', archive: '归档', unarchive: '移回收件箱', addTags: '添加标签', removeTags: '移除标签', trash: '删除', restore: '恢复', purge: '永久删除' };

export default function BatchToolbar({ selected, loaded, trash, busy, processed, onSelectAll, onCancel, onAction }: {
  selected: number;
  loaded: number;
  processed: number;
  trash: boolean;
  busy: boolean;
  onSelectAll: () => void;
  onCancel: () => void;
  onAction: (action: BatchAction, tagIds?: string[]) => void;
}) {
  const ref = useRef<HTMLElement>(null);
  useAppHeaderOffset(ref);
  const actions: BatchAction[] = trash ? ['restore', 'purge'] : ['markRead', 'markUnread', 'favorite', 'unfavorite', 'archive', 'unarchive', 'addTags', 'removeTags', 'trash'];
  return <section ref={ref} className={styles.bar} aria-label="批量操作" aria-busy={busy}>
    <div className={styles.summary}>
      <strong>已选 {selected} 篇</strong>
      <span>仅限当前已加载的 {loaded} 篇</span>
      <Button variant="ghost" size="sm" disabled={busy} onClick={onSelectAll}>全选已加载</Button>
      <Button variant="ghost" size="sm" disabled={busy} onClick={onCancel}>取消</Button>
    </div>
    <div className={styles.actions}>
      {actions.map(action => action === 'addTags' || action === 'removeTags' ? (
        <BatchTagAction key={action} action={action} disabled={busy || !selected} onApply={onAction} />
      ) : (
        <Button key={action} variant={action === 'purge' || action === 'trash' ? 'danger' : 'secondary'} size="sm" disabled={busy || !selected} onClick={() => onAction(action)}>{actionLabels[action]}</Button>
      ))}
    </div>
    {busy && <p className={styles.progress} role="status">处理中 {processed}/{selected}…</p>}
  </section>;
}

function BatchTagAction({ action, disabled, onApply }: {
  action: 'addTags' | 'removeTags';
  disabled: boolean;
  onApply: (action: BatchAction, tagIds: string[]) => void;
}) {
  const [open, setOpen] = useState(false);
  const [tags, setTags] = useState<TagWithArticleCount[]>([]);
  const [tagIds, setTagIds] = useState<string[]>([]);
  const [error, setError] = useState('');
  const label = actionLabels[action];

  useEffect(() => {
    if (!open) return;
    let active = true;
    tagApi.getTags().then(value => { if (active) setTags(value); }).catch(() => { if (active) setError('标签加载失败，请重新打开'); });
    return () => { active = false; };
  }, [open]);

  return <Popover open={open} onOpenChange={next => {
    if (next) { setTagIds([]); setError(''); }
    setOpen(next);
  }}>
    <PopoverTrigger asChild><Button variant="secondary" size="sm" disabled={disabled}>{label}</Button></PopoverTrigger>
    <PopoverContent className={styles.tagPopover} aria-label={label}>
      <fieldset disabled={disabled} className={styles.tags}>
        <legend>{label}</legend>
        <div className={styles.tagOptions}>
          {tags.map(tag => <Checkbox key={tag.id} compact label={tag.name} checked={tagIds.includes(tag.id)} disabled={tagIds.length >= 20 && !tagIds.includes(tag.id)} onCheckedChange={() => setTagIds(current => current.includes(tag.id) ? current.filter(id => id !== tag.id) : [...current, tag.id])} />)}
        </div>
        {error && <p role="alert">{error}</p>}
        {!tags.length && !error && <p>暂无标签</p>}
        <div className={styles.tagActions}>
          <Button variant="secondary" size="sm" onClick={() => setOpen(false)}>关闭</Button>
          <Button disabled={!tagIds.length || disabled} size="sm" onClick={() => { onApply(action, tagIds); setOpen(false); }}>应用</Button>
        </div>
      </fieldset>
    </PopoverContent>
  </Popover>;
}
