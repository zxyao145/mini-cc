# Web 前端按 Arc UI 设计语言改版

- 日期：2026-11-14
- 状态：已实施，待使用本地数据验收
- 参考：Arc UI（https://uiarc.dev，免费组件，注册表 `https://uiarc.dev/r/<name>.json`）
- 样稿：[`arc-ui-mockups/visual-style.html`](arc-ui-mockups/visual-style.html)（选定 C 版）、[`arc-ui-mockups/reader.html`](arc-ui-mockups/reader.html)
- 实施计划：[`2026-11-14-arc-ui-redesign.md`](../superpowers/plans/2026-11-14-arc-ui-redesign.md)

---

## 1. 背景与目标

用户觉得现在的网站 UI 很丑，要求按 Arc UI 的设计语言重新设计整个 Web 前端（`src/frontend`）。用户截图指出了五个问题：

1. 文库筛选栏的按钮太大，样式不统一：阅读状态和标签是带 ▶ 三角的 `<details>`，其余是原生下拉框。
2. 卡片上的多选框难看，是一个白色大方块里套着原生复选框。
3. 阅读页目录被挤成一个字一行，并出现了横向滚动条。
4. 订阅弹窗出现滚动条后，左边圆角、右边直角，底部按钮被截断。
5. 按钮有的带图标有的不带，中英文混用（如高亮菜单里的 Highlight Options / Delete Highlight / Cancel）。

### 1.1 原因

- `globals.scss` 把 Simple.css 引入到 `layer(base)`，又通过 `@import "tailwindcss"` 带入了它的 reset。Simple.css 会给 `button/select/checkbox/details/dialog/aside/label/fieldset` 等裸元素加样式，模块里没覆盖到的属性全部漏进来。
- **目录塌陷**：Simple.css 的 `aside{float:right;width:30%;padding:0 15px}` 作用在 `.reader-toc-sidebar` 上。200px 的栅格列乘以 30% 只有 60px，扣掉 padding 和边框后，文字只剩约 12px 宽。
- **弹窗**：`<dialog>` 本身既带圆角又是滚动容器，页脚也处在滚动内容里。
- 项目里没有共享的组件层，每个页面的控件各写各的。

### 1.2 用户已定的事项

- **视觉方向 C**：以 Arc 的中性黑白灰为底（neutral accent），渐变只用在 Logo 标记、当前导航项、继续阅读卡片、目录当前章节指示线和阅读进度条这几处。
- **阅读页样稿通过**（`reader.html`）：
  - 工具栏每个按钮都有图标。“返回 / 标记已读 / 从头阅读”带文字，其余只放图标，悬停显示中文提示。
  - 目录固定宽 210px，标题最多两行，当前章节用渐变细线标出。
  - 选中文字时出现深色小浮条（高亮 / ×）。
  - 点击已有高亮时弹出白色菜单：顶部回显原文，每项都有图标，删除放最后并用红色。
  - 弹窗只有中间区域滚动，四角保持圆角，底部按钮常驻。
- **样式全部改用 CSS Modules**，移除 Sass、Simple.css 和 Tailwind。
- **深色模式**：整站跟随系统；侧边栏底部提供手动切换（浅色 / 深色 / 跟随系统），选择会被记住。
- **浏览器确认框**：`confirm()`/`alert()` 一起换成 Arc 风格的确认弹窗。
- **界面文字统一为中文**，`<html lang="zh-CN">`，日期按 zh-CN 格式显示。
- **样稿只作风格参考**：内容和功能保持现状。以下是样稿里为画面完整加上的额外内容，**都不做**：
  - 侧边栏各视图的文章数、搜索框里的 ⌘K 提示；
  - 继续阅读改成单卡片（仍然最多列出 5 篇，每篇换成新样式并带进度条）；
  - 阅读页的字数、预计阅读时长，以及目录下方的“已读 x%”；
  - 工具栏常驻的“已同步”状态和“打开原文”按钮（访问原文仍然留在文章头部）；
  - 订阅弹窗的步骤条和改名后的按钮（保留原有的步骤文字和“返回地址 / 确认添加”按钮）。
