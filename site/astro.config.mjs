// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

export default defineConfig({
	site: 'https://cc.zxyao.net',
	// The homepage imports the README screenshots from the repository's assets/ directory.
	vite: { server: { fs: { allow: ['..'] } } },
	integrations: [
		starlight({
			title: 'MiniCC',
			logo: { light: './src/assets/logo-light.svg', dark: './src/assets/logo-dark.svg' },
			favicon: '/favicon.svg',
			defaultLocale: 'root',
			locales: {
				root: { label: '简体中文', lang: 'zh-CN' },
				en: { label: 'English', lang: 'en' },
			},
			social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/zxyao145/mini-cc' }],
			customCss: [
				'@fontsource-variable/geist',
				'@fontsource-variable/geist-mono',
				'./src/styles/theme.css',
			],
			components: { Hero: './src/components/Hero.astro' },
			sidebar: [
				{
					label: '开始',
					translations: { en: 'Getting started' },
					items: ['docs', 'docs/installation', 'docs/extension'],
				},
				{
					label: '使用',
					translations: { en: 'Using MiniCC' },
					items: ['docs/usage', 'docs/feeds', 'docs/api'],
				},
				{
					label: '配置与运维',
					translations: { en: 'Configuration and operations' },
					items: ['docs/configuration', 'docs/images', 'docs/maintenance'],
				},
			],
		}),
	],
});
