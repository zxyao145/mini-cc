import { SITE_HANDLERS } from './handlers';
import { assertHttpPageUrl } from './page-meta';
import { applyOverrides } from './prepare';
import type { FetchContext, PageChoices, SiteHandler, SiteOverrides } from './types';

interface UrlOptions {
  handlers?: readonly SiteHandler[];
  timeoutMs?: number;
}

function httpUrl(raw: string): URL {
  assertHttpPageUrl(raw);
  return new URL(raw);
}

async function runHook<T>(
  hook: (ctx: FetchContext) => Promise<T | null>,
  ctx: FetchContext,
  timeoutMs = 5000,
): Promise<T | null> {
  const controller = new AbortController();
  let timer: ReturnType<typeof setTimeout> | undefined;
  const timeout = new Promise<null>(resolve => {
    timer = setTimeout(() => {
      resolve(null);
      controller.abort();
    }, timeoutMs);
  });

  try {
    return await Promise.race([hook({ fetch: ctx.fetch, signal: controller.signal }), timeout]);
  } finally {
    clearTimeout(timer);
  }
}

export async function resolveUrl(input: string, ctx: FetchContext, options?: UrlOptions): Promise<string> {
  try {
    const url = httpUrl(input);
    const handler = (options?.handlers ?? SITE_HANDLERS).find(handler => handler.resolve && handler.matches(url));
    if (!handler?.resolve) return input;

    const result = await runHook(context => handler.resolve!(url, context), ctx, options?.timeoutMs);
    if (!result) return input;
    httpUrl(result);
    return result;
  } catch {
    return input;
  }
}

export async function preHandleUrl(input: string, ctx: FetchContext, options?: UrlOptions): Promise<SiteOverrides | null> {
  try {
    const url = httpUrl(input);
    const handler = (options?.handlers ?? SITE_HANDLERS).find(handler => handler.preHandle && handler.matches(url));
    if (!handler?.preHandle) return null;

    return await runHook(context => handler.preHandle!(url, context), ctx, options?.timeoutMs);
  } catch {
    return null;
  }
}

export function mergeOverrides(choices: PageChoices, overrides: SiteOverrides): PageChoices {
  const link = applyOverrides(choices.link, overrides);
  if (!choices.article) return { ...choices, link };

  return { ...choices, link, article: applyOverrides(choices.article, overrides) };
}