- **范围**：只改 Web 前端，Chrome 插件和后端都不动。所有功能和交互行为保持不变，包括阅读进度、高亮锚定、批量操作与撤销、列表缓存、吸顶测量、44px 触控目标、焦点管理。
- **验证约束**：这次不跑 Playwright E2E。视觉和手动检查连用户现有的本地后端，**只看不删**（见第 6 节）。

### 1.3 非目标

- 不加新功能，不增加新的数据请求，不改 API。
- 不改 `src/lib/**`、`src/hooks/**` 里的业务逻辑（`useArticleList`、`useReadingProgress`、`library-cache`、`progress-writer` 等）。唯一的例外是 `useAppHeaderOffset` 查找侧边栏用的选择器（见 3.6）。
- 不重做信息架构：页面、路由和导航项保持不变。
- 不修与改版无关的问题。标签页搜索防抖失效（`tags/page.tsx:52-56`）、标签按首字母 `name[0].toUpperCase()` 分组对中文不合理、未使用的 `sanitize.css` 依赖和 `.btn*` 全局类，这些只在交付说明里列出。

---

## 2. 视觉规范

全部以 Arc 的 `foundation.css` token 为准，不另起一套变量。

| 项 | 取值 |
|---|---|
| 颜色 | 中性灰阶 `--neutral-0…11`（oklch）；语义变量 `--background`、`--surface`、`--surface-muted`、`--foreground`、`--text-secondary`、`--text-muted`、`--border(-subtle/-strong)`；状态色 `--success/--warning/--danger/--info`；标签色 `--tone-*` |
| 渐变（C 方向） | `--arc-gradient`（淡紫到浅橘）、`--arc-gradient-soft`（浅底，用于继续阅读卡片和当前导航项）、`--arc-gradient-ink`（文字级深色，用于目录指示线和进度条） |
| 字号 | 12 / 13 / 15 / 17 / 20 / 22 / 28px（`--text-xs … --text-3xl`）；输入框文字 14px，手机上 16px |
| 字体 | 标题用 Geist（`--font-display`），正文用 Inter（`--font-body`），中文回退 PingFang SC / Noto Sans SC。阅读正文仍由阅读偏好里的字体决定 |
| 控件高度 | 32 / 40 / 44px（`--control-height-sm/md/lg`）；≤600px 或触控设备上，可点击区域保持 44px |
| 圆角 | 控件 14px、面板 20px、大面板/弹窗 24px（`--radius-control/panel/surface`）；筛选项用胶囊形 |
| 阴影 | `--shadow-resting`（卡片）、`--shadow-raised`（悬停）、`--shadow-floating`（浮层、弹窗） |
| 动效 | Arc 的 `motion-tokens`（snappy / smooth / morph），尊重 `prefers-reduced-motion` |
| 焦点 | 用鼠标点击时不显示焦点环，键盘操作时显示 accent 轮廓（foundation 内置） |
| 深色 | `:root[data-theme="dark"]`，直接用 Arc 的深色定义 |

**按钮规则**
- 同一组按钮要么全带图标，要么全不带。
- 只有图标、没有文字的按钮必须有中文 `aria-label`，并配 Arc Tooltip。
- 危险操作用 `danger` 变体，并放在同组的最后。

---

## 3. 技术方案

### 3.1 引入 Arc 组件

项目里没有 Tailwind，而 shadcn CLI 初始化时会尝试配置 Tailwind，所以**不用 CLI**。改为写一个一次性脚本，读取 `https://uiarc.dev/r/<name>.json`，把每个条目 `files[].content` 按 `target` 写入 `src/components/arc/`，递归处理 `registryDependencies`，并汇总所需的 npm 依赖。

- **需要的免费条目**：`arc-foundation`、`arc-motion-tokens`、`button`、`input`、`textarea`、`select`、`checkbox`、`dialog`、`popover`、`tooltip`、`tabs`、`avatar`。
- **新增依赖**：`motion`、`@radix-ui/react-{dialog,popover,select,checkbox,tooltip,tabs}`、`geist`、`@fontsource-variable/inter`。字体打进项目，构建时不需要联网。
- **移除依赖**：`sass`、`simpledotcss`、`tailwindcss`、`@tailwindcss/postcss`、`postcss`，以及 `src/postcss.config.mjs` 和 `tsconfig.json` 里对它的 include。

