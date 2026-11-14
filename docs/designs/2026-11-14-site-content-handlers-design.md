# 按网站提取策略与 content-handler SDK 设计

- 日期：2026-11-14
- 状态：已批准（2026-11-14）
- 参考：omnivore `packages/content/content-handler`（`src/content-handler.ts`、`src/index.ts`、`src/websites/*-handler.ts`）
- 下一步：按本文编写实施计划

---

## 1. 背景与目标

插件目前对所有网页走同一条路径：Readability + DOMPurify。小红书笔记保存后（用户截图）：

- 轮播里十几张图片全部丢失，封面也没有；
- 每行以 emoji 开头的独立行被挤成一整段；
- 标题带 “ - 小红书” 后缀；
- 末尾混进 “10-21 安徽”（日期 + IP 属地）。

本设计分两部分：

- **A. 提取（插件 + SDK）**
  1. **按网站选择提取策略**：参照 omnivore content-handler，为每个网站写一个 handler，在 Readability 之前改写页面或补充元数据。第一批 9 个网站：小红书、微信公众号、Wikipedia、YouTube、知乎、Medium、GitHub、Apple News、Stack Overflow。
  2. **把提取逻辑做成独立 SDK**：不依赖任何 Chrome API。插件现在用它，以后的 iOS App 也能用。iOS App 会通过系统分享按钮收到一个链接；手机端是否支持小红书另行评估。
- **B. 重新保存时替换正文（后端 + 阅读页）**：现在已存过完整内容的网页再存一次，会保留旧正文。改为用新抓到的内容替换，这样以前存坏的条目重新保存一次就能修好。

两部分互相独立，先做 A 再做 B。

**成功标准：** 第 4 节的测试和验收全部通过。

### 1.1 用户已定的事项

- 小红书：图片、换行、标题后缀、末尾杂项都要修。如果作者给笔记**添加了地点**，就带上这个地点；日期和 IP 属地（如“安徽”）不要。
- iOS App 的技术栈**还没定**。这次只保证 SDK 是纯 TypeScript、只接收 `Document`、网络请求由调用方注入；SDK 怎么打包给手机用，等 App 立项再定。
- 用户审阅逐站点方案时只对小红书提了修改，其余按第 3.3 节执行。
- 小红书图片地址带时间戳和签名，可能会过期：**先验证**；如果确实会过期，就在 handler 里改写成不带签名的永久地址，不改后端。
- 从发现页弹窗打开笔记时，如果页面上拿不到全部图片或地点：**接受缺失**，能拿到多少存多少，不额外读取页面运行时数据。
- 已存过的网页再次用插件保存文章时：**替换正文**（第 B 部分）。

### 1.2 非目标

- 构建 iOS App，或为它打包 SDK（生成 IIFE、验证 linkedom 兼容性等）。
- 手机端支持小红书，包括解析 `xhslink.com` 短链。
- YouTube 内嵌播放器和字幕。阅读页的 DOMPurify 会剥掉 iframe，YouTube 继续存成链接。
- 第 B 部分以外的后端、API、前端改动。例如微信发布时间：保存 API 没有 `publishedAt` 字段，这次忽略。
- 改变“保存链接”和订阅源导入的规则：它们仍然不改动已有正文。
- 保存时由后端下载图片存档。
- omnivore 里其余的网站和 newsletter handler（Twitter、TikTok、Bloomberg、Substack 等）。
- Stack Overflow 以外的 Stack Exchange 站点。
- 服务端提取。

### 1.3 反目标（即使能用，出现这些也算失败）

- **handler 出错导致整页存不了。** 网站改版后选择器找不到、或 handler 抛异常时，必须退回默认的 Readability 流程，结果与没有 handler 时一致。
- **改写了用户正在看的页面。** 所有 DOM 改写只能作用在克隆上；`originalContent` 仍是未改动的原页面 HTML。
- 原页面能看到的图片在保存结果里消失（本次要修的原始问题）。
- handler 误伤其他网站。域名匹配必须精确：`zhihu.com` 能匹配 `www.zhihu.com`，不能匹配 `evilzhihu.com`。
- SDK 用到 `chrome.*`，或依赖调用方传入的 `Document` 以外的浏览器全局对象（`window`、`location`、`navigator`）。
- “Copy Markdown” 因为等待网络而变成慢的异步流程，导致剪贴板写入被浏览器拒绝。
- 没有 handler 的网站行为发生变化（现有测试原样迁移，断言不变）。
- 重新保存丢失标签、笔记、收藏、归档、已读状态。
- 替换正文后，之前打开的阅读页继续按旧正文写入阅读位置或定位高亮。

