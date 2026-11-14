# 保存非文章网页（链接型条目）与复制 Markdown 设计

- 日期：2026-11-13
- 状态：待评审
- 相关：`docs/superpowers/specs/2026-11-07-library-reading-rss-design.md`（`ContentKind`、保存去重、SummaryOnly→Full 升级均来自该设计）

---

## 1. 背景与目标

MiniCC 的插件目前只有一条路径：用 `@mozilla/readability` 解析正文，解析失败就报错 `This page does not contain a readable article.`，页面存不下来。YouTube 视频页这类“本身不是文章”的网页因此无法保存。另外，用户经常把好文章整理进本地 Markdown 汇总文档，现在只能先存进 MiniCC 再手动复制。

本设计加入两项能力：

1. **保存链接型条目**：对没有可读正文的网页，只保存网址（加标题、描述、封面图等元数据）。它与文章共用同一个文库：出现在收件箱 / 全部文章，可打标签、收藏、归档、进回收站；详情页以“访问原文”为主操作。
2. **复制 Markdown**：插件弹窗新增按钮，把 Readability 解析出的正文转成带 YAML front matter 的 Markdown，**写入剪贴板**（不下载文件），用户粘贴进自己的本地汇总文档。

**成功标准：** 第 9 节验收场景全部通过。

### 1.1 用户场景（来自用户）

- 链接：在 YouTube 视频页点插件即可保存；之后从“收件箱”或“全部文章”进入详情，点“访问原文”回到原始地址。
- Markdown：复制后粘贴到本地的 Markdown 汇总文档；文档开头用 front matter 带上标题、原文链接、作者、保存日期，以及网页自身带的标签（`article:tag` / `keywords` 等）。
- 保存方式：弹窗同时提供“保存文章”和“保存链接”，**由用户选择**；插件自动判断的结果作为默认建议高亮显示（2026-11-14 用户改定，原为“插件自动判断，用户不手动选择”）。

### 1.2 非目标（首版不做）

- 复制 Markdown 的同时保存到 MiniCC，或下载 `.md` 文件。
- 图片下载、内嵌或转 base64（Markdown 中图片保留远程地址）。
- 视频页的专属解析（播放时长、频道、字幕、嵌入播放器）。唯一例外是 YouTube 封面，见 3.1。
- 对视频站点地址做专门规范化（用户决定，见 4.2）。
- 把已存的链接条目手动“转成文章”（只能通过插件在同一 URL 上重新保存升级，见 4.3）。
- 文库按“链接 / 文章”类型筛选（见第 8 节“留给实现者”）。
- 在 MiniCC 内导出 Markdown（网站端不做，只在插件）。

### 1.3 反目标（即使能用，出现这些也算失败）

- 自动判断把真正的文章存成链接（丢正文），或把视频页存成一段评论/简介“文章”。**宁可存成链接，也不要误把噪声当正文**，但已存的完整文章不得被降级。
- 站内跳转后，把上一个页面的标题、封面、简介或标签记到当前地址上（见 3.1）。
- 同一 URL 再次保存（任意类型）造成重复条目，或丢失已有的标签、高亮、笔记、状态、阅读进度。
- 链接条目出现阅读进度、高亮、目录等对它没有意义的界面，或这些接口对它产生脏数据。
- 复制的 Markdown 粘贴后 front matter 失效（标题含冒号、引号、换行等导致 YAML 解析错误）。
- 复制 Markdown 时弹窗卡死，或因点击后异步等待导致剪贴板写入被浏览器拒绝。

---

## 2. 现状与约束

### 2.1 已有代码（实现者应复用）