源码归项目所有，以下几处按需修改：
- **Dialog**：
  - 内容层改为 `overflow:hidden` 的纵向 flex 布局，新增 `footer` 插槽，只让中间主体滚动。这修复截图里的问题 4；Arc 原版的 `.content{overflow:auto}` 本身也有同样的问题。
  - 关闭按钮的 aria-label 改为“关闭”。
  - 透传 `onInteractOutside`/`onEscapeKeyDown`，用来保持现有约束：点外部不关闭，提交中不能按 Esc 关闭。
- **Select**：增加 `hideLabel`（标签只对读屏可见）和 `chip` 外观，供筛选胶囊使用。
- **Button**：沿用 primary / secondary / ghost / danger 四种变体和 sm / md / lg 三档，并保留现有的 44px 触控规则。

### 3.2 全局样式 `src/app/globals.css`（替换 `globals.scss`）

- 先 `@import` Arc 的 `foundation.css`，再加一段小型 reset，替代 Tailwind preflight 和 Simple.css：
  - box-sizing、margin 归零；
  - 媒体元素 `display:block`；
  - `[hidden]{display:none!important}`；
  - 表单控件继承字体；
  - 链接不加下划线；
  - 应用界面里的列表去掉项目符号（文章正文另行处理）。
- 覆盖 C 方向需要的少量 token，删除 maps 和 indigo 两套旧调色板。
- 只保留 `.sr-only` 和 `.skip-link` 两个全局工具类。
- 运行时写入的变量（`--app-header-offset`、`--reader-*`）用法不变。

### 3.3 主题切换

- 新增 `src/contexts/ThemeContext.tsx`：
  - 偏好取值为 `system | light | dark`，存在 `localStorage["app-theme"]`，读写都包 try/catch；
  - 把解析后的结果写到 `<html data-theme>`，并监听 `prefers-color-scheme` 的变化。
- `layout.tsx` 的 `<head>` 里放一段内联脚本，在首屏绘制前设置 `data-theme`，避免闪烁。
- 侧边栏底部的切换控件：展开时是三段切换（太阳 / 月亮 / 显示器图标）；收起时是一个图标按钮，点击循环切换，并配 Tooltip。
- 阅读主题选“跟随系统”时，改为跟随整站当前生效的主题（`Client.tsx` 读取 ThemeContext 的结果）。用户没手动切换时，整站本身就跟随系统，所以默认表现和现在一样。

### 3.4 转换到 CSS Modules 的规则

- 每个 `.module.scss` 都按新设计重写为 `.module.css`，不是逐行翻译。只用原生嵌套，`//` 注释一律改为 `/* */`。
- 现有文件没有用到 Sass 独有的语法（变量、mixin、`@extend`、函数、`&-suffix` 拼接），所以可以直接转换。第 1 步要先验证：在 webpack 和 turbopack 下，类名里嵌套的元素选择器都能通过 CSS Modules 的 pure 检查。不行就改用扁平选择器。
- 全局的 `client.scss` 拆成以下几个模块：
  - `Client.module.css`
  - `ReaderToolbar.module.css`
  - `ReaderPanel.module.css`
  - `ReaderSettings.module.css`
  - `ReaderToc.module.css`
  - `ReadableContent.module.css`：文章正文是注入的 HTML，所以这里用 `.content :global(h2)` 这种写法。
- **稳定钩子类名保留，但不再承载样式**。测试和 JS 依赖下面这些类名，它们和模块类名并列写在元素上（如 ``className={`${styles.toolbar} reader-toolbar`}``），样式全部放在模块里：
  - `reader`、`readable-content`、`reader-toolbar`
  - `reader-toc-sidebar`、`reader-toc-control`、`reader-toc-nested`
  - `reader-panel`、`reader-drawer`、`reader-backdrop`、`reader-backdrop-visible`
  - `reader-summary-banner`、`reader-source`、`reader-body-row`、`article-title-section`
  - `visit-original-btn primary`
  - `reader-table`：由 `reader-content.ts` 在运行时插入，样式写在 `ReadableContent.module.css` 的 `:global(.reader-table)` 里。