---

## 2. 现状（实现者应复用）

| 位置 | 现状 |
|---|---|
| `src/web-extension/src/extract-page.ts` | `extractPage(document, loadedUrl): PageChoices`：生成 link 元数据，跑 `parseReadable`，再用 `isArticlePage` 决定 `suggested`。 |
| `src/web-extension/src/readable.ts` | `parseReadable(document, mode)`：克隆 document → markdown 模式下插入任务列表标记 → Readability → DOMPurify（`createDOMPurify(document.defaultView!)`）。 |
| `src/web-extension/src/extract-article.ts` | `extractArticle`：`originalContent = document.documentElement.outerHTML`，标题/作者/摘要按 `ARTICLE_LIMITS` 截断。 |
| `src/web-extension/src/page-meta.ts` | `assertHttpPageUrl`、`isMetaTrusted`（站内跳转后不信任 head 元数据）、`metaContent`、`pageTitle`、`pageAuthor`、`extractImageUrl`（限 http/https、≤2000）、`youtubeVideoId`、`jsonLdArticles`、`extractPageTags`。 |
| `src/web-extension/src/classify-page.ts` | `isArticlePage`：媒体 URL → link；正文 ≥500 字（有文章信号时 ≥200）→ article。短的小红书笔记会因此被判成 link。 |
| `src/web-extension/src/markdown.ts` | `buildMarkdown(document, loadedUrl)` 与 `htmlToMarkdown`，同步执行，复用 `parseReadable(…, 'markdown')`。 |
| `src/web-extension/src/types.ts` | `ARTICLE_LIMITS`、`ArticlePage`、`LinkPage`、`ExtractedPage`、`PageChoices`，以及插件专用的 `DEFAULT_API_URL`、`SavePageMessage`、`SavePageResponse`。 |
| `content.ts` / `popup.ts` / `background.ts` / `clipboard.ts` | 用到 Chrome API 的只有这几个文件。popup 通过 `extractContent` / `extractMarkdown` 消息向内容脚本同步取数据；popup 有 `<all_urls>` 主机权限，可以跨域 `fetch`。 |
| 后端 `ArticleDomainService.CreateArticleFromContent` | 保存时用 `ImageProxyService.RewriteImages` 把正文图片改写为签名的代理地址。阅读页经代理加载图片，不带浏览器的 Referer。图片代理（`src/image-proxy/start_imageproxy.sh`）带磁盘缓存，但只在第一次被打开时抓取图片。 |
| 后端 `SaveArticleContentCommandHandler` | 事务内先加锁，再按 `UrlNormalizer.Normalize` 查找已有文章（含回收站），找到就恢复。只有 `ContentKind != Full` 时才替换正文：调用 `ReplaceContent`、`FillMissingMetadata`，用严格更新的微秒版本号 `ResetProgress`，并把全部高亮置为 `Pending`。`Full` 时正文和标题都不变。 |
| 前端 `hooks/useReadingProgress.ts`、`Client.tsx` | 已打开的阅读页能检测到正文被替换，但判断依据是 `contentKind` 从 `summaryOnly` 变为 `full`（`observeContentKind`、`storedProgress`）；检测到后暂停旧坐标，等用户重新加载。`Full` → `Full` 的替换检测不到。 |
| 测试 | `tsx --test test/*.test.ts` + jsdom；`test/with-globals.ts` 用于模拟全局对象。 |
| 仓库结构 | 没有 pnpm workspace；插件和前端各自独立安装依赖。本机 pnpm 12.5.1。 |

---

## 3. 方案

### 3.1 SDK 包与接线

