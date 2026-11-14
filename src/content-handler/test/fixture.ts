import { readFileSync } from 'node:fs';
import { JSDOM } from 'jsdom';

export function fixture(site: string, name: string, url: string): JSDOM {
  const path = new URL(`./fixtures/${site}/${name}.html`, import.meta.url);
  return new JSDOM(readFileSync(path, 'utf8'), { url });
}