| 位置 | 现状 |
|---|---|
| `src/web-extension/src/extract-article.ts` | `extractArticle(document)`：Readability 解析 + DOMPurify 净化，失败则抛错；已有 `og:image` 提取（限 http/https、≤2000 字符）。 |
| `content.ts` / `popup.ts` / `background.ts` | popup 通过 `chrome.tabs.sendMessage({action:'extractContent'})` 向内容脚本取数据，通过 `chrome.runtime.sendMessage({action:'saveArticle'})` 交给 background，由 background 带 `X-Api-Key` POST 到 `/api/articles/content`。 |
| `ContentKind { Full = 0, SummaryOnly = 1 }` | 已有于 `ArticleNs/Domain/AggregatesModel/ReadingStatus.cs`；列表/详情 DTO 已暴露，前端以 `'summaryOnly'` 判断摘要横幅（`Client.tsx`、`ArticleCard.tsx`）。 |
| `SaveArticleContentCommandHandler` | 事务内 `LockArticleCreationAsync` → 按 `UrlNormalizer.Normalize` 查已有（含回收站）→ 恢复并保留一切状态；`SummaryOnly` 时替换为完整正文、重置进度、高亮置 Pending。 |
| `Content.Create` | 要求 `originalContent` 非空；`ArticleDomainService.CreateArticleFromContent` 要求 `readableContent` 非空。链接型条目没有正文，需要新的创建路径。 |
| 前端详情页 `Client.tsx` | 已有“访问原文”链接，位于标题下方的 `article-actions` 区，不在 `ReaderToolbar` 里（工具栏只有返回、已读、重置进度、收藏、归档、删除）。样式 `.visit-original-btn` 是低调的描边按钮。另有 SummaryOnly 横幅。 |
| 文章卡片 `ArticleCard.tsx` | 来源行总是显示站点域名（去掉 `www.`）；`summaryOnly` 时用 `styles.archiveLabel` 显示“仅摘要”徽标。 |
| `Articles` 表列（`MiniCcContextModelSnapshot.cs`） | `OriginalContent` / `ReadableContent` 是 `text NOT NULL`，`Author` / `Summary` 也是 `NOT NULL`。PostgreSQL 中空串是合法的非空值，所以链接条目存空串不需要迁移。 |
| `SearchVector`（`ArticleConfiguration.cs`） | 生成列，来源 `Title, Summary, ReadableContent, Author`。链接条目正文为空，仍可按标题、摘要、作者搜到。 |
| Readability 默认选项 | 0.6.0 默认 `keepClasses: false`，会删掉正文里的 `class`（只保留 `page`）。现有保存结果里因此没有 `language-xxx` 这类 class，见 5.2。 |
| popup 界面文案 | 现有文案全部是英文（`Save Article`、`Settings`、`Extracting content...`）。 |
| `ArticlesController.SaveProgress` | 目前把处理器的所有 Failure 映射为 404，见 4.3“其他后端联动”。（`HighlightsController.Anchor` 也是如此，但本期不涉及。） |

### 2.2 约束

- 单用户、沿用现有分层（MediatR / FluentValidation / Mapster / `IUnitOfWork`）和 API 认证（Cookie + `X-Api-Key`）。
- 插件是 Manifest V3（Chrome），构建使用 Vite；扩展测试使用 `tsx --test` + jsdom。
- JSON 枚举为 camelCase，故新值序列化为 `"link"`。
- 新增 Markdown 转换依赖需固定版本（现有两个运行时依赖 `@mozilla/readability`、`dompurify` 是固定版本；devDependencies 用 `^` 范围）。

---

## 3. 总体方案

```
          ┌────────────── content.ts（页面内）──────────────┐
popup ──► │ action: extractContent                         │
          │   按正文和页面元数据选择文章或链接 │
          │   article → extractArticle()   link → extractLink() │
          │ action: extractMarkdown                        │
          │   Readability + DOMPurify → Turndown → front matter │
          └────────────────────────────────────────────────┘
popup ─► 展示“将保存为：文章/链接”（只读标签）
  保存按钮 ─► background ─► POST /api/articles/content  (kind=article)
                         └► POST /api/articles/link     (kind=link)
  复制 Markdown 按钮 ─► navigator.clipboard.writeText(markdown)
```

关键取舍（均为本设计的选择，非用户指定）：

- **链接是 `ContentKind` 的第三个值 `Link = 2`，不是新实体**。这样去重、回收站恢复、状态、标签、批量操作、列表查询全部沿用，不需要新表。
- **新增独立端点 `POST /api/articles/link`**，不去放宽 `/content` 的 `Required` 校验。理由：`/content` 的契约（“必须带正文”）仍然是对外保证；链接保存的请求体没有正文字段，语义清晰，校验也各自简单。
- **自动判断放在插件端**。判断依赖 DOM（Readability 结果、meta、JSON-LD），服务端只有 HTML 字符串；并且链接路径不需要上传整页 HTML。

### 3.1 站内跳转后的页面元数据

单页应用（YouTube 是典型）在站内跳转时只换地址和正文，`<head>` 里的 `og:*`、`keywords`、JSON-LD 往往还是上一个页面的。内容脚本只在整页加载时注入一次，可以据此判断：

