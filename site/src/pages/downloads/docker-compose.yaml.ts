import type { APIRoute } from 'astro';
import { quickstartCompose } from '../../components/quickstart-compose';

export const GET: APIRoute = () => new Response(quickstartCompose, {
	headers: { 'Content-Type': 'application/yaml; charset=utf-8' },
});
