import { test,expect,Page,APIRequestContext } from '@playwright/test';

const defaults={fontSize:18,lineHeight:1.8,width:'standard',fontFamily:'system',theme:'system'};
let ids:string[]=[],original:typeof defaults;
async function login(request:APIRequestContext){expect((await request.post('/api/account/login',{form:{userName:process.env.E2E_USERNAME||'mini_cc',password:process.env.E2E_PASSWORD||'mini_cc_password',rememberMe:'false'}})).ok()).toBe(true);}
async function preferences(request:APIRequestContext){return (await request.get('/api/account/reader-preferences')).json();}
async function open(page:Page,id=ids[0]){await page.goto(`/pages/article/${id}`);await expect(page.locator('.readable-content p')).toHaveCount(45);}
async function settings(page:Page){await page.getByRole('button',{name:'阅读设置',exact:true}).click();return page.getByRole('dialog',{name:'阅读设置'});}
async function anchor(page:Page){return page.locator('.readable-content p').evaluateAll(nodes=>nodes.findIndex(node=>node.getBoundingClientRect().bottom>window.innerHeight*.2));}
async function positionAt(page:Page,index:number){await page.locator('.readable-content p').nth(index).evaluate(el=>window.scrollTo({top:el.getBoundingClientRect().top+scrollY-innerHeight*.2,behavior:'instant'}));await expect.poll(()=>anchor(page)).toBe(index);}
test.beforeEach(async({page})=>{
 ids=[];await login(page.request);original=await preferences(page.request);expect((await page.request.put('/api/account/reader-preferences',{data:defaults})).ok()).toBe(true);
 const tech='<pre><code>'+('const longIdentifier = "'+'x'.repeat(160)+'";\n').repeat(4)+'</code></pre><table><tbody><tr>'+Array.from({length:14},(_,i)=>`<td style="white-space:nowrap">Column ${i} long content</td>`).join('')+'</tr></tbody></table>';
 for(const kind of ['chinese','english','technical']){
  const paragraphs=Array.from({length:45},(_,i)=>`${kind!=='english'&&i%10===0?`<h${i%20===0?3:4}>Section ${i}</h${i%20===0?3:4}>`:''}<p>Paragraph ${i} — ${kind==='chinese'?'中文阅读与缓存。'.repeat(65):'English technical reading with stable anchors. '.repeat(18)}</p>${kind==='technical'&&i===5?tech:''}`).join('');
  const response=await page.request.post('/api/articles/content',{data:{url:`https://reader-p3.example/${crypto.randomUUID()}`,title:`P3 ${kind} temporary article`,originalContent:paragraphs,readableContent:paragraphs}});expect(response.status()).toBe(201);ids.push((await response.json()).id);
 }
});
test.afterEach(async({page})=>{
 for(const id of ids)expect((await page.request.delete(`/api/articles/${id}`)).ok()).toBe(true);
 if(original)expect((await page.request.put('/api/account/reader-preferences',{data:original})).ok()).toBe(true);
});