- 内容脚本注入时记下 `location.href`（下称 `loadedUrl`）。
- 提取时，若 `document.URL` 与 `loadedUrl` 去掉 hash 后不同，说明发生过站内跳转，**元数据不可信**。
- `document.URL` 和 `document.title` 始终可信。

元数据不可信时，凡是读取 `<head>` meta 或 JSON-LD 的地方一律不读：

| 位置 | 不可信时的行为 |
|---|---|
| 4.1 页面类型判断 | 跳过 `og:type` 和 JSON-LD 信号；URL 模式照常生效。 |
| 4.2 链接字段 | `title` 用 `document.title`（回退主机名）；`summary`、`author`、`imageUrl` 留空。 |
| 5.3 front matter | `title` 用 `document.title`；不写 `author` 和 `tags`。 |

这条规则是我的取舍：宁可少存，不存错（1.3）。代价是站点即使正确更新了 meta，站内跳转后也拿不到封面和简介，刷新页面再保存才有。判断结果应作为参数传给提取函数，便于用 jsdom 测试。

**YouTube 封面例外（用户决定）**：`imageUrl` 为空且当前地址是 YouTube 视频页时，用视频 ID 拼出 `https://i.ytimg.com/vi/<id>/hqdefault.jpg`。视频 ID 取自 `watch?v=<id>` 或 `shorts/<id>`。只补封面，不补简介、频道等（1.2）。

**范围**：文章保存路径（`extractArticle`）的标题、作者、摘要由 Readability 给出，Readability 自己会读 meta；封面读 `og:image`。这些是现状，本期不改。

**未实测的前提**：“YouTube 站内跳转不更新 `og:*`”是常见说法，本设计没有实测。规则本身不依赖这个前提。实现时可在控制台确认一次：打开一个视频，站内点到另一个，读 `document.querySelector('meta[property="og:title"]').content`。

---

## 4. 保存链接型条目

### 4.1 自动判断（插件，`isArticlePage`）

输入当前 `document`，输出 `'article'` 或 `'link'`，以及（文章时）已解析好的结果以避免重复解析。规则按序执行，命中即返回：

1. **Readability 返回空**（`parse()` 为 null 或无 `content`）→ `link`。
2. **强“非文章”信号 → `link`**：`<meta property="og:type">` 以 `video`、`music` 开头；或 URL 命中内置的已知视频/媒体页模式（首版：`youtube.com/watch`、`youtube.com/shorts/`、`youtu.be/`、`vimeo.com/<数字>`、`bilibili.com/video/`）。这一条优先于下一条，因为 YouTube 页面上 Readability 往往能“成功”解析出简介或评论。
3. **强“文章”信号 → `article`**：`og:type` 为 `article`，或 JSON-LD 中存在 `@type` 为 `Article` / `NewsArticle` / `BlogPosting` / `TechArticle`，且 Readability 纯文本长度 ≥ 200。
4. **文本太短 → `link`**：Readability 纯文本长度 < 500 字符（与 RSS 摘要阈值 D3 一致，见 2026-11-07 设计 2.1）。
5. 其余 → `article`。

元数据不可信时（3.1），规则 2 的 `og:type` 判断和规则 3 整条跳过，规则 2 的 URL 模式照常生效。

纯文本长度按 `textContent` 去空白后的字符数计。阈值（200 / 500）与站点列表是**我的初始取值**，应集中为命名常量，便于调整；见第 8 节。

判断结果只决定默认建议：弹窗同时显示“保存文章”和“保存链接”，建议的那个用主按钮样式，另一个用次按钮样式，用户可以选任意一个（1.1）。Readability 返回空时“保存文章”不可用。

### 4.2 链接提取（`extractLink`）

不运行 Readability，不上传整页 HTML。字段与现有长度限制对齐：

| 字段 | 来源（按序取第一个非空） | 限制 |
|---|---|---|
| `url` | `document.URL` | ≤ 2000（超出则报错，同现有 API 限制） |
| `title` | `og:title` → `twitter:title` → `document.title` → URL 主机名 | ≤ 500，trim |
| `summary` | `og:description` → `twitter:description` → `meta[name=description]` | ≤ 1000 |
| `imageUrl` | 文章和链接共用 `extractImageUrl`，取文档顺序中第一个 `og:image` 或 `twitter:image`。没有可用封面时，YouTube 视频链接用视频 ID 拼封面（3.1） | http/https，≤ 2000 |
| `author` | `meta[name=author]` → `meta[property=article:author]`（YouTube 上通常没有，可空） | ≤ 200 |

