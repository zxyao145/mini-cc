import { test, expect, Page, APIRequestContext } from '@playwright/test';

// Each test owns a newly created article; the original 65-article library fixture is never changed.
let id: string;
async function detail(request: APIRequestContext) { return (await request.get(`/api/articles/${id}`)).json(); }
async function open(page: Page) {
  await page.goto(`/pages/article/${id}`);
  await expect(page.locator('.readable-content p')).toHaveCount(90);
  await expect(page.getByRole('button',{name:'从头阅读',exact:true})).toBeVisible();
}
async function userScroll(page: Page, paragraph: number) {
  const y = await page.locator('.readable-content p').nth(paragraph).evaluate(el=>el.getBoundingClientRect().top+window.scrollY-window.innerHeight*.2);
  await page.mouse.move(200,300);
  // Wheel is the user's intent; scrollTo only supplies a consistent target for desktop and touch projects.
  await page.mouse.wheel(0,100);
  await page.evaluate(top=>window.scrollTo({top,behavior:'instant'}),y);
  await page.waitForTimeout(3400);
}
async function anchorParagraph(page: Page) {
  return page.locator('.readable-content p').evaluateAll(nodes=>nodes.findIndex(node=>node.getBoundingClientRect().bottom>window.innerHeight*.2));
}

test.beforeEach(async({page})=>{
  const login=await page.request.post('/api/account/login',{form:{userName:process.env.E2E_USERNAME||'mini_cc',password:process.env.E2E_PASSWORD||'mini_cc_password',rememberMe:'false'}});
  expect(login.ok()).toBe(true);
  const body=Array.from({length:90},(_,i)=>`<p>Paragraph ${i} — 缓存与阅读位置。${'A long technical paragraph with stable text anchors and 中文内容。'.repeat(12)}</p>`).join('');
  const saved=await page.request.post('/api/articles/content',{data:{url:`https://reader-p2.example/${crypto.randomUUID()}`,title:'P2 temporary reading article',originalContent:body,readableContent:body}});
  expect(saved.status()).toBe(201);id=(await saved.json()).id;
});
test.afterEach(async({page})=>{if(id){expect((await page.request.delete(`/api/articles/${id}`)).ok()).toBe(true);id='';}});

test('requires active scroll and dwell, then restores the same paragraph after reload and changed viewport/font geometry',async({page},testInfo)=>{
  await open(page);await page.waitForTimeout(3500);
  expect((await detail(page.request)).readingStatus).toBe('unread');
  expect((await detail(page.request)).progress).toBeNull();
  await userScroll(page,25);
  await expect.poll(async()=>(await detail(page.request)).readingStatus).toBe('reading');
  const stored=await detail(page.request);expect(stored.progress.percent).toBeGreaterThan(2);
  const before=await anchorParagraph(page);
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth)).toBe(true);
  const resetButton=page.getByRole('button',{name:'从头阅读',exact:true});
  expect(await resetButton.evaluate(button=>{const rect=button.getBoundingClientRect();return button.contains(document.elementFromPoint(rect.left+rect.width/2,rect.top+rect.height/2));})).toBe(true);
  await page.screenshot({path:testInfo.outputPath('reader.png')});
  await page.reload();await expect(page.locator('.readable-content p')).toHaveCount(90);
  await expect.poll(()=>anchorParagraph(page)).toBe(before);
  const nextViewport=testInfo.project.name==='desktop'?{width:375,height:812}:{width:1280,height:800};
  await page.setViewportSize(nextViewport);
  await page.addInitScript(()=>{const style=document.createElement('style');style.textContent='.readable-content { font-size:24px !important }';document.addEventListener('DOMContentLoaded',()=>document.head.append(style));});
  await page.reload();await expect(page.locator('.readable-content p')).toHaveCount(90);
  await expect.poll(()=>anchorParagraph(page)).toBe(before);
  expect((await detail(page.request)).progress.textOffset).toBe(stored.progress.textOffset);
});

