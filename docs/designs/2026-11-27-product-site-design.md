# 产品网站设计

- 日期：2026-11-27
- 状态：已实现，待确认事项见第 5 节

---

## 1. 目标

为 MiniCC 建一个产品网站，包含首页和文档，托管在 Cloudflare 上，域名暂定 `cc.zxyao.net`。

用户提出的要求：

- 有首页和文档页；
- 文档介绍安装、使用、配置等内容；
- 打 tag 时自动部署；
- 支持中英双语；
- 托管到 Cloudflare。

## 2. 技术选型

| 事项 | 选择 | 理由 |
| --- | --- | --- |
| 站点框架 | Astro 7 + Starlight 0.43 | 静态输出；自带多语言路由、语言切换、按语言划分的 Pagefind 搜索、站点地图和深浅色主题 |
| 位置 | 仓库根目录的 `site/` | 和产品代码（`src/`）分开，部署流程只关心这个目录 |
| 包管理 | `site/` 自成一个 pnpm 工作区，有自己的 `pnpm-lock.yaml` | 实测把它加进根工作区后，Astro 的 peer 依赖会让 pnpm 重新解析根和扩展的依赖（例如根目录的 eslint 从 9.30.1 变成 9.39.5）。独立工作区不碰产品的锁文件，也不影响 Web 镜像构建 |
| 托管 | Cloudflare Workers 静态资源（`wrangler deploy`） | 自定义域名可以写在 `wrangler.jsonc` 里，随部署生效，不需要在控制台另外配置 |
| 字体 | Geist、Geist Mono（Fontsource 本地打包），首页示例正文用 Source Serif 4；中文用系统字体 | 和应用本身一致；不依赖 Google Fonts，国内访问不受影响 |

## 3. 结构

- 简体中文为根语言（`/`），英文在 `/en/`。
- 首页：`src/content/docs/index.mdx`、`src/content/docs/en/index.mdx`，只放各自的 hero 文案；版式在 `src/components/Hero.astro`（覆盖 Starlight 的 Hero）和 `Home.astro`，两种语言的其余文案在 `home-content.ts`。
- 文档：`/docs/` 下九页，分三组：开始（简介、安装部署、浏览器扩展）、使用（使用指南、RSS 订阅、API）、配置与运维（配置参考、图片存储、备份与升级）。英文页面路径相同，前面加 `/en`。
- 截图直接引用仓库根目录的 `assets/`，和 README 共用一份。

### 3.1 视觉

应用界面是单色的（Arc UI），唯一的颜色是阅读器高亮 `#ead386`。网站沿用中性灰，把这支荧光笔色作为强调色：首页的步骤编号、文本选区和高亮都用它，按钮保持应用里的墨色。

首页唯一的动效是 hero 右侧的阅读器示例：页面加载后，荧光笔逐行划过一句话，随后出现一条笔记。开启“减少动态效果”时直接显示最终状态。

界面只有中文，英文文档沿用 README 的写法：先写中文标签，括号里给英文，例如 **收件箱** (Inbox)。

## 4. 部署

`.github/workflows/deploy-site.yml`：

- 触发：推送 `v*` tag；也可以在 Actions 页面手动运行。
- 步骤：安装 Node 22 和 pnpm 12.5.1（读取 `site/package.json` 的 `packageManager`），`pnpm install --frozen-lockfile`、`pnpm build`，然后用 `npx wrangler@4.163.0 deploy` 发布。
- 需要仓库 Secrets：`CLOUDFLARE_API_TOKEN`、`CLOUDFLARE_ACCOUNT_ID`。
- `wrangler.jsonc` 里的 `custom_domain` 路由要求 `zxyao.net` 这个 zone 在同一个 Cloudflare 账号下。

## 5. 决定与待办

用户已确认（2026-11-27）：

- 设计和内容以本项目的代码和 README 为依据，没有另外的参考站点。
- 中文为根路径，英文在 `/en/`。
- 使用 Cloudflare Workers 静态资源。
- tag 规则为 `v*`。

首次部署前需要用户完成：

- 确认 `zxyao.net` 已托管在部署用的 Cloudflare 账号下。
- 在仓库 Secrets 中加入 `CLOUDFLARE_API_TOKEN` 和 `CLOUDFLARE_ACCOUNT_ID`。

仍待决定：

1. README 是否加上网站链接。
2. `AesKeys`：仓库自带的默认密钥是公开的。文档目前只说明不要改动在用的条目，是否要补充生产环境更换密钥的步骤（需要先确认更换后已有 API 密钥如何处理）。

## 6. 验证

- `pnpm build` 生成 21 个页面，站内链接和锚点全部有效（脚本检查 `dist/`）。
- 在干净目录里执行 `pnpm install --frozen-lockfile` 和 `pnpm build`，模拟 CI 通过。
- `wrangler deploy --dry-run` 读取 120 个静态文件，配置有效。
- 浏览器检查：1280px 和 375px，浅色和深色主题；375px 下首页和文档页都没有横向滚动。