- 新包 `src/content-handler/`，名为 `@minicc/content-handler`。直接以 TypeScript 源码对外（`"type": "module"`，`exports` 指向 `src/index.ts`），不需要构建步骤，由 Vite 和 tsx 直接编译。
- 从插件迁入 SDK：`classify-page.ts`、`extract-article.ts`、`extract-page.ts`、`markdown.ts`、`page-meta.ts`、`readable.ts`、`turndown-plugin-gfm.d.ts`，以及 `types.ts` 里与提取有关的部分（`ARTICLE_LIMITS`、`ArticlePage`、`LinkPage`、`ExtractedPage`、`PageChoices`）。对应测试一起迁入，断言不变。
- 运行时依赖 `@mozilla/readability`、`dompurify`、`turndown`、`turndown-plugin-gfm` 随代码迁到 SDK，版本保持固定。插件不再直接依赖它们。
- 插件通过 `"@minicc/content-handler": "link:../content-handler"` 引用 SDK。`link:` 不会安装被链接包自身的依赖，所以 SDK 和插件都要各自执行 `pnpm install`，这一点写进文档。不建 workspace，避免影响前端的安装方式和 Docker 构建。
- SDK 自己的 `tsconfig.json` 不包含 `chrome` 类型，`pnpm typecheck` 一旦发现 SDK 用了 Chrome API 就会报错。SDK 提供 `test`、`typecheck`、`lint` 三个脚本，配置与插件一致。

### 3.2 handler 接口与提取流程

```ts
// handler 返回的覆盖值，优先于从页面推断出的值；SDK 统一按 ARTICLE_LIMITS 截断，图片地址按 extractImageUrl 的规则校验
export interface SiteOverrides {
  title?: string;
  author?: string;
  summary?: string;
  imageUrl?: string;
  suggested?: 'article' | 'link'; // 只对 preParse 有效，且仅在 Readability 有结果时生效
}

export interface FetchContext { fetch: typeof fetch }

export interface SiteHandler {
  name: string;
  matches(url: URL): boolean;                                              // 按域名精确匹配
  resolve?(url: URL, ctx: FetchContext): Promise<string | null>;           // 短链 → 真实地址（给手机端）
  preHandle?(url: URL, ctx: FetchContext): Promise<SiteOverrides | null>;  // 只凭链接取元数据
  preParse?(document: Document): SiteOverrides | void;                     // 同步改写克隆
}
```

这里参照 omnivore 保留三层钩子，但做了两处简化：用一个 `matches(url)` 代替每个钩子各自的 `shouldX`（第一批网站全部只按域名判断）；`preParse` 改为同步，保证 Markdown 复制仍是同步流程。handler 列表按顺序匹配，第一个命中的生效。

**基于 DOM 的流程**（`extractPage` 与 `buildMarkdown` 共用）：

1. 用 `document.URL` 找出匹配的 handler。
2. 克隆 document，在克隆上执行 `preParse`。如果抛异常：重新克隆一份干净的，并且不使用任何覆盖值。
3. markdown 模式先插入任务列表标记，再交给 Readability 和 DOMPurify，这两步保持不变。
4. 用原 document 生成 link 元数据（现有逻辑），再叠加覆盖值。handler 读的是当前页面内容，所以覆盖值不受 `isMetaTrusted` 限制。
5. article 的标题、作者、摘要、封面同样叠加覆盖值；`originalContent` 仍取未改动的原 document。
6. `suggested = overrides.suggested ?? isArticlePage(…)`。
7. Markdown 的 front matter 和 `# 标题` 使用覆盖后的标题和作者。

**基于链接的流程**（SDK 导出，供手机端使用；插件只用于 YouTube）：

- `resolveUrl(url, ctx): Promise<string>`：找到第一个有 `resolve` 且匹配的 handler；结果必须是 http/https 地址，否则返回原链接；出错时也返回原链接。
- `preHandleUrl(url, ctx): Promise<SiteOverrides | null>`：5 秒超时，出错返回 `null`。超时用 `AbortController` + `setTimeout` 实现，不依赖 `AbortSignal.timeout`。
- 再导出一个把覆盖值合并进 `PageChoices` 的辅助函数，插件和以后的手机端共用同一套截断和校验规则。

### 3.3 各网站规则

下面的选择器参考 omnivore，可能已经过时。**每个 handler 都要用真实页面抓取的固定样本来定稿**（见第 4 节）。

