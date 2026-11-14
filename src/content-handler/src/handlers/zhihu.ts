import { hostMatches, validImageUrl } from '../page-meta';
import type { SiteHandler } from '../types';

function restoreImages(content: Element): void {
  content.querySelectorAll('noscript').forEach(node => node.remove());
  for (const img of content.querySelectorAll('img[data-original], img[data-actualsrc]')) {
    const base = content.ownerDocument.baseURI;
    const original = img.getAttribute('data-original');
    const actual = img.getAttribute('data-actualsrc');
    const src = (original && validImageUrl(original, base)) || (actual && validImageUrl(actual, base));
    if (src) {
      img.setAttribute('src', src);
      img.removeAttribute('srcset');
      img.removeAttribute('data-original');
      img.removeAttribute('data-actualsrc');
    }
  }
}

export const zhihu: SiteHandler = {
  name: 'zhihu',
  matches: url => hostMatches(url, 'zhihu.com'),
  preParse(document) {
    const url = new URL(document.URL);
    if (url.hostname === 'zhuanlan.zhihu.com') {
      const content = document.querySelector('.Post-RichText, .Post-RichTextContainer');
      if (!content) return;
      restoreImages(content);
      return { title: document.querySelector('.Post-Title')?.textContent?.trim() };
    }

    if (!/^\/question\/\d+(?:\/answer\/\d+)?\/?$/.test(url.pathname)) return;
    const header = document.querySelector('.QuestionHeader');
    const title = header?.querySelector('.QuestionHeader-title')?.textContent?.trim();
    if (!title) return;
    const description = header?.querySelector('.QuestionRichText, .QuestionHeader-detail .RichText');
    const article = document.createElement('article');
    const question = document.createElement('h3');
    question.textContent = title;
    article.append(question);
    if (description) {
      description.querySelectorAll('button').forEach(button => button.remove());
      article.append(description);
    }

    for (const answer of document.querySelectorAll('.AnswerItem')) {
      const content = answer.querySelector('.RichContent-inner .RichText, .RichContent-inner');
      if (!content) continue;
      const section = document.createElement('section');
      const author = answer.querySelector('.AuthorInfo-name a, [itemprop="author"] [itemprop="name"]')
        ?? answer.querySelector('.AuthorInfo-name');
      const name = author?.textContent?.trim();
      if (name) {
        const byline = document.createElement('p');
        const bold = document.createElement('strong');
        bold.textContent = name;
        byline.append(bold);
        section.append(byline);
      }
      const text = content.querySelector('.RichText') ?? content;
      section.append(...Array.from(text.childNodes));
      article.append(section);
    }
    restoreImages(article);
    document.body.replaceChildren(article);
    return { title };
  },
};