test('applies every setting, preserves highlighted text and current paragraph, persists through reload/article and another login',async({page,browser},testInfo)=>{
 await open(page);await positionAt(page,20);
 const text=await page.locator('.readable-content').textContent();const start=text!.indexOf('Paragraph 20');
 expect((await page.request.post(`/api/articles/${ids[0]}/highlights`,{data:{text:'Paragraph 20',startOffset:start,endOffset:start+12,note:'P3 preserved note',color:'#ffeb3b'}})).ok()).toBe(true);
 await page.reload();await expect(page.locator('[data-highlight-id]').first()).toBeVisible();await positionAt(page,20);
 const highlighted=await page.locator('.readable-content [data-highlight-id]').allTextContents();const before=await anchor(page);
 const dialog=await settings(page);await expect(dialog).toBeVisible();
 for(let i=0;i<6;i++)await dialog.getByRole('button',{name:'增大字号'}).click();
 await dialog.getByRole('slider',{name:'行距'}).focus();await page.keyboard.press('End');
 await dialog.getByRole('button',{name:'宽',exact:true}).click();await dialog.getByRole('button',{name:'衬线字体'}).click();await dialog.getByRole('button',{name:'深色',exact:true}).click();
 await expect(page.locator('.reader')).toHaveAttribute('data-reader-theme','dark');await expect(page.locator('.readable-content')).toHaveCSS('font-size','24px');
 await expect.poll(()=>anchor(page)).toBe(before);expect(await page.locator('.readable-content').textContent()).toBe(text);expect(await page.locator('.readable-content [data-highlight-id]').allTextContents()).toEqual(highlighted);
 await page.screenshot({path:testInfo.outputPath('reader-settings.png')});
 await page.keyboard.press('Escape');await expect(page.getByRole('button',{name:'阅读设置',exact:true})).toBeFocused();
 const expected={fontSize:24,lineHeight:2.2,width:'wide',fontFamily:'serif',theme:'dark'};await expect.poll(()=>preferences(page.request)).toEqual(expected);
 await page.reload();await expect(page.locator('.readable-content')).toHaveCSS('font-size','24px');await open(page,ids[1]);await expect(page.locator('.reader')).toHaveAttribute('data-reader-theme','dark');
 const other=await browser.newContext({baseURL:new URL(page.url()).origin,viewport:testInfo.project.use.viewport});
 try{await login(other.request);const otherPage=await other.newPage();await open(otherPage,ids[2]);await expect(otherPage.locator('.readable-content')).toHaveCSS('font-size','24px');await expect(otherPage.locator('.reader')).toHaveAttribute('data-reader-theme','dark');}finally{await other.close();}
 await page.screenshot({path:testInfo.outputPath('reader-preferences.png')});
});

test('keeps code monospace, tables and code horizontally scrollable, all article kinds within the viewport',async({page})=>{
 for(const id of ids){await open(page,id);const dialog=await settings(page);await dialog.getByRole('button',{name:'衬线字体'}).click();await dialog.getByRole('button',{name:'护眼',exact:true}).click();await page.keyboard.press('Escape');expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);}
 await expect(page.locator('pre')).toHaveCSS('overflow-x','auto');const mono=await page.locator('pre code').evaluate(el=>getComputedStyle(el).fontFamily);expect(mono).toMatch(/monospace/);
 const table=page.locator('.reader-table');await expect(table).toHaveCount(1);await expect(table).toHaveCSS('overflow-x','auto');expect(await table.evaluate(el=>el.scrollWidth>el.clientWidth)).toBe(true);
 expect(await page.locator('pre').evaluate(el=>el.scrollWidth>el.clientWidth)).toBe(true);expect(await page.locator('.readable-content').evaluate(el=>getComputedStyle(el).fontFamily)).toContain('Serif');
 const dialog=await settings(page);await dialog.getByRole('button',{name:'恢复默认'}).click();await expect(page.locator('.readable-content')).toHaveCSS('font-size','18px');await expect.poll(()=>preferences(page.request)).toEqual(defaults);
});

