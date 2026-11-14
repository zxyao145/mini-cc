'use client';
import { FormEvent, useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';
import axios from 'axios';
import { feedApi, tagApi } from '@/lib/api';
import { Feed, FeedPreview, Tag } from '@/types';
import styles from './feeds.module.scss';

export function feedError(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data;
    if (data?.existingFeedId) return '这个订阅源已添加，请在订阅列表中查看。';
    if (typeof data?.error === 'string') return data.error;
  }
  return '操作失败，请检查地址或网络后重试。';
}
export default function FeedDialog({ feed, onClose, onDone }: { feed?: Feed; onClose: () => void; onDone: (message: string) => void }) {
  const dialog = useRef<HTMLDialogElement>(null);
  const [preview, setPreview] = useState<FeedPreview | null>(null), [tags, setTags] = useState<Tag[]>([]);
  const [selected, setSelected] = useState(feed?.tags.map(t => t.id) || []);
  const [pending, setPending] = useState(false), [error, setError] = useState('');
  const [tagsError, setTagsError] = useState(false);
  useEffect(() => { dialog.current?.showModal(); void tagApi.getTags().then(setTags).catch(() => setTagsError(true)); }, []);
  const confirmStep = !!feed || !!preview;
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const values = new FormData(event.currentTarget); setPending(true); setError('');
    try {
      if (!confirmStep) { setPreview(await feedApi.preview(String(values.get('url')).trim())); }
      else {
        const title = String(values.get('title')).trim();
        if (feed) { await feedApi.edit(feed.id, { title, tagIds: selected }); onDone('订阅已更新'); }
        else { const result = await feedApi.create(preview!.feedUrl, title, selected); onDone(`已导入 ${result.imported} 篇`); }
      }
    } catch (e) { setError(feedError(e)); }
    finally { setPending(false); }
  }
  return <dialog ref={dialog} className={styles.dialog} aria-labelledby="feed-dialog-title" onCancel={event => { event.preventDefault(); if (!pending) onClose(); }}>
    <header><div><span className={styles.eyebrow}>{feed ? '订阅设置' : `添加订阅 · ${confirmStep ? '2 / 2' : '1 / 2'}`}</span><h2 id="feed-dialog-title">{feed ? '编辑订阅' : confirmStep ? '确认订阅' : '添加订阅'}</h2></div><button type="button" aria-label="关闭订阅对话框" disabled={pending} onClick={onClose}><X size={20}/></button></header>
    <form onSubmit={submit}>
      {!confirmStep ? <><label>订阅地址<input name="url" type="url" required autoFocus placeholder="https://example.com/feed" /></label><p className={styles.muted}>支持 RSS 和 Atom，也可以输入网站地址来查找订阅。</p></> : <>
        {preview && <section className={styles.preview}><p className={styles.muted}>{preview.siteUrl || preview.feedUrl}</p><ul>{preview.items.map((item, index) => <li key={index}><span>{item.title}</span><small>{item.contentKind === 'summaryOnly' ? '仅摘要' : '全文'}{item.publishedAt && ` · ${new Date(item.publishedAt).toLocaleDateString()}`}</small></li>)}</ul></section>}
        <label>订阅名称<input key="title" name="title" required maxLength={500} defaultValue={feed?.title || preview?.title} /></label>
        <fieldset><legend>默认标签</legend><p className={styles.muted}>只应用于今后新导入的文章。</p><div className={styles.tags}>{tags.map(tag => <label key={tag.id}><input type="checkbox" checked={selected.includes(tag.id)} onChange={e => setSelected(ids => e.target.checked ? [...ids, tag.id] : ids.filter(id => id !== tag.id))}/>{tag.name}</label>)}</div>{!tags.length && !tagsError && <p className={styles.muted}>暂无标签，可稍后编辑。</p>}{tagsError && <p role="alert">标签加载失败，请关闭后重试。</p>}</fieldset>
      </>}
      {error && <p className={styles.error} role="alert">{error}</p>}
      <footer>{preview && <button type="button" disabled={pending} onClick={() => { setPreview(null); setError(''); }}>返回地址</button>}<button type="button" disabled={pending} onClick={onClose}>取消</button><button className={styles.primary} disabled={pending || (confirmStep && tagsError)}>{pending ? '处理中…' : feed ? '保存' : confirmStep ? '确认添加' : '预览订阅'}</button></footer>
    </form>
  </dialog>;
}
