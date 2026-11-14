'use client';

import { FormEvent, useEffect, useRef, useState } from 'react';
import { Rss } from 'lucide-react';
import axios from 'axios';
import { Button } from '@/components/arc/button/button';
import { Checkbox } from '@/components/arc/checkbox/checkbox';
import { Dialog, DialogContent } from '@/components/arc/dialog/dialog';
import { Input } from '@/components/arc/input/input';
import { feedApi, tagApi } from '@/lib/api';
import { Feed, FeedPreview, Tag } from '@/types';
import styles from './feeds.module.css';

export function feedError(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data;
    if (data?.existingFeedId) return '这个订阅源已添加，请在订阅列表中查看。';
    if (typeof data?.error === 'string') return data.error;
  }
  return '操作失败，请检查地址或网络后重试。';
}

export default function FeedDialog({ feed, onClose, onDone }: { feed?: Feed; onClose: () => void; onDone: (message: string) => void }) {
  const urlInput = useRef<HTMLInputElement>(null);
  const titleInput = useRef<HTMLInputElement>(null);
  const [preview, setPreview] = useState<FeedPreview | null>(null);
  const [tags, setTags] = useState<Tag[]>([]);
  const [selected, setSelected] = useState(feed?.tags.map(tag => tag.id) || []);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState('');
  const [tagsError, setTagsError] = useState(false);
  const confirmStep = !!feed || !!preview;

  useEffect(() => {
    void tagApi.getTags().then(setTags).catch(() => setTagsError(true));
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const values = new FormData(event.currentTarget);
    setPending(true);
    setError('');
    try {
      if (!confirmStep) {
        setPreview(await feedApi.preview(String(values.get('url')).trim()));
      } else {
        const title = String(values.get('title')).trim();
        if (feed) {
          await feedApi.edit(feed.id, { title, tagIds: selected });
          onDone('订阅已更新');
        } else {
          const result = await feedApi.create(preview!.feedUrl, title, selected);
          onDone(`已导入 ${result.imported} 篇`);
        }
      }
    } catch (submitError) {
      setError(feedError(submitError));
    } finally {
      setPending(false);
    }
  }

  const close = () => {
    if (!pending) onClose();
  };
  const title = feed ? '编辑订阅' : confirmStep ? '确认订阅' : '添加订阅';

  return (
    <Dialog open onOpenChange={open => { if (!open) close(); }}>
      <DialogContent
        title={title}
        description={feed ? '订阅设置' : `添加订阅 · ${confirmStep ? '2 / 2' : '1 / 2'}`}
        closeLabel="关闭订阅对话框"
        closeDisabled={pending}
        onInteractOutside={event => event.preventDefault()}
        onEscapeKeyDown={event => { if (pending) event.preventDefault(); }}
        onOpenAutoFocus={event => {
          event.preventDefault();
          (feed ? titleInput : urlInput).current?.focus();
        }}
        className={styles.dialog}
        footer={
          <div className={styles.dialogActions}>
            {preview && !feed && (
              <Button
                type="button"
                variant="ghost"
                disabled={pending}
                onClick={() => { setPreview(null); setError(''); window.requestAnimationFrame(() => urlInput.current?.focus()); }}
              >
                返回地址
              </Button>
            )}
            <span className={styles.footerSpacer} />
            <Button type="button" variant="secondary" disabled={pending} onClick={close}>取消</Button>
            <Button
              type="submit"
              form="feed-dialog-form"
              disabled={pending || (confirmStep && tagsError)}
            >
              {pending ? '处理中…' : feed ? '保存' : confirmStep ? '确认添加' : '预览订阅'}
            </Button>
          </div>
        }
      >
        <form id="feed-dialog-form" onSubmit={submit} className={styles.dialogForm}>
          {!confirmStep ? (
            <>
              <Input
                ref={urlInput}
                label="订阅地址"
                name="url"
                type="url"
                placeholder="https://example.com/feed"
                required
                autoComplete="url"
                leading={<Rss size={16} aria-hidden="true" />}
                disabled={pending}
              />
              <p className={styles.muted}>支持 RSS 和 Atom 地址，也可以输入网站地址来查找订阅。</p>
            </>
          ) : (
            <>
              {preview && (
                <section className={styles.preview} aria-label="订阅预览">
                  <p className={styles.feedUrl}>{preview.siteUrl || preview.feedUrl}</p>
                  <ul>
                    {preview.items.map((item, index) => (
                      <li key={`${item.url}-${index}`}>
                        <span>{item.title}</span>
                        <small>{item.contentKind === 'summaryOnly' ? '仅摘要' : '全文'}{item.publishedAt && ` · ${new Date(item.publishedAt).toLocaleDateString('zh-CN')}`}</small>
                      </li>
                    ))}
                  </ul>
                </section>
              )}

              <Input
                ref={titleInput}
                label="订阅名称"
                name="title"
                required
                maxLength={500}
                defaultValue={feed?.title || preview?.title}
                disabled={pending}
              />

              <fieldset className={styles.tagFieldset}>
                <legend>默认标签</legend>
                <p className={styles.muted}>只应用于今后新导入的文章。</p>
                {tags.length > 0 ? (
                  <div className={styles.tagChoices}>
                    {tags.map(tag => (
                      <Checkbox
                        key={tag.id}
                        compact
                        label={tag.name}
                        checked={selected.includes(tag.id)}
                        disabled={pending}
                        onCheckedChange={checked => setSelected(ids => checked === true ? [...ids, tag.id] : ids.filter(id => id !== tag.id))}
                      />
                    ))}
                  </div>
                ) : !tagsError ? (
                  <p className={styles.muted}>暂无标签，可稍后编辑。</p>
                ) : (
                  <p role="alert" className={styles.error}>标签加载失败，请关闭后重试。</p>
                )}
              </fieldset>
            </>
          )}

          {error && <p className={styles.error} role="alert">{error}</p>}
        </form>
      </DialogContent>
    </Dialog>
  );
}
