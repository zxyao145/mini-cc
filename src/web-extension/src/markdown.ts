import TurndownService from 'turndown';
import { strikethrough } from 'turndown-plugin-gfm';
import { extractPageTags, isMetaTrusted, pageAuthor, pageTitle } from './page-meta';
import { isArticlePage } from './classify-page';
import { parseReadable } from './readable';

export function htmlToMarkdown(html: string): string {
  const service = new TurndownService({
    headingStyle: 'atx',
    bulletListMarker: '-',
    emDelimiter: '*',
    strongDelimiter: '**',
    codeBlockStyle: 'fenced',
    linkStyle: 'inlined',
  });
  service.use(strikethrough);
  service.addRule('preservedTaskState', {
    filter: node => node.nodeName === 'WBR' && node.hasAttribute('data-minicc-task'),
    replacement: (_content, node) => node.getAttribute('data-minicc-task') === 'checked' ? '[x]' : '[ ]',
  });
  service.addRule('tableCell', {
    filter: ['th', 'td'],
    replacement: (content, node) => {
      const cell = content.trim().replace(/\s*\n\s*/g, '<br>').replace(/\|/g, '\\|');
      return (node.previousElementSibling ? ' ' : '| ') + cell + ' |';
    },
  });
  service.addRule('tableRow', {
    filter: 'tr',
    replacement: content => content ? '\n' + content : '',
  });
  service.addRule('tableSection', {
    filter: ['thead', 'tbody', 'tfoot'],
    replacement: content => content,
  });
  service.addRule('tableCaption', { filter: 'caption', replacement: () => '' });
  service.addRule('table', {
    filter: 'table',
    replacement: (content, node) => {
      const firstRow = Array.from((node as HTMLTableElement).rows).find(row => row.cells.length);
      if (!firstRow) return '';
      const cells = Array.from(firstRow.cells);
      const heading = firstRow.parentElement?.nodeName === 'THEAD' || cells.every(cell => cell.nodeName === 'TH');
      const rows = content.trim().split('\n').filter(row => row.trim());
      const header = heading ? rows.shift() : '|' + '   |'.repeat(cells.length);
      const marks = new Map([['left', ':--'], ['right', '--:'], ['center', ':-:']]);
      const separator = '|' + cells.map(cell => ' ' + (marks.get(cell.getAttribute('align')?.toLowerCase() ?? '') ?? '---') + ' |').join('');
      const caption = Array.from(node.children).find(child => child.nodeName === 'CAPTION');
      const captionText = caption?.textContent?.trim().replace(/\s+/g, ' ');
      const prefix = captionText ? service.escape(captionText) + '\n\n' : '';
      return `\n\n${prefix}${[header, separator, ...rows].join('\n')}\n\n`;
    },
  });
  service.remove(['script', 'style']);
  service.addRule('removeDataImages', {
    filter: node => node.nodeName === 'IMG' && (node.getAttribute('src') ?? '').toLowerCase().startsWith('data:'),
    replacement: () => '',
  });
  service.addRule('fencedCodeBlock', {
    filter: node => node.nodeName === 'PRE',
    replacement: (_content, node) => {
      const code = node.getElementsByTagName('code')[0];
      const languagePattern = /(?:^|\s)(?:language|lang)-([\w+#.-]+)/;
      const className = node.getAttribute('class') ?? '';
      const codeClassName = code?.getAttribute('class') ?? '';
      const wrapperClass = node.parentElement?.getAttribute('class') ?? '';
      const highlightedLanguage = wrapperClass.match(/(?:^|\s)highlight-(?:text|source)-([\w+#.]+)(?:[-\s]|$)/)?.[1];
      const language = className.match(languagePattern)?.[1] ?? codeClassName.match(languagePattern)?.[1]
        ?? highlightedLanguage ?? '';
      const source = node.textContent ?? '';
      let longestBackticks = 0;
      for (const match of source.matchAll(/`+/g)) longestBackticks = Math.max(longestBackticks, match[0].length);
      const fence = '`'.repeat(Math.max(3, longestBackticks + 1));
      const body = source.replace(/\r\n?/g, '\n').replace(/\n$/, '');
      return `\n\n${fence}${language}\n${body}\n${fence}\n\n`;
    },
  });

  // Turndown already separates Markdown blocks with at most two newlines.
  return service.turndown(html.replace(/\r\n?/g, '\n')) + '\n';
}

export function yamlString(value: string): string {
  const escaped = value
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"')
    .replace(/\r\n?|\n/g, '\\n')
    .replace(/\t/g, '\\t')
    .replace(/[\u0000-\u0008\u000B-\u001F\u007F-\u009F]/g, '');
  return `"${escaped}"`;
}

export function localDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function frontMatter(meta: { title: string; url: string; author?: string; date: string; tags?: string[] }): string {
  const lines = ['---', `title: ${yamlString(meta.title)}`, `url: ${yamlString(meta.url)}`];
  if (meta.author?.trim()) lines.push(`author: ${yamlString(meta.author)}`);
  lines.push(`date: ${meta.date}`);
  if (meta.tags?.length) lines.push(`tags: [${meta.tags.map(yamlString).join(', ')}]`);
  lines.push('---', '');
  return lines.join('\n');
}

function singleLine(value: string): string {
  return value.replace(/\s+/g, ' ').trim();
}

export function buildMarkdown(document: Document, loadedUrl: string, now = new Date()): string {
  const url = document.URL;
  const trusted = isMetaTrusted(loadedUrl, url);
  const readable = parseReadable(document, 'markdown');
  const fields = { url, date: localDate(now), tags: trusted ? extractPageTags(document) : undefined };

  if (readable && isArticlePage(document, readable, trusted)) {
    const title = (trusted ? readable.title.trim() : '') || document.title.trim();
    const front = frontMatter({ ...fields, title, author: trusted ? readable.byline : undefined });
    return `${front}\n# ${singleLine(title)}\n\n${htmlToMarkdown(readable.html)}`;
  }

  const title = pageTitle(document, trusted);
  const front = frontMatter({ ...fields, title, author: trusted ? pageAuthor(document) : undefined });
  const escapedTitle = singleLine(title).replace(/\\/g, '\\\\').replace(/[\[\]]/g, '\\$&');
  const escapedUrl = url.replace(/[()]/g, '\\$&');
  return `${front}\n[${escapedTitle}](${escapedUrl})\n`;
}