test('TOC selects actual h3/h4 levels, navigates without history, tracks sections and hides for heading-free English',async({page},testInfo)=>{
 await open(page);
 if(testInfo.project.name==='desktop'){
  await page.setViewportSize({width:1024,height:800});await page.getByRole('button',{name:'目录',exact:true}).click();
  const tabletToc=page.getByRole('dialog',{name:'目录'});await expect(tabletToc).toHaveAttribute('aria-modal','true');await expect(tabletToc).toHaveCSS('position','fixed');await expect(page.locator('.reader-backdrop-visible')).toBeVisible();
  expect(Math.round((await tabletToc.boundingBox())!.y+(await tabletToc.boundingBox())!.height)).toBe(800);await page.screenshot({path:testInfo.outputPath('reader-toc-tablet.png')});
  await page.setViewportSize({width:1280,height:800});await expect(page.locator('.reader-toc-control [role="dialog"][aria-label="目录"]')).toHaveCount(0);await page.setViewportSize({width:1024,height:800});
  const panel=await settings(page);await expect(panel).not.toHaveAttribute('aria-modal','true');await expect(panel).toHaveCSS('position','absolute');
  await page.setViewportSize({width:375,height:812});await expect(panel).toHaveAttribute('aria-modal','true');await expect(panel).toHaveCSS('position','fixed');
  await page.setViewportSize({width:1024,height:800});await expect(panel).not.toHaveAttribute('aria-modal','true');await expect(panel).toHaveCSS('position','absolute');await page.keyboard.press('Escape');
  await page.setViewportSize({width:1280,height:800});
 }
 let toc=page.locator('.reader-toc-sidebar nav');
 if(testInfo.project.name==='mobile'){await page.getByRole('button',{name:'目录',exact:true}).click();toc=page.getByRole('dialog',{name:'目录'}).getByRole('navigation');await expect(page.getByRole('dialog',{name:'目录'})).toHaveAttribute('aria-modal','true');}
 await expect(toc.getByRole('link')).toHaveCount(5);await page.screenshot({path:testInfo.outputPath('reader-toc.png')});const historyLength=await page.evaluate(()=>history.length);
 await toc.getByRole('link',{name:'Section 20',exact:true}).click();await expect.poll(()=>page.evaluate(()=>location.hash)).toContain('toc-');expect(await page.evaluate(()=>history.length)).toBe(historyLength);
 if(testInfo.project.name==='mobile')await expect(page.getByRole('dialog',{name:'目录'})).toHaveCount(0);
 await positionAt(page,30);await page.locator('.readable-content h4').nth(1).evaluate(el=>window.scrollTo({top:el.getBoundingClientRect().top+scrollY-innerHeight*.25,behavior:'instant'}));
 if(testInfo.project.name==='mobile'){await page.getByRole('button',{name:'目录',exact:true}).click();toc=page.getByRole('dialog',{name:'目录'}).getByRole('navigation');}
 await expect(toc.getByRole('link',{name:'Section 30',exact:true})).toHaveAttribute('aria-current','location');
 await open(page,ids[1]);await expect(page.getByRole('button',{name:'目录',exact:true})).toHaveCount(0);await expect(page.locator('.reader-toc-sidebar')).toHaveCount(0);
});

test('retains local edits when PUT fails and retries, then server adoption preserves a saved anchor despite stale local cache',async({page})=>{
 await open(page);let fail=true;await page.route('**/api/account/reader-preferences',async route=>{if(route.request().method()==='PUT'&&fail)await route.fulfill({status:503,contentType:'application/json',body:'{}'});else await route.continue();});
 const dialog=await settings(page);await dialog.getByRole('button',{name:'增大字号'}).click();await expect(dialog.getByText('偏好未同步，',{exact:false})).toBeVisible();await expect(page.locator('.readable-content')).toHaveCSS('font-size','19px');fail=false;await dialog.getByRole('button',{name:'重试',exact:true}).click();await expect(dialog.locator('p[role="status"]')).toHaveCount(0);await expect.poll(()=>preferences(page.request)).toEqual({...defaults,fontSize:19});
 await page.keyboard.press('Escape');await page.mouse.wheel(0,100);await positionAt(page,25);await expect.poll(async()=>(await (await page.request.get(`/api/articles/${ids[0]}`)).json()).progress?.blockIndex,{timeout:10000}).toBeGreaterThan(20);
 await page.request.put('/api/account/reader-preferences',{data:{...defaults,fontSize:28,lineHeight:2.2}});
 await page.evaluate(value=>localStorage.setItem('reader-preferences',JSON.stringify(value)),{...defaults,fontSize:24});
 const hydrationErrors:string[]=[];page.on('console',message=>{if(message.type()==='error'&&/hydration|didn't match|server rendered/i.test(message.text()))hydrationErrors.push(message.text());});
 let release!:()=>void;const delay=new Promise<void>(resolve=>{release=resolve;});await page.route('**/api/account/reader-preferences',async route=>{if(route.request().method()==='GET')await delay;await route.continue();});
 await page.reload();await expect(page.locator('.readable-content p')).toHaveCount(45);await expect(page.locator('.readable-content')).toHaveCSS('font-size','24px');await expect.poll(()=>anchor(page)).toBe(25);const before=await anchor(page);release();await expect(page.locator('.readable-content')).toHaveCSS('font-size','28px');await expect.poll(()=>anchor(page)).toBe(before);expect(hydrationErrors).toEqual([]);
});