表中 `title`、`summary`、`author`、`imageUrl` 的 meta 来源只在元数据可信时使用；不可信时的取值见 3.1。

**地址原样保存（用户决定）**：`url` 就是 `document.URL`，不为视频站点做专门规范化。去重只按现有 `UrlNormalizer`（去掉 hash、`utm_*`、`fbclid`、`gclid`、`spm` 等跟踪参数）。因此同一个 YouTube 视频带不同参数（`&list=`、`&t=`、`&pp=`）会存成不同条目，“访问原文”回到的是保存时的播放列表位置或时间点。这是已知并接受的行为。

### 4.3 后端

**领域**

- `ContentKind` 新增 `Link = 2`。
- `Article` 新增工厂 `Article.CreateLink(Url url, string title, string author, string summary, string imageUrl)`：`ReadableContent = ""`、`OriginalContent = ""`、`Length = 0`、`ContentKind = Link`。`Content.Create` 的“原文非空”校验保持不变，链接路径绕过它（不放宽对文章的校验）。两列都是 `text NOT NULL`，空串合法，**不需要迁移**（见 2.1）。
- `ContentKind` 已有默认值 0，新增枚举值本身不需要改库。

**接口**

```
POST /api/articles/link
{ url, title, author?, summary?, imageUrl? }
→ 201 ArticleDto（contentKind: "link"）；校验失败 400
```

校验：`Url` 必填、http/https、≤2000；`Title` 必填（插件已保证回退到主机名）、≤500；`Author` ≤200；`Summary` ≤1000；`ImageUrl` ≤2000。`imageUrl` 与文章一致走 `ImageProxyService.GetUrl`。

**保存/去重语义**（`SaveLinkCommandHandler`，与 `SaveArticleContentCommandHandler` 同样的事务 + `LockArticleCreationAsync` + 规范化 URL 查找，含回收站）：

| 已有条目 | 保存链接 | 保存文章（`/content`，现有端点） |
|---|---|---|
| 无 | 新建 Link | 新建 Full（现状） |
| 在回收站 | 恢复，保留一切，不改内容 | 恢复（现状） |
| `Full` | 仅恢复 + `MarkSavedFromExtension`；**不降级、不覆盖** | 现状 |
| `SummaryOnly` | 同上，保持摘要不变 | 现状：升级为 Full |
| `Link` | 恢复；不改已有元数据（与“已有内容不被更新”的原则一致） | **升级为 Full**：替换正文、重置进度、高亮置 Pending（可为空）、保留标签/笔记/状态 |

实现上，把 `/content` 处理器里的升级条件从 `ContentKind == SummaryOnly` 改为 `ContentKind != Full`，升级时还应用新提取的 `title/author/summary/imageUrl` 中现有条目为空的字段（我的取舍：链接条目 `author` 常为空，升级后补上更有价值；已有非空值不覆盖）。两种升级走同一段代码，所以 `SummaryOnly → Full` 也会补空字段；这是对现有行为的一处小改动，同样只补空、不覆盖。

**其他后端联动**

- `ArticleDto` / `ArticleListItemDto` 不需要新字段，`contentKind` 已暴露。
- `UpdateProgressCommand`、`ResetProgress`、`AddHighlight` 对 `Link` 条目返回 **400**（无正文可定位）。`PUT highlights/{id}/anchor` 不需要改：链接条目建不了高亮（`AddHighlight` 被拒绝），也没有任何路径把已有文章降级成链接，所以链接条目不可能有高亮。批量操作、状态 PATCH、标签、回收站、`ArticlePurger` 无需区分类型。注意 `ArticlesController.SaveProgress` 现在把所有 Failure 返回 404，需要让“条目是链接”与“条目不存在”可区分，后者仍是 404。
- 列表搜索：`SearchVector` 已包含 `Title, Summary, ReadableContent, Author`（见 2.1），链接条目按标题、摘要、作者命中，查询不需要改。
- 订阅（RSS）路径不产生 `Link`，不受影响。

### 4.4 插件

