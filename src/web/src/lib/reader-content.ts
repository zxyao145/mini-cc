/** Decorations must never introduce/remove article text: offsets are persisted by highlights/progress. */
export function decorateReaderContent(content: HTMLElement) {
  content.querySelectorAll('h1,h2,h3,h4,h5,h6').forEach((heading, index) => {
    if (heading.id) return;
    let suffix = index;
    while (content.querySelector(`[id="toc-${suffix}"]`)) suffix++;
    heading.id = `toc-${suffix}`;
  });
  content.querySelectorAll('table').forEach(table => {
    if (table.parentElement?.classList.contains('reader-table')) return;
    const wrapper = document.createElement('div'); wrapper.className = 'reader-table';
    table.before(wrapper); wrapper.append(table);
  });
  content.querySelectorAll('img').forEach(image => { image.loading = 'lazy'; image.decoding = 'async'; });
  content.querySelectorAll<HTMLAnchorElement>('a[href]').forEach(link => {
    const href = link.getAttribute('href')?.trim();
    if (!href || href.startsWith('#') || (link.protocol !== 'http:' && link.protocol !== 'https:')) return;
    link.target = '_blank';
    link.relList.add('noopener', 'noreferrer');
  });
}