- `HighlightManager` 里的 `document.querySelector('.readable-content')` 保持不变。

### 3.5 阅读正文排版（替代 Simple.css 提供的部分）

`ReadableContent.module.css` 完整定义下列元素的样式：
- h1–h6、p、a；
- ul 用 disc，ol 用 decimal（顺带修复现在有序列表不显示序号的问题）；
- blockquote、pre/code/kbd/samp；
- 表格：th 底色、斑马纹，`.reader-table{overflow-x:auto}`；
- hr、mark、figure/figcaption、img/video、details/summary；
- 正文里出现的 aside/section/article 一律去掉装饰（不浮动、不加边框）。

阅读主题变量 `--reader-*`（light / dark / sepia）映射到 Arc token。`data-reader-theme` 和阅读偏好对应的变量（`--reader-font-size`、`--reader-line-height`、`--reader-max-width`、`--reader-font`、`--reader-mono`）都保持不变。

### 3.6 不换成 Radix 的地方

| 部件 | 处理方式 | 原因 |
|---|---|---|
| `ReaderPanel`（设置、目录的弹层和抽屉） | 保留自写的定位、焦点循环、断点（768/1200）、`aria-modal` 和 `position fixed/absolute`，只重做样式 | Radix 会把弹层挂到 `body` 下，脱离 `main.reader` 上的主题变量；模态时会锁住页面滚动，而阅读进度依赖 window 滚动；e2e 也断言了这里的几何位置 |
| `HighlightManager` 的两个浮层 | 保留 `@floating-ui/react`，选区浮条仍锚定在由选区生成的虚拟元素上；按样稿重做样式，新增 Esc 关闭 | 需要虚拟锚点，并且同样要保持在 `.reader` 主题范围内 |
| 视图切换、阅读设置里的分段按钮 | 保持 `aria-pressed` 的按钮语义，只套 Arc 分段控件的外观 | 保持现有的可访问性语义 |
| 行距滑块 | 保留原生 `<input type=range step=0.1>`，用 Arc 的 `--control-*` token 画样式 | 同上 |
| 侧边栏 | `aria-label="Sidebar"` 改为“侧边栏”；`useAppHeaderOffset` 改用 `data-app-sidebar` 属性查找侧边栏 | 不再依赖英文的 aria-label |

---

## 4. 各区域设计

1. **应用外壳**（`layout/Sidebar.tsx`、`ClientLayout.tsx`）
   - 品牌标记用渐变；导航全部改为中文：收件箱、全部文章、收藏、归档、回收站、订阅、高亮笔记、标签、工作区、个人账户、退出登录。
   - 当前导航项用渐变浅底；用户区用 Arc Avatar；底部放主题切换。
   - 移动端的订阅列表 `<dialog>` 改为 Arc Dialog，保持“切到桌面宽度时自动关闭”和焦点返回。
   - 232/76px 的侧边栏宽度和移动端 68px 头部的几何关系不变。
2. **文库**（`page.tsx`、`components/LibraryToolbar.tsx`、`BatchToolbar.tsx`、`ArticleCard.tsx`、`ArticleList.tsx`）
   - 顶部一行：Arc Input 搜索框（带图标和清除按钮，300ms 防抖不变）、带图标的“选择”按钮、视图分段切换。
   - 阅读状态、标签：`<details>` 改为 Arc Popover 胶囊，内部用 Arc Checkbox。标签搜索和“最多 10 个”的上限不变。原来手写的外部点击关闭和 Esc 关闭由 Radix 接管。
   - 来源、订阅源、保存时间、排序：原生下拉框改为 chip 外观的 Arc Select，并删掉注入的 `appearance: base-select` 样式。自定义日期仍用原生 `type=date`，套 Arc 样式。
   - 已生效的筛选显示为可移除的胶囊，加“清除筛选”。
   - 卡片：
     - 悬停或聚焦时，左上角浮出 22px 的 Arc 复选框，选中后卡片显示描边；触控设备只在选择模式下显示复选框（保持现状）。
     - 操作按钮只放图标并配 Tooltip，`aria-pressed` 的按钮顺序不变（收藏在前、归档在后）。
   - 批量栏：浮动条，底色与页面主背景一致（`--background`），用细边框和轻阴影与内容区分，不用深色底（用户审阅实现后要求）；吸顶位置仍由 `useAppHeaderOffset` 决定；批量打标签改为 Popover 加 Checkbox。
   - 继续阅读：最多 5 篇，每篇是渐变浅底卡片加进度条。回收站卡片、加载更多、空状态和加载状态都一并重做。
