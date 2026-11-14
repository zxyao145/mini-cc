import { hostMatches } from '../page-meta';
import type { SiteHandler } from '../types';

function parseAuthors(post: Element, section: HTMLElement): void {
  const document = post.ownerDocument;
  for (const signature of post.querySelectorAll('.post-signature')) {
    const details = signature.querySelector('.user-details');
    const name = details?.querySelector('a') ?? details;
    if (!name?.textContent?.trim()) continue;

    const paragraph = document.createElement('p');
    const author = document.createElement('b');
    author.append(name.cloneNode(true));
    const reputation = signature.querySelector('.reputation-score')?.textContent?.trim();
    const badges = Array.from(signature.querySelectorAll('span[title*="badges"]'),
      badge => badge.getAttribute('title')).join(', ');
    const date = signature.querySelector('.user-action-time')?.textContent?.trim();
    paragraph.append(author, [reputation && `${reputation} reputation`, badges, date]
      .filter(Boolean).map(value => ` - ${value}`).join(''));
    section.append(paragraph);
  }
}

function parseComments(post: Element, section: HTMLElement): void {
  const comments = post.querySelectorAll('.comments .comment:has(.comment-copy)');
  if (!comments.length) return;

  const document = post.ownerDocument;
  const heading = document.createElement('h3');
  heading.textContent = 'Comments';
  section.append(heading);
  for (const comment of comments) {
    const paragraph = document.createElement('p');
    const user = comment.querySelector('.comment-user');
    if (user) {
      const author = document.createElement('b');
      author.append(user.cloneNode(true));
      paragraph.append(author, ': ');
    }

    const copy = comment.querySelector('.comment-copy');
    if (copy) paragraph.append(...Array.from(copy.childNodes));
    const date = comment.querySelector('.relativetime-clean')?.textContent?.trim();
    if (date) paragraph.append(` - ${date}`);
    section.append(paragraph);
  }
}

function appendPost(article: HTMLElement, post: Element, label: string): void {
  const text = post.querySelector('[itemprop="text"], .js-post-body');
  if (!text) return;

  const document = post.ownerDocument;
  const voteCount = post.querySelector('[itemprop="upvoteCount"], .js-vote-count');
  const votes = voteCount?.getAttribute('data-value')?.trim() || voteCount?.textContent?.trim();
  const heading = document.createElement('h2');
  heading.textContent = votes ? `${label}: ${votes} vote${votes === '1' ? '' : 's'}` : label;

  const section = document.createElement('section');
  section.append(heading, ...Array.from(text.childNodes));
  parseAuthors(post, section);
  parseComments(post, section);
  article.append(section);
}

export const stackOverflow: SiteHandler = {
  name: 'stack-overflow',
  matches: url => hostMatches(url, 'stackoverflow.com'),
  preParse(document) {
    const question = document.querySelector('#question');
    if (!question) return;

    const article = document.createElement('article');
    appendPost(article, question, 'Question');
    for (const answer of document.querySelectorAll('#answers .answer')) {
      appendPost(article, answer, answer.classList.contains('accepted-answer') ? 'Accepted Answer' : 'Answer');
    }
    if (!article.childElementCount) return;

    const title = document.querySelector('#question-header h1')?.textContent?.trim();
    document.body.replaceChildren(article);
    return { title };
  },
};