- `types.ts`：`ExtractedContent` 改为带判别字段的联合类型：
  ```ts
  type ExtractedPage =
    | { kind: 'article'; url; title; originalContent; readableContent; summary?; author?; imageUrl? }
    | { kind: 'link';    url; title; summary?; author?; imageUrl? };
  ```
  `SavePageMessage` 携带 `ExtractedPage`；`background.ts` 按 `kind` 选择 `/api/articles/content` 或 `/api/articles/link`，鉴权与错误处理不变。
- `content.ts`：`extractContent` 返回 `PageChoices`，即 `{ suggested, article, link }`：`link` 总是存在；`article` 在 Readability 返回空时为 `null`；`suggested` 是 4.1 的判断结果，为 `'article'` 时 `article` 必不为空。不再因“无可读正文”报错（那正是链接路径）。
- `popup`：预览区显示建议类型对应的标题、作者和地址；“保存文章”“保存链接”两个按钮同时显示，建议的那个是主按钮；保存时把用户所选的那一种 `ExtractedPage` 发给 `background.ts`；成功提示按所选类型显示。
- **文案语言（我的取舍）**：popup 现有文案是英文，新增文案沿用英文，不在本期做汉化。本文档里的中文文案只说明含义，对应关系：

  | 本文档 | popup 实际文案 |
  |---|---|
  | 保存文章 / 保存链接 | `Save Article`（现有）/ `Save Link` |
  | 复制 Markdown | `Copy Markdown` |
  | 已复制 Markdown | `Markdown copied` |

### 4.5 前端

- `types/index.ts`：`contentKind` 联合类型加入 `'link'`。
- **文章卡片**（`ArticleCard.tsx`）：显示“链接”徽标（沿用“仅摘要”徽标的写法）；点击仍进入 MiniCC 内详情（用户明确要求先进详情再点“访问原文”）。摘要区显示 `summary`。卡片本来就总是显示站点域名，无封面时不需要额外占位。
- **详情页**（`Client.tsx`）：`contentKind === 'link'` 时渲染“链接视图”，不渲染阅读正文、目录、进度条、高亮列表、阅读器设置：
  - 标题、域名、保存时间、标签管理、收藏/归档/已读/删除（沿用 `ReaderToolbar`，隐藏“重置进度”）。
  - 封面图（若有）和 `summary`。
  - 一个醒目的主按钮“访问原文”（`target=_blank rel="noopener noreferrer"`），放在现有 `article-actions` 位置，沿用现有文案和图标。现有 `.visit-original-btn` 是描边的次要样式，链接视图里要改用主按钮样式（实心主色）；文章视图里的样式不变。
  - 链接详情向 `useReadingProgress` 传入 `undefined`。打开和关闭链接详情时，hook 保持空闲，阅读状态由手动标记修改。
  - 链接详情页开着的时候，该条目被插件升级成 `Full`：页面保持链接视图，刷新后显示正文。链接视图没有进度和高亮坐标，不需要 `SummaryOnly` 那套“旧正文暂停”的处理。
- 回收站、批量操作、撤销等沿用现有逻辑，不需要改动。

---

## 5. 复制 Markdown

### 5.1 交互

- 弹窗新增“复制 Markdown”按钮，位于“保存”旁。独立于保存：不触发保存，保存也不触发复制。
- **必须在点击的用户手势内完成剪贴板写入**。因此弹窗打开提取预览时，不预生成 Markdown；点击后：向内容脚本发 `extractMarkdown` → 收到字符串 → `navigator.clipboard.writeText`。（Markdown 转换很快，通常不会超出手势有效期；如遇拒绝，作为兜底使用隐藏 `<textarea>` + `document.execCommand('copy')`。）`manifest.json` 增加 `clipboardWrite` 权限。
- 成功提示“已复制 Markdown”；失败显示错误。无正文时的行为见 5.4。

### 5.2 转换管线（内容脚本内）

1. `Readability` 解析（克隆的 document）→ `DOMPurify.sanitize`。保存和复制都使用 `parseReadable`。保存沿用默认的 `keepClasses: false`。点击复制时单独解析，保留 class 和任务列表状态。
   - 为什么 Markdown 要单独解析：Readability 默认删掉正文里的 `class`，`language-xxx` 随之丢失，围栏代码块就拿不到语言标记（验收 10）。
   - 为什么保存路径不跟着开 `keepClasses`：带着原站 class 的 HTML 进入阅读器后，可能和 MiniCC 自己的样式类撞名（例如原站的 `hidden`、`flex`）。保存结果必须保持现状。Markdown 没有 class，转换后自然丢弃。