| 网站（匹配域名） | 钩子 | 规则 |
|---|---|---|
| 小红书（`xiaohongshu.com`） | preParse | 见下方单独说明。 |
| 微信公众号（`mp.weixin.qq.com`） | preParse | 懒加载图片：把 `data-src` 补到 `src`。删除标题区、`.rich_media_meta_list`、沉浸式封面、公众号卡片、`#content_bottom_area`、`.rich_media_area_extra`、`#js_pc_qr_code`。覆盖 author：取 `#js_author_name`，没有则取 `#js_name`。`suggested: 'article'`。 |
| Wikipedia（`wikipedia.org`） | preParse | 删除 `.mw-editsection`（[编辑] 链接）和 `sup.reference`（脚注上标）。**保留信息框**。覆盖 title：取 `#firstHeading`，去掉 “ - Wikipedia” / “ - 维基百科…” 后缀，Readability 处理不掉这类短标题的后缀。 |
| YouTube（`youtube.com`、`youtu.be`） | preHandle | 请求 oEmbed（`https://www.youtube.com/oembed?format=json&url=…`，视频、Shorts、播放列表先换成规范地址），覆盖 title、author（频道）、imageUrl（缩略图）。仍保存为链接，现有 `hqdefault` 封面兜底保留。Markdown 复制不走 oEmbed。 |
| 知乎（`zhihu.com`） | preParse | 问题页和回答页：只保留问题标题与描述，加上每个回答（每个回答带作者名），替换整个 body；覆盖 title：取问题标题。知乎 `document.title` 常带 “(N 封私信 / N 条消息)” 前缀和 “ - 知乎” 后缀。专栏（`zhuanlan.zhihu.com`）交给 Readability，覆盖 title：取文章标题。两种页面都要处理懒加载：`data-original` / `data-actualsrc` 补到 `src`，并去掉 `<noscript>` 里的重复图片。 |
| Medium（`medium.com`） | preParse | 把 `<picture><source srcset>` 换成取最大尺寸的 `<img>`。不重新抓取页面。 |
| GitHub（`github.com`） | preParse | 页面有 `<article>`（例如仓库首页 README）时，用它替换 body；覆盖 author：取 `[itemprop=author]`；覆盖 title：取 `twitter:title` / `og:title` 并去掉 “GitHub - owner/”。issue、PR 等没有 `<article>` 的页面走默认流程。 |
| Apple News（`apple.news`） | resolve | 带桌面浏览器 UA 抓取页面，取 `span.click-here` 父级链接的 `href` 作为真实地址。只在 SDK 里用：插件场景下用户通常已经跳到原站。 |
| Stack Overflow（`stackoverflow.com`） | preParse | 重组为问题加各个回答。每段一个标题，写明 Question / Accepted Answer / Answer 和票数；附作者、声望、评论（沿用 omnivore 的英文标签）。覆盖 title：取 `#question-header h1`。 |

**小红书 handler**

- 找到当前笔记的容器。两种情况都要支持：从发现页点开、以弹窗显示（站内跳转），以及直接打开笔记链接。只读当前 DOM，不使用 head 元数据。
- **图片**：取轮播里全部图片，保持顺序，去重，排除 swiper 为循环播放复制出来的幻灯片；用户没翻到的那几张也要有。如果直接打开笔记链接时 DOM 里拿不全，可以退回页面内嵌的初始状态 JSON，但必须先核对其中的笔记 ID 与当前 URL 一致（站内跳转后这段 JSON 仍是旧页面的）。弹窗场景拿不全就接受缺失。每张图单独包一层，例如 `<figure>`：Readability 的条件清理会删掉“图片多、段落少”的容器，这很可能就是截图里图片全丢的原因。
- **图片地址是否过期**：实现前先验证，方法是隔一段时间（例如一天）再请求同一个地址，或者核对地址里的时间戳。如果会过期，handler 就把地址改写成不带签名的永久形式，并测试改写后的地址能经图片代理正常显示。如果不会过期，保持原地址。
- **正文**：按换行拆成段落，空行保留为段落间隔。话题标签按原样保留，包括链接。
- **地点**：作者添加了地点时，正文末尾单独一行 `📍 地点名`。日期和 IP 属地不放进正文。
- **覆盖值**：
  - `title` 取笔记标题；笔记没有标题时，用 `document.title` 去掉 “ - 小红书”；
  - `author` 取作者昵称；
  - `imageUrl` 取第一张图；
  - `suggested: 'article'`，短笔记也按文章保存。