3. **阅读页**（`pages/article/[id]/components/*`）
   - 工具栏按第 2 节的按钮规则；进度条用渐变；同步状态只在未同步时显示（保持现状）。
   - 目录：210px 宽，标题两行截断，当前章节用 `--arc-gradient-ink` 细线标出；1200px 以下仍收进工具栏的抽屉。
   - 冲突条、读完提示、摘要横幅、链接详情、来源信息、文章头部（访问原文按钮位置不变）全部重做。
   - 高亮浮层按样稿重做；`HighlightList` 和 `TagManager` 改为中文，并换用 Arc 控件。
   - 以下保持不变：`--reader-header-offset`/`--reader-toolbar-height`、标题的 scroll-margin、IntersectionObserver 的 rootMargin `-20% 0px -70% 0px`、1200/768 断点。
4. **其他页面**
   - 订阅页和 `FeedDialog`：
     - Arc Dialog 分为页头、可滚动主体、常驻页脚三部分；
     - 保持“提交中不能按 Esc 关闭”“点外部不关闭”、URL 输入框自动聚焦；
     - 标题随步骤变化，按钮名称保持现状。
   - 高亮笔记页、标签页、标签详情页、账户页（改用 Arc Tabs）、登录页全部重做。
   - 合并高亮的提示和账户页的提示条改成 Arc 风格，触发机制不变。
5. **确认弹窗**
   - 新增 `src/contexts/ConfirmContext.tsx`，提供 `useConfirm()`，返回 `Promise<boolean>`。它基于 Arc Dialog 实现，危险操作的确认按钮用 danger 变体。
   - 替换以下 `confirm()`：`page.tsx:57,83`、`ArticleCard.tsx:31,70`、`Client.tsx:92`、`feeds/page.tsx:16-17`、`TagDetail.tsx:48,54`、`account/page.tsx:169`。
   - `TagManager.tsx:47,59` 的 `alert()` 改为 Toast。
   - `ConfirmProvider` 和 `ToastProvider` 一起挂在 `ClientLayout`。
6. **Toast**（`contexts/ToastContext.tsx`）：只换外观（Arc 卡片样式、X 图标、动效）。最多 3 条、撤销、分批恢复、重试、`aria-live="polite"` 这些逻辑都不变。

---

## 5. 测试

测试改动都是有意为之，改动时保持元素的可访问名称和角色不变。

- 新增 `vitest.setup.ts`，并配进 `vitest.config.mts` 的 `setupFiles`，提供以下兼容实现：
  - 稳健的 `matchMedia`：能处理不含数字的查询，带 `addEventListener`；
  - `ResizeObserver`；
  - `hasPointerCapture`/`releasePointerCapture`；
  - `scrollIntoView`。

  同时修正 `ReaderPanel.test.tsx` 和 `Sidebar.test.tsx` 里的 matchMedia mock：前者遇到 motion 发出的 `(prefers-reduced-motion: reduce)` 这类不含数字的查询会抛错。
- **单元测试需要改的**：
  - `LibraryToolbar.test.tsx`、`page.test.tsx`、`page.batch.test.tsx`：`input:checked` 改为 `[role=checkbox][data-state=checked]`，筛选相关的用例要先打开 Popover 再操作；
  - `Sidebar.test.tsx`、`FeedDialog.test.tsx`：Portal 里的内容要从 `document.body` 查找；
  - `ReaderSettings.test.tsx`：配合 setup 的调整；
  - `highlights/page.test.tsx:19`：英文名称改为中文。