2. 用 **Turndown** 和 `turndown-plugin-gfm` 的删除线规则转 Markdown。表格和任务列表由自定义规则处理。依赖固定版本，由实现者选择当前稳定版并写入 `package.json`。配置：
   - 标题 ATX（`# `）；列表符号 `-`；强调 `*`/`**`；代码块用围栏 ` ``` `；
   - 围栏的语言标记优先取 `<pre>` 或 `<code>` 上的 `language-xxx` 或 `lang-xxx`，其次取 GitHub 高亮包装里的语言。
   - 链接内联；图片 `![alt](url)`，保留远程地址（Readability 已把相对地址转为绝对地址）；丢弃 `data:` 图片、`<script>`、`<style>`；
   - 任务列表在 Readability 清理表单控件前，从克隆文档保留列表项开头复选框的状态。
   - 无表头的表格使用空表头，以保留首行数据。跳过没有单元格的 HTML 表格行。
3. 统一 `\n` 换行，Markdown 文本块间最多使用两个换行，围栏内保留空行及缩进，文末保留单个换行。

### 5.3 输出格式

```markdown
---
title: "文章标题"
url: "https://example.com/post"
author: "作者"
date: 2026-11-13
tags: ["tag1", "tag2"]
---

# 文章标题

正文……
```

字段与用户要求的对应（键名是我的取舍，用户只指定了要带哪些信息）：

| 用户要求 | 键 | 是否必有 |
|---|---|---|
| 标题 | `title` | 总是 |
| 原文链接 | `url` | 总是 |
| 作者 | `author` | 页面有才写 |
| 保存日期 | `date` | 总是 |
| tag / tags | `tags` | 页面有才写 |

规则：

- `title`、`url`、`author`、`tags` 元素均使用 **YAML 双引号字符串**，转义 `\`、`"`，CRLF、CR 和 LF 转为 `\n`，制表符转为 `\t`，清除其余 C0、DEL 和 C1 控制字符。这样标题含冒号、引号、`#` 等也不会破坏 YAML。
- `date` 为**保存日期**，即点击复制当天，本地时区，`YYYY-MM-DD`（无引号，YAML 日期）。
- `author` 为空则整行省略；`tags` 为空则整行省略。`title`、`url`、`date` 总是存在。
- `title` 取 Readability 标题，回退 `document.title`；`url` 取 `document.URL`（保留 hash，不做 MiniCC 规范化）。
- 元数据不可信时（3.1）：`title` 用 `document.title`，不写 `author` 和 `tags`。
- front matter 之后写一个 `# 标题` 一级标题（Readability 的正文通常不含标题）。这是我的取舍，便于汇总文档里直接看到标题。

**tags 来源（网页自身，不是 MiniCC 标签；用户已确认）**，按序合并去重：

1. 所有 `<meta property="article:tag">` 的 `content`；
2. `<meta name="keywords">`，按 `,` `，` `;` `；` 切分；
3. JSON-LD 中 Article 类对象的 `keywords`（字符串按上述分隔符切分，或字符串数组）。

处理：trim、丢空、大小写不敏感去重（保留首次出现的写法）、单个标签 ≤ 50 字符（超出丢弃）、最多 20 个。

### 5.4 无正文页面（链接类）

“复制 Markdown”对判定为 `link` 的页面仍可用：输出 front matter（`title`、`url`、可选 `author`、`date`、`tags`），正文为一行 `[标题](url)`。这是我的取舍：用户的汇总文档里同样会想收录视频页链接。若实现者发现这个行为让界面含混，可改为禁用并提示，但需在计划里说明。

---

## 6. 安全与隐私

- 链接保存只上传元数据，不上传整页 HTML，降低对登录态页面的无意泄露。
- Markdown 由 DOMPurify 净化后的 HTML 转换，不引入未净化内容；front matter 全部转义（5.3）。
- 新端点沿用 API 认证；`/api/articles/link` 的 `url` 只接受 http/https。
- 前端“访问原文”必须 `rel="noopener noreferrer"`。

---

## 7. 兼容与迁移

- 旧版插件继续只调用 `/content`，行为不变；新版插件才会调用 `/link`。
- 旧版前端遇到 `contentKind: "link"` 会按完整文章渲染空正文。前端与后端同版本发布（单用户自托管，docker 一起构建）。
- 数据库：无迁移。`ContentKind` 是整数列，新增枚举值不改表；正文列允许空串（见 2.1）。