- **视频笔记**：保存封面图和文字，不嵌入视频。
- 用重建好的 `<article>` 替换 body，再交给 Readability。

### 3.4 插件改动

- `content.ts`、`popup.ts` 改为从 `@minicc/content-handler` 引入。消息协议不变，内容脚本仍同步返回。
- popup 收到 `extractContent` 的结果后：如果当前链接有匹配的 `preHandle`（目前只有 YouTube），用扩展页面的 `fetch` 调用 `preHandleUrl`，等待最多 5 秒，合并覆盖值后再显示预览。等待期间沿用现有的 “Extracting content...” 状态。失败时保持现有结果。顺带修复已知问题：YouTube 站内跳转后标题变成 “YouTube”。
- “Copy Markdown” 流程不变（同步，不发网络请求）。
- 插件 `types.ts` 只保留 `DEFAULT_API_URL`、`SavePageMessage`、`SavePageResponse`。

### 3.5 第 B 部分：重新保存时替换正文

只改 `POST /api/articles/content`（插件的“保存文章”）。“保存链接”和订阅源导入不变。

- **已有文章为 `Full` 时**：
  - 如果新正文（经图片代理改写后）与已存正文不同，走现有的替换路径：`ReplaceContent`、按严格更新的微秒版本号 `ResetProgress`、全部高亮置为 `Pending`。阅读页会按现有逻辑重新定位高亮，定位不到的成为 Orphaned，笔记仍可见。
  - 标题用新值替换。作者、摘要、封面在新值非空时替换。
  - 如果正文相同，不重置进度，也不改高亮。只有标题变了就只更新标题。
- **`SummaryOnly` / `Link` 的升级行为不变**，仍然只补空缺的元数据。
- **始终保留**：标签、笔记、收藏、归档、已读状态。保留现有行为：恢复回收站里的条目，只在 `SavedFromExtensionAt` 为空时设置它。
- **已打开的阅读页**：现有的“正文被替换”保护只认 `summaryOnly → full`。需要一个每次替换正文都会变化的正文版本标识（例如新增一列，这需要新的迁移），在详情、进度保存、重置和 409 响应里返回。阅读页把现有保护从“检测 `contentKind` 变化”推广为“检测正文版本变化”。效果：从插件重存后切回阅读页时，不会再按旧正文写入位置或定位高亮，并提示用户重新加载。会话存储里暂存的失败进度，也要按正文版本丢弃旧坐标（现在只按 `contentKind`）。

---

## 4. 测试与验收

**SDK 单元测试**（`src/content-handler`，`tsx --test` + jsdom，不注入任何 `chrome` 全局对象）：

- **固定样本**：用浏览器自动化打开真实页面（优先在未登录状态下），抓取渲染完成后的 HTML，删减到相关结构，放在 `test/fixtures/<site>/`。提交前去掉 token、cookie 值、用户昵称等个人信息。某个网站必须登录才能看到内容时，到那一步再请用户协助。截图里那篇小红书笔记，如果用户提供了链接就用它，否则找一篇结构类似的。最少需要：
  - 小红书：多图且正文多行的笔记（截图里那篇或类似的）、带作者地点的笔记、弹窗打开（站内跳转）的场景、视频笔记、只有一两行字的短笔记；
  - 微信：带懒加载图片的文章；
  - Wikipedia：中文、英文各一篇；
  - 知乎：问题页、专栏各一篇；
  - Medium：含 `<picture>` 的文章；
  - GitHub：仓库首页、一个 issue（验证走默认流程）；
  - Stack Overflow：带已采纳回答和评论的问题。
- **每个 handler 的具体断言**。以小红书为例：图片数量和顺序与轮播一致；每行文字是独立段落；标题没有后缀；作者正确；有地点时出现 `📍` 行；不出现日期和 IP 属地；`suggested` 为 article；Markdown 输出同样包含全部图片和换行。
- **通用断言**：
  - handler 抛异常时，结果与没有 handler 时完全相同；
  - 提取完成后，原 document 的 `outerHTML` 没有变化；
  - `evilzhihu.com` 这类相似域名不会被匹配；
  - 覆盖值同样按 `ARTICLE_LIMITS` 截断，并通过图片地址校验。