- **e2e 需要同步改的**（本次只做类型检查，不运行）：
  - `library.spec.ts:113-121,134-138`：改为先打开筛选胶囊，再点选项；
  - `library.spec.ts:158-159`、`feeds.spec.ts:36,129`：英文名称改为中文；
  - `feeds.spec.ts:184-191`：`selectOption` 改为打开 Select 后点选项；
  - `batch-trash.spec.ts:97,101,118,121` 和 `feeds.spec.ts:98`：处理原生 dialog 的写法改为点击确认弹窗。
- `reader-preferences.spec.ts` 里目录和设置面板的几何断言预计不用改，因为 `ReaderPanel` 的结构没有变。

---

## 6. 验收

- **自动检查**（在 `src/frontend` 下）：
  - `pnpm exec tsc --noEmit`、`pnpm test`、`pnpm build` 都要通过；
  - lint 只检查改动范围：`pnpm exec eslint src e2e --ignore-pattern src/app/pages/account/page.tsx`。account 页原有 8 个 lint 错误，如果重写后数量有变化，按实际结果报告。
- **视觉**：`pnpm dev` 连用户现有的本地后端，在 1280×800、1024×768、375×812 三个尺寸下，分别检查浅色和深色，截图和两份样稿对照。必须做到：
  - 截图里的五个问题全部消失；
  - 没有横向溢出；
  - 浮层和弹窗在深色模式下显示正确。
- **手动抽查（只看不删，不写用户数据）**：
  - 筛选组合与 URL 同步；
  - 进入选择模式并勾选卡片：批量栏出现后直接退出，不执行任何操作；
  - 选中文字：浮条出现，能用 × 和 Esc 关闭（不点“高亮”）；
  - 点已有高亮：菜单弹出后可用 Esc 关闭；
  - 目录跳转；设置抽屉关闭后焦点回到触发按钮；
  - 订阅弹窗打开到第 2 步后取消；
  - 每种确认弹窗：弹出后点“取消”。

  永久删除、清空回收站、取消订阅、删除已导入文章，都只测到确认弹窗弹出为止。删除高亮沿用原有的直接删除行为，验收时只打开并关闭菜单。不执行批量操作，也不新建高亮或标签。打开文章正常滚动时，会像平时阅读一样自动保存阅读位置，这是现有行为。长目录、表格、有序列表、代码块这类排版情况，如果现有数据里没有，就用单元测试和只读页面检查覆盖，不往用户的库里写数据。
- **E2E**：按用户要求，本次不运行。用例会同步更新并通过类型检查，交付说明里会写明这一点。
- **文档**：更新项目 `CLAUDE.md` 前端相关的描述：改用 CSS Modules、引入 Arc UI、整站深色模式、移除 Simple.css 和 Tailwind。

---

## 7. 实施顺序（概要，细节由实施计划展开）

1. **地基**：
   - 引入 Arc 源码和依赖、字体、`globals.css`、ThemeContext 和防闪烁脚本；
   - 所有 `.module.scss` 先机械改为 `.module.css`，`client.scss` 暂时改成全局 `client.css`；
   - 移除 Sass、Simple.css 和 Tailwind，加上 vitest setup。

   这一步结束时构建和测试都要通过，界面可以暂时粗糙。
2. 应用外壳和主题切换。
3. 文库。
4. 阅读页：拆分 `client.css`，完成正文排版和三种阅读主题。
5. 订阅、高亮笔记、标签、账户、登录页，以及确认弹窗和 Toast。
6. 收尾：清理残留英文、更新 e2e、更新 `CLAUDE.md`。

每一步结束都跑类型检查和单元测试，并在三个尺寸、浅深两色下截图检查。

开工前先从 `main` 切出分支 `feature/arc-ui`，并把 `.superpowers/` 加进 `.gitignore`。用户没有要求就不提交。

---

## 8. 由实现者自行决定的部分

- 每个组件内部的具体间距和层级细节：在第 2 节 token 的范围内取值，以样稿为准。
- 空状态和加载状态的具体排版。
- Tooltip 的延迟时间、弹层的偏移量。
- 文库卡片网格在各断点下的列数：保持现有的响应式行为，只调整间距。
