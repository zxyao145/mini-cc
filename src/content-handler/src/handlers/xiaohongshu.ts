import { hostMatches, validImageUrl } from '../page-meta';
import type { SiteHandler } from '../types';

interface InitialNote {
  noteId?: string;
  imageList?: { urlDefault?: string; urlPre?: string }[];
  tagList?: { type?: string; name?: string }[];
}

function readInitialNote(document: Document, id: string): InitialNote | undefined {
  const assignment = /^window\.__INITIAL_STATE__\s*=\s*/;
  for (const script of document.querySelectorAll('script:not([src])')) {
    const text = script.textContent?.trim() ?? '';
    if (!assignment.test(text)) continue;
    try {
      // The serialized payload includes bare undefined values. Keep quoted strings verbatim.
      const state = JSON.parse(text.replace(assignment, '').replace(/;$/, '')
        .replace(/"(?:[^"\\]|\\.)*"|\bundefined\b/g, value => value === 'undefined' ? 'null' : value)) as {
          note?: { noteDetailMap?: Record<string, { note?: InitialNote }> };
        };
      const note = state.note?.noteDetailMap?.[id]?.note;
      if (note?.noteId === id) return note;
    } catch {
      // A different script format is optional. The rendered DOM still works.
    }
  }
}

function imageUrl(raw: string | null | undefined, baseUrl: string): string {
  const src = typeof raw === 'string' && raw.trim() && validImageUrl(raw, baseUrl);
  if (!src) return '';
  const url = new URL(src);
  const signedPath = url.pathname.match(/^\/\d{12}\/[a-f\d]{32}\/(.+)$/i)?.[1];
  if (hostMatches(url, 'xhscdn.com') && url.hostname.startsWith('sns-webpic-') && signedPath) {
    // The same object path and image transform work on the unsigned CDN.
    return validImageUrl(`https://sns-img-qc.xhscdn.com/${signedPath}`, baseUrl);
  }
  return src;
}

function appendDescription(article: HTMLElement, description: Element): void {
  const document = article.ownerDocument;
  let paragraph = document.createElement('p');
  function flush() {
    if (paragraph.textContent?.trim() || paragraph.querySelector('img')) article.append(paragraph);
    paragraph = document.createElement('p');
  }
  function append(node: Node) {
    if (node.nodeType === 3) {
      const lines = (node.textContent ?? '').split(/\r\n?|\n/);
      for (const [index, line] of lines.entries()) {
        if (index) flush();
        paragraph.append(line);
      }
    } else if (node.nodeName === 'BR') {
      flush();
    } else if (node.nodeName === 'A' || node.nodeName === 'IMG') {
      paragraph.append(node.cloneNode(true));
    } else if (node.nodeType === 1) {
      node.childNodes.forEach(append);
    }
  }
  description.childNodes.forEach(append);
  flush();
}

export const xiaohongshu: SiteHandler = {
  name: 'xiaohongshu',
  matches: url => hostMatches(url, 'xiaohongshu.com'),
  preParse(document) {
    const id = new URL(document.URL).pathname.match(/^\/(?:explore|discovery\/item)\/([a-z\d]+)\/?$/i)?.[1];
    if (!id) return;
    const rendered = Array.from(document.querySelectorAll('#noteContainer, .note-container'));
    const note = rendered.find(element => element.getAttribute('data-note-id') === id)
      ?? (rendered.length === 1 && !rendered[0].hasAttribute('data-note-id') ? rendered[0] : undefined);
    if (!note) return;

    const detail = note.querySelector('#detail-desc');
    const description = detail?.querySelector('.note-text') ?? detail;
    const title = note.querySelector('#detail-title')?.textContent?.trim()
      || document.title.trim().replace(/\s+-\s+小红书$/, '');
    const author = note.querySelector('.author-wrapper .username')?.textContent?.trim();
    const isVideo = note.getAttribute('data-type') === 'video';
    const isModal = note.closest('.note-detail-mask:not(.undertake-ssr)');
    const initial = isModal ? undefined : readInitialNote(document, id);

    const src = (raw: string | null | undefined) => imageUrl(raw, document.URL);
    const slides = note.querySelectorAll('.note-slider .swiper-slide:not(.swiper-slide-duplicate)');
    const carousel = new Map<number, string>();
    for (const [index, slide] of Array.from(slides).entries()) {
      const image = slide.querySelector('img:not(.live-img), img.note-slider-img');
      const url = src(image?.getAttribute('data-src')) || src(image?.getAttribute('src'));
      const slideIndex = slide.getAttribute('data-swiper-slide-index');
      if (url) carousel.set(slideIndex && /^\d+$/.test(slideIndex) ? Number(slideIndex) : index, url);
    }
    if (!slides.length && !isVideo) {
      for (const [index, image] of Array.from(note.querySelectorAll('img.note-slider-img')).entries()) {
        const url = src(image.getAttribute('data-src')) || src(image.getAttribute('src'));
        if (url) carousel.set(index, url);
      }
    }
    if (Array.isArray(initial?.imageList)) {
      for (const [index, image] of initial.imageList.entries()) {
        const url = src(image?.urlDefault) || src(image?.urlPre);
        if (url && !carousel.has(index)) carousel.set(index, url);
      }
    }

    const poster = src(note.querySelector('video[poster]')?.getAttribute('poster'));
    const sources = isVideo ? [poster || carousel.get(0) || '']
      : Array.from(carousel).sort(([left], [right]) => left - right).map(([, url]) => url);
    const images = new Set(sources.filter(Boolean));
    const statePlace = Array.isArray(initial?.tagList)
      ? initial.tagList.find(tag => tag?.type === 'poi')?.name
      : undefined;
    const place = note.querySelector('.poi-info-address')?.textContent?.trim() || (typeof statePlace === 'string' ? statePlace.trim() : '');
    const article = document.createElement('article');
    for (const src of images) {
      const figure = document.createElement('figure');
      const img = document.createElement('img');
      img.setAttribute('src', src);
      figure.append(img);
      article.append(figure);
    }
    if (description) appendDescription(article, description);
    if (place) {
      const paragraph = document.createElement('p');
      paragraph.textContent = `📍 ${place}`;
      article.append(paragraph);
    }
    if (!article.textContent?.trim()) {
      if (!images.size || !title) return;
      const caption = document.createElement('p');
      caption.textContent = title;
      article.append(caption);
    }

    document.body.replaceChildren(article);
    return { title, author, imageUrl: images.values().next().value, suggested: 'article' };
  },
};