- **基于链接的 handler**：注入模拟的 `fetch`，测 YouTube oEmbed 的成功、失败、超时，以及 Apple News 的解析、找不到链接、地址不是 http(s) 三种情况。
- 迁入的现有测试原样通过；`pnpm typecheck`、`pnpm lint` 退出码为 0。

**插件**：

- `background`、`popup`、`clipboard` 测试通过。popup 新增测试：YouTube 覆盖值成功合并；`preHandle` 失败或超时时保持原结果。
- `pnpm typecheck`、`pnpm lint`、`pnpm build` 退出码为 0，`dist/` 里包含打包进去的 SDK 代码。

**第 B 部分（后端，用真实 PostgreSQL 的 Testcontainers 测试）**：

- `Full` 文章重存且正文不同：正文和标题被替换；标签、笔记、收藏、归档、已读状态保留；进度版本严格变新；高亮变为 `Pending`；正文版本标识变化。
- 正文相同：进度、高亮、正文版本都不变；只有标题不同时只更新标题。
- 回收站里的 `Full` 文章重存：被恢复并替换正文。
- `SummaryOnly` 和 `Link` 的升级行为、“保存链接”、订阅源导入，现有测试全部通过，行为不变。
- 正文版本标识出现在详情、进度保存、重置、409 响应里；真实 HTTP 测试覆盖这几个接口。

**第 B 部分（前端）**：

- Vitest：阅读页打开期间正文版本变化后，暂停写入位置和定位高亮，并提示重新加载；会话存储里旧版本的失败进度被丢弃。
- Playwright 新增场景：打开一篇文章 → 通过 API 以不同正文重存 → 回到阅读页，看到重新加载提示，且没有写入旧坐标。原 65 篇文章的基准数据不受影响。
- 后端和前端全部测试、TypeScript、限定范围的 lint（现有 ArticleCard 图片告警除外）、EF 模型一致性检查都通过。

**真实浏览器验收**（需要用户在 Chrome 里重新加载 `dist/`）：

- 对 9 个网站各保存一个样例到本地环境，在阅读页确认效果。
- 截图里那篇小红书笔记**直接重新保存**（第 B 部分生效后，不用先删除）：全部图片能经图片代理正常显示（同时确认小红书和微信 CDN 的防盗链不会拦截代理请求）、换行保留、标题没有后缀，原有标签和状态保留。
- 对小红书笔记执行一次 “Copy Markdown”，检查剪贴板内容。

**文档**：更新 `CLAUDE.md`（Web Extension 部分、开发命令、新增 SDK 部分；把“插件保存保留已有完整正文”的规则改为第 3.5 节的新规则）和 `src/web-extension/README.md`。

---

## 5. 我自己做的决定（请重点核对）

- SDK 放在 `src/content-handler/`，用 `link:` 引用，不建 workspace。
- Wikipedia 保留信息框（omnivore 会删掉）。
- YouTube 继续存成链接，oEmbed 只用来修正标题、作者、封面；Markdown 复制不走 oEmbed。
- `preParse` 改为同步、`matches(url)` 只看域名，都是对 omnivore 接口的简化。
- 小红书地点行的格式是 `📍 地点名`；视频笔记只保存封面和文字；短笔记也按文章保存。
- 只有小红书和微信强制 `suggested: 'article'`。其余网站的正文通常够长，沿用 `isArticlePage` 的判断。
- Stack Overflow 沿用 omnivore 的英文分段标签。
- 重存时标题总是替换。用户在 MiniCC 里手动改过的标题会被覆盖，因为重存就是“重新抓取”。
- 重存时正文相同就不重置进度，避免误点重存后丢失阅读位置。
- 为了不让已打开的阅读页按旧正文写数据，新增正文版本标识，这需要一次数据库迁移。
- 测试样本优先在未登录状态下通过浏览器自动化抓取，提交前去掉个人信息。

## 6. 留给实现者

- 各网站的具体选择器，以真实页面抓取的样本为准。
- `parseReadable` 怎么改才能接收一个已经处理过的克隆。
- 合并覆盖值的辅助函数叫什么名字、放在哪个文件。
- SDK 的 ESLint 配置细节（与插件保持一致即可）。
- 正文版本标识的具体形式（递增整数或时间戳）、列名和迁移名（按现有顺序应为 `M4_…`）。
- 小红书永久图片地址的具体改写规则，以验证结果为准。
