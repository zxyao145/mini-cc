import { createServer, Server } from 'node:http';
import { expect, request as playwrightRequest, test } from '@playwright/test';

const imageBytes = Buffer.from(
  'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/9qkAAAAASUVORK5CYII=',
  'base64',
);
const downloaderUserAgent = 'Mozilla/5.0 (Macintosh; Intel Mac OS X 11_1) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/102.0.4280.141 Safari/537.36';
const port = Number(process.env.E2E_IMAGE_PORT ?? 5019);
const imageUrl = (process.env.E2E_IMAGE_URL ?? 'http://127.0.0.1:5019').replace(/\/$/, '');
let imageServer: Server;
let articleId: string | undefined;
let browserRequestedSource: Promise<void>;
let allowApiDownload = () => {};

test.setTimeout(120_000);

test.beforeAll(async () => {
  browserRequestedSource = new Promise(resolve => { allowApiDownload = resolve; });
  imageServer = createServer(async (request, response) => {
    if (request.headers.referer) {
      response.writeHead(403).end();
      return;
    }

    if (request.headers['user-agent'] === downloaderUserAgent) {
      await browserRequestedSource;
    } else {
      allowApiDownload();
    }

    response.writeHead(200, { 'Content-Type': 'image/png' }).end(imageBytes);
  });
  await new Promise<void>((resolve, reject) => {
    imageServer.once('error', reject);
    imageServer.listen(port, '0.0.0.0', resolve);
  });
});

test.afterAll(async () => {
  allowApiDownload();
  if (imageServer?.listening) {
    await new Promise<void>((resolve, reject) => imageServer.close(error => error ? reject(error) : resolve()));
  }
  if (articleId) {
    const api = await playwrightRequest.newContext({ baseURL: process.env.E2E_BASE_URL });
    try {
      const login = await api.post('/api/account/login', {
        form: {
          userName: process.env.E2E_USERNAME || 'mini_cc',
          password: process.env.E2E_PASSWORD || 'mini_cc_password',
          rememberMe: 'false',
        },
      });
      expect(login.ok()).toBe(true);
      expect((await api.delete(`/api/articles/${articleId}`)).ok()).toBe(true);
      articleId = undefined;
    } finally {
      await api.dispose();
    }
  }
});

test.beforeEach(async ({ page }) => {
  const login = await page.request.post('/api/account/login', {
    form: {
      userName: process.env.E2E_USERNAME || 'mini_cc',
      password: process.env.E2E_PASSWORD || 'mini_cc_password',
      rememberMe: 'false',
    },
  });
  expect(login.ok()).toBe(true);
});

test('stores article images and serves them after the source disappears', async ({ page }, testInfo) => {
  const run = `${testInfo.project.name}-${crypto.randomUUID()}`;
  const sourceUrl = `${imageUrl}/a.png?run=${run}`;
  const readableContent = `<p>Image ${run}</p><img src="${sourceUrl}">`;
  const saved = await page.request.post('/api/articles/content', {
    data: {
      url: `https://e2e.invalid/${run}`,
      title: `Image ${run}`,
      originalContent: '<p>x</p>',
      readableContent,
      author: '',
      summary: '',
      imageUrl: '',
    },
  });
  expect(saved.status()).toBe(201);
  articleId = (await saved.json()).id;

  const image = page.locator('.readable-content img');
  await page.goto(`/pages/article/${articleId}`);
  await expect(image).toHaveAttribute('src', sourceUrl);
  await expect(image).toHaveAttribute('referrerpolicy', 'no-referrer');
  await expect.poll(() => image.evaluate(element => (element as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);

  await expect.poll(async () => {
    const response = await page.request.get(`/api/articles/${articleId}`);
    return (await response.json()).readableContent as string;
  }, { timeout: 90_000 }).toContain('/api/images/');

  await new Promise<void>((resolve, reject) => imageServer.close(error => error ? reject(error) : resolve()));
  await page.reload();
  await expect(image).toHaveAttribute('src', /^\/api\/images\//);
  await expect(image).toBeVisible();
  await expect.poll(() => image.evaluate(element => (element as HTMLImageElement).naturalWidth)).toBeGreaterThan(0);

  const storedImageUrl = await image.getAttribute('src');
  if (!storedImageUrl) throw new Error('Stored image URL was not rendered.');
  const storedImage = await page.request.get(storedImageUrl);
  expect(storedImage.status()).toBe(200);
  expect(storedImage.headers()['content-type']).toBe('image/png');
});