test('keeps read/archive/unread independent, retains anchors, and resets with a usable version',async({page})=>{
  await open(page);await userScroll(page,18);
  await expect.poll(async()=>(await detail(page.request)).readingStatus).toBe('reading');
  const old=(await detail(page.request)).progress;
  await page.getByRole('button',{name:'标记已读',exact:true}).first().click();
  await expect(page.getByRole('button',{name:'标记未读',exact:true})).toBeVisible();
  expect((await detail(page.request)).isArchived).toBe(false);
  await page.getByRole('button',{name:'归档文章',exact:true}).click();
  await expect(page.getByRole('button',{name:'移回收件箱',exact:true})).toBeVisible();
  expect((await detail(page.request)).readingStatus).toBe('read');
  await page.getByRole('button',{name:'标记未读',exact:true}).click();
  await expect(page.getByRole('button',{name:'标记已读',exact:true}).first()).toBeVisible();
  expect((await detail(page.request)).progress.textOffset).toBe(old.textOffset);
  await page.reload();await expect.poll(()=>anchorParagraph(page)).toBe(18);
  await page.getByRole('button',{name:'从头阅读',exact:true}).click();
  await expect.poll(async()=>(await detail(page.request)).progress).toBeNull();
  const reset=await detail(page.request);expect(reset.readingStatus).toBe('unread');expect(reset.isArchived).toBe(true);expect(reset.progressUpdatedAt).toBeTruthy();
  await expect.poll(()=>page.evaluate(()=>window.scrollY)).toBe(0);
  await page.reload();await userScroll(page,12);
  await expect.poll(async()=>(await detail(page.request)).readingStatus).toBe('reading');
});

test('two page sessions surface conflicts, pause until a choice, and jump to another session anchor',async({page,context})=>{
  await open(page);
  const other=await context.newPage();
  try {
    await open(other);
    await page.bringToFront();await userScroll(page,20);
    await expect.poll(async()=>(await detail(page.request)).progress?.blockIndex).toBe(20);
    // Simulates visibility restoration deterministically in headless Chromium, which keeps tabs visible.
    await other.bringToFront();await other.evaluate(()=>document.dispatchEvent(new Event('visibilitychange')));
    await expect(other.getByRole('button',{name:'留在这里',exact:true})).toBeVisible();
    await other.getByRole('button',{name:'留在这里',exact:true}).click();
    await userScroll(other,40);
    await expect.poll(async()=>(await detail(page.request)).progress?.blockIndex).toBe(40);
    await page.bringToFront();
    // PUT from the stale first page must conflict and must not silently rebase/retry.
    await userScroll(page,30);
    await expect(page.getByRole('button',{name:'跳转',exact:true})).toBeVisible();
    expect((await detail(page.request)).progress.blockIndex).toBe(40);
    await page.waitForTimeout(2300);expect((await detail(page.request)).progress.blockIndex).toBe(40);
    const jump=page.getByRole('button',{name:'跳转',exact:true});
    expect(await jump.evaluate(button=>{const rect=button.getBoundingClientRect();return button.contains(document.elementFromPoint(rect.left+rect.width/2,rect.top+rect.height/2));})).toBe(true);
    await jump.click();
    await expect.poll(()=>anchorParagraph(page)).toBe(40);
  } finally {await other.close();}
});

test('failed progress stays unsynced and automatically retries after recovery',async({page})=>{
  await open(page);
  let failing=true;
  await page.route(`**/api/articles/${id}/progress`,async route=>{if(failing)await route.fulfill({status:503,contentType:'application/json',body:'{}'});else await route.continue();});
  await userScroll(page,22);
  await expect(page.getByText('进度暂未同步',{exact:true})).toBeVisible();
  expect((await detail(page.request)).progress).toBeNull();
  expect(await page.evaluate(key=>JSON.parse(sessionStorage.getItem(key)!).position.blockIndex,`progress:${id}`)).toBe(22);
  failing=false;
  await expect.poll(async()=>(await detail(page.request)).progress?.blockIndex,{timeout:10000}).toBe(22);
  await expect(page.getByText('进度暂未同步',{exact:true})).toHaveCount(0);
});

test('shows a dismissible end prompt and never automatically marks a completed scroll read',async({page})=>{
  await open(page);await page.mouse.wheel(0,100);await page.evaluate(()=>window.scrollTo(0,document.body.scrollHeight));await page.waitForTimeout(3400);
  await expect(page.getByRole('region',{name:'读完提示'})).toBeVisible();
  expect((await detail(page.request)).readingStatus).not.toBe('read');
  await page.getByRole('button',{name:'关闭读完提示'}).click();
  await expect(page.getByRole('region',{name:'读完提示'})).toHaveCount(0);
  await page.reload();await expect(page.getByRole('region',{name:'读完提示'})).toBeVisible();
  await page.getByRole('region',{name:'读完提示'}).getByRole('button',{name:'标记已读',exact:true}).click();
  await expect.poll(async()=>(await detail(page.request)).readingStatus).toBe('read');
  await expect(page.getByRole('region',{name:'读完提示'})).toHaveCount(0);
});