---

## 8. 留给实现者

- `Article.CreateLink` 的具体写法（`Content` 的构造函数是私有的，`Content.Create` 拒绝空原文，需要另一条构造路径）。
- `isArticlePage` 的阈值（200 / 500）和已知站点列表是初始取值，可用真实页面微调，但要保持规则顺序和“宁可存成链接”的倾向，并用 fixtures 测试固定下来。
- 文库“按类型筛选链接 / 文章”不在本期；若实现 UI 成本很低（`GET /api/articles` 增加 `kind` 参数），可作为单独的小任务，不要挤入本期。
- progress / 高亮接口对 `Link` 返回 400 的具体实现方式（处理器结果如何与 404 区分）。
- YouTube 封面用哪个尺寸：默认 `hqdefault.jpg`（480×360，上下有黑边）；若卡片里黑边难看，可换 `mqdefault.jpg`（320×180，16:9）。
- Turndown 的具体版本与细节规则（表格内换行、嵌套列表缩进等）。

---

## 9. 验收场景

**保存链接**

1. 直接打开（整页加载）一个 YouTube 视频页（`watch?v=`），打开插件：“保存链接”是主按钮，点击后保存成功；收件箱出现“链接”卡片，带标题、封面、描述。
2. 打开该卡片详情：无阅读正文/目录/进度条/高亮；有“访问原文”按钮，新标签页打开原始地址。给它打标签、收藏、归档、删除、撤销、永久删除都正常。
3. 再次对同一 URL（含不同 hash/跟踪参数，按现有规范化）保存链接：不产生重复；若在回收站则被恢复且标签/状态保留。
4. 链接条目页面访问 progress / 高亮接口：400；前端不发这些请求。
5. 对已存的 `Link` 条目所在 URL，用一个会判定为文章的页面（或测试夹具）保存：升级为 `Full`，标签/收藏/归档/笔记保留；之后详情页显示正文。
6. 对已存的 `Full` 文章再保存链接：内容不变，不降级。
7. 普通博客文章页：判定为“文章”（“保存文章”是主按钮），保存行为与现在一致。短页面（<500 字符且无文章信号）判定为链接。用户可以不按建议，选另一个按钮保存；页面没有可读正文时“保存文章”不可点。

**复制 Markdown**

8. 在博客文章页点“复制 Markdown”：剪贴板内容以 front matter 开头，`title`/`url`/`date` 正确；页面有 `article:tag` 或 `keywords` 时有 `tags`，没有则无该行；作者缺失时无 `author` 行。
9. 标题含 `"`、`:`、`#`、换行时，front matter 仍是合法 YAML（用 YAML 解析器测试）。
10. 代码块保留语言标记；表格、链接、图片（远程地址）、列表正确；无 `data:` 图片和脚本内容。
11. 复制不产生保存请求，保存不触发复制。
12. 链接类页面（如 YouTube）点“复制 Markdown”：得到 front matter + `[标题](url)`。

**站内跳转与地址**

13. 在 YouTube 站内从视频 A 点到视频 B（不刷新）后保存：条目的地址和标题是 B 的，封面是由 B 的视频 ID 拼出的缩略图，没有简介；不出现 A 的任何信息。
14. 站内跳转后点“复制 Markdown”：`title` 来自 `document.title`，没有 `author` 和 `tags` 行。
15. 同一视频的 `watch?v=ID` 与 `watch?v=ID&list=…` 分别保存：得到两个条目（按用户决定，不合并）；`watch?v=ID` 与 `watch?v=ID#t=1`、`watch?v=ID&utm_source=x` 仍是同一个条目。

**测试要求**

- 插件：`isArticlePage`（含 YouTube、文章、短页面夹具）、`extractPage` 的链接路径、Markdown 转换与 front matter 转义/tags 合并、background 按 `kind` 选择端点；以上各项都要有“元数据不可信”的用例，另测 YouTube 视频 ID 拼封面。
- 后端：`/link` 端点的新建、去重、回收站恢复、不降级、Link→Full 升级、400 校验、progress/highlight 对 Link 的 400（真实 PostgreSQL 夹具）。
- 前端：卡片徽标、详情链接视图（进度 hook 保持空闲，目录和高亮隐藏）、`'link'` 类型。
