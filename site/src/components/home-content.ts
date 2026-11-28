export type HomeLang = 'zh-CN' | 'en';

interface HomeContent {
	demo: {
		label: string;
		source: string;
		readTime: string;
		title: string;
		before: string;
		highlight: string;
		after: string;
		noteLabel: string;
		note: string;
		tags: { name: string; color: string }[];
	};
	library: { alt: string; caption: string };
	features: {
		heading: string;
		groups: { title: string; items: string[] }[];
	};
	start: {
		heading: string;
		services: { title: string; body: string; download: string; preview: string };
		steps: { title: string; body: string }[];
		more: { text: string; link: string };
	};
	footer: { license: string; source: string; otherLanguage: { text: string; lang: string; link: string } };
}

const repo = 'https://github.com/zxyao145/mini-cc';

export const homeContent: Record<HomeLang, HomeContent> = {
	'zh-CN': {
		demo: {
			label: 'MiniCC 阅读器示例：一段被高亮并添加了笔记的文章',
			source: 'example.com',
			readTime: '约 6 分钟',
			title: '收藏夹里的链接为什么总会失效',
			before:
				'很多链接存下时还好好的，过一两年再点开，页面已经改版、搬了家，或者干脆打不开了。',
			highlight: '只存链接，留下的只是一个地址；把正文存下来，这篇文章才算真的留住了。',
			after: '所以剪藏要趁页面还在的时候做，把能读的部分完整存好。',
			noteLabel: '笔记',
			note: '存正文，别只存链接。',
			tags: [
				{ name: '阅读', color: '#10b981' },
				{ name: '工具', color: '#3b82f6' },
			],
		},
		library: {
			alt: 'MiniCC 收件箱：顶部是筛选条件，下方是带标签的文章卡片',
			caption: '收件箱默认只显示未读文章，可以按阅读状态、标签、来源、订阅源和保存时间筛选。',
		},
		features: {
			heading: 'MiniCC 能做什么',
			groups: [
				{
					title: '收集',
					items: [
						'浏览器扩展在你自己的浏览器里提取正文，需要登录才能看的页面也能存。',
						'可以存全文，也可以只存链接，或者复制成带元数据的 Markdown。',
						'订阅 RSS 和 Atom，新条目自动加上默认标签。',
					],
				},
				{
					title: '阅读',
					items: [
						'字号、行高、版心宽度、字体和主题都能调，主题包括护眼色。',
						'阅读位置在设备之间同步，长文自动生成目录。',
						'高亮段落、写笔记，所有高亮集中在一个页面回顾。',
					],
				},
				{
					title: '整理',
					items: [
						'彩色标签，随时改名、换色。',
						'全文搜索，使用 PostgreSQL 时支持中文分词。',
						'批量处理文章，删除可以撤销，回收站保留 30 天。',
					],
				},
				{
					title: '自托管',
					items: [
						'一条 Compose 命令启动全部服务。',
						'数据库用 PostgreSQL 或 SQLite。',
						'文章图片可存到本地目录或 Cloudflare R2 等 S3 兼容存储，也可用 ImageProxy 按需代理。',
					],
				},
			],
		},
		start: {
			heading: '三步开始使用',
			services: {
				title: '启动服务',
				body: '在空目录中运行以下命令，Podman 会从 GitHub Container Registry（GHCR）拉取 Linux amd64 镜像并启动 SQLite 版。PostgreSQL 部署见完整安装说明。',
				download: '下载 docker-compose.yaml',
				preview: '查看 YAML 内容',
			},
			steps: [
				{
					title: '登录',
					body: '打开 http://localhost:5000，用户名 mini_cc，密码 mini_cc_password。登录后先在账户设置里改掉用户名和密码。',
				},
				{
					title: '连接浏览器扩展',
					body: '按安装说明下载并加载 Chrome 扩展，填入服务器地址和 API 密钥，就可以开始剪藏了。',
				},
			],
			more: { text: '查看完整安装说明', link: '/docs/installation/' },
		},
		footer: {
			license: 'MiniCC 以 MIT 许可证开源。',
			source: '源代码',
			otherLanguage: { text: 'English', lang: 'en', link: '/en/' },
		},
	},
	en: {
		demo: {
			label: 'MiniCC reader example: an article passage with a highlight and a note',
			source: 'example.com',
			readTime: '6 min read',
			title: 'Why saved links stop working',
			before:
				'A link that worked the day you saved it can lead nowhere a year later. Pages get redesigned, sites move, and some simply disappear.',
			highlight: 'Saving a link keeps an address. Saving the text keeps the article.',
			after: 'So clip a page while it is still there, and keep the readable part in full.',
			noteLabel: 'Note',
			note: 'Keep the text, not just the link.',
			tags: [
				{ name: 'Reading', color: '#10b981' },
				{ name: 'Tools', color: '#3b82f6' },
			],
		},
		library: {
			alt: 'The MiniCC inbox, with filters at the top and tagged article cards below',
			caption:
				'The inbox shows unread articles by default. Filter by reading status, tags, source, feed or save date.',
		},
		features: {
			heading: 'What MiniCC does',
			groups: [
				{
					title: 'Collect',
					items: [
						'The browser extension extracts the article in your own browser, so pages behind a sign-in can be saved too.',
						'Save the full article or just the link, or copy the page as Markdown with front matter.',
						'Follow RSS and Atom feeds and tag new entries automatically.',
					],
				},
				{
					title: 'Read',
					items: [
						'Adjust font size, line height, width, typeface and theme, including sepia.',
						'Your reading position syncs across devices, and long articles get a table of contents.',
						'Highlight passages, add notes, and review every highlight on one page.',
					],
				},
				{
					title: 'Organize',
					items: [
						'Colored tags that you can rename and recolor at any time.',
						'Full-text search, with Chinese word segmentation on PostgreSQL.',
						'Act on many articles at once, undo deletions, and restore from the trash for 30 days.',
					],
				},
				{
					title: 'Self-host',
					items: [
						'One Compose command starts every service.',
						'Run it on PostgreSQL or SQLite.',
						'Keep article images on local disk or in S3-compatible storage such as Cloudflare R2, or serve them on demand with ImageProxy.',
					],
				},
			],
		},
		start: {
			heading: 'Get started in three steps',
			services: {
				title: 'Start the services',
				body: 'Run these commands in an empty directory. Podman pulls the Linux amd64 images from GitHub Container Registry (GHCR) and starts MiniCC with SQLite. For PostgreSQL, see the full installation guide.',
				download: 'Download docker-compose.yaml',
				preview: 'View YAML contents',
			},
			steps: [
				{
					title: 'Sign in',
					body: 'Open http://localhost:5000 and sign in as mini_cc with the password mini_cc_password. Change both in account settings right away.',
				},
				{
					title: 'Connect the browser extension',
					body: 'Follow the installation guide to download and load the Chrome extension, then enter your server address and an API key to start clipping.',
				},
			],
			more: { text: 'Read the full installation guide', link: '/en/docs/installation/' },
		},
		footer: {
			license: 'MiniCC is open source under the MIT License.',
			source: 'Source code',
			otherLanguage: { text: '简体中文', lang: 'zh-CN', link: '/' },
		},
	},
};

export const repoUrl = repo;

export function homeLang(lang: string): HomeLang {
	return lang === 'en' ? 'en' : 'zh-CN';
}
