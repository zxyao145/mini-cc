# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Docker Stack (Recommended)
- `./docker-build.sh` - Build all container images
- `./docker-run.sh` - Start complete stack with PostgreSQL, nginx, API, frontend, and readability service
- External access: http://localhost:5000 (nginx proxy routes to appropriate services)

### Backend Commands (from `/src/backend/MiniCc.Api/`)
- `dotnet run` - Start the API server (https://localhost:5001, http://localhost:5000)
- `dotnet build` - Build the backend
- `dotnet build` (from solution root) - Build entire solution including ContentHandler
- `dotnet tool install --global dotnet-ef` - Install EF Core tools globally (one-time setup)
- `dotnet ef migrations add <MigrationName>` - Create new migration
- `dotnet ef database update` - Apply database migrations

### Frontend Commands (from `/src/frontend/`)
- `pnpm dev` - Start Next.js development server with Turbopack (http://localhost:3000)
- `pnpm dev-https` - Start development server with HTTPS
- `pnpm build` - Build for production
- `pnpm start` - Start production server
- `pnpm lint` - Run ESLint

### Web Extension Commands (from `/src/web-extension/`)
- `pnpm dev` - Start Chrome Extension development
- `pnpm build` - Build Chrome Extension for production
- `pnpm lint` - Run ESLint

### Readability API Commands (from `/src/backend/readability-api/`)
- `npm start` - Start content extraction service (http://localhost:5002)

## Architecture Overview

### Project Structure
This is a microservices-based read-later application with:
- **Backend API**: ASP.NET Core 9.0 Web API with PostgreSQL database
- **Frontend**: Next.js 15 with TypeScript, SCSS, and App Router
- **Web Extension**: Vite-based TypeScript Chrome extension
- **Readability Service**: Node.js service for content extraction using Mozilla Readability
- **Load Balancer**: Nginx reverse proxy for routing
- **Database**: PostgreSQL 16 with Chinese text search (zhparser extension)

### Backend Architecture (`/src/backend/MiniCc.Api/`)
- **Authentication**: Dual authentication (Cookie for web, AccessKey for API/extension)
- **Controllers**: API endpoints for articles, tags, and highlights
- **Models**: Entity models (Article, Tag, Highlight) with EF Core annotations, Primary Key Type Guid
- **Data**: Entity Framework DbContext with PostgreSQL provider, sensitive data encryption
- **Services**: Business logic services (ArticleService, TagService, HighlightService)
- **ContentHandler**: Chain of responsibility pattern for content extraction with extensible handlers
- **Database**: PostgreSQL with Entity Framework Core 9.0 and Chinese text search support

### Content Processing Pipeline
Content extraction follows this flow:
1. **URL Input** → ContentHandler (ASP.NET Core)
2. **Raw HTML Fetch** → ContentHandler using HtmlAgilityPack
3. **Content Cleaning** → Readability API (Node.js service with Mozilla Readability)
4. **Processed Content** → Database storage with metadata

### Docker Service Architecture
```
nginx (port 5000) → Routes traffic to:
├── minicc-api (port 8080 internal) → Main ASP.NET Core API
├── minicc-web (port 3000 internal) → Next.js frontend
└── readability-api (port 5002) → Content extraction service
                   ↓
            PostgreSQL (port 15432) → Database with zhparser
```

### Frontend Architecture (`/src/frontend/src/`)
- **App Router**: Next.js 15 app directory structure
- **Components**: React functional components with SCSS modules and Tailwind CSS
- **API Client**: Centralized axios-based API client in `lib/api.ts`
- **Types**: TypeScript definitions in `types/index.ts`

### Key Dependencies
- **Backend**: Entity Framework Core, HtmlAgilityPack (web scraping), Npgsql (PostgreSQL), Mapster (object mapping)
- **Frontend**: React 19, Next.js 15, TypeScript, Axios, SASS, Tailwind CSS
- **Web Extension**: Vite, TypeScript, SCSS, vite-plugin-web-extension
- **Readability Service**: @mozilla/readability, Express.js, JSDOM
- **Infrastructure**: PostgreSQL 16, Nginx, Docker/Podman

### Database Schema
Three main entities with relationships:
- **Articles**: Core content with title, URL, content, metadata (author, summary, etc.)
- **Tags**: Many-to-many relationship with Articles via junction table
- **Highlights**: One-to-many relationship with Articles, stores text selections with notes

### API Endpoints
Base URL: `http://localhost:5000/api`
- Articles CRUD: `/articles` (GET, POST, PUT, DELETE)
- Article actions: `/articles/{id}/favorite`, `/articles/{id}/archive`
- Tags: `/articles/{id}/tags` (POST, DELETE)
- Highlights: `/articles/{id}/highlights` (POST), `/highlights/{id}` (DELETE)

### Environment Configuration
- **Backend connection string**: Set via `MiniCC_Db` environment variable or `appsettings.json`
- **Readability API URL**: Set via `MiniCC_ReadabilityApi` environment variable (defaults to http://127.0.0.1:5002)
- **Frontend API URL**: Set via `NEXT_PUBLIC_API_URL` environment variable
- **CORS**: Configured for `http://localhost:3000` and `http://localhost:5000` in development
- **Container Mode**: Use `./docker-run.sh` for full stack with proper service discovery

### Authentication Methods
- **Web Frontend**: Cookie-based authentication for browser sessions
- **Web Extension/API**: AccessKey authentication for programmatic access
- **Dual Support**: Both methods available simultaneously

### Development Prerequisites
- .NET 9.0 SDK
- Node.js 18+ and pnpm
- PostgreSQL database (or use Docker stack)
- Entity Framework Core CLI tools (`dotnet tool install --global dotnet-ef`)
- Docker/Podman for containerized development (recommended)

### Testing
- **Backend**: No test projects currently configured
- **Frontend**: No test framework currently configured  
- **Linting**: ESLint available for frontend and web extension

## Checkpoint 记录

项目: MiniCC (Mini Cut Collection)
时间: 2025-07-13 17:30:00 +0800
里程碑: 用户界面重构与功能完善阶段

### 技术状态
- 代码质量: 良好 (8/10)
- 架构健康: 稳定 (8/10)
- 开发活跃度: 高

### 历史轨迹分析
- 首次检查点: 无历史记录
- 期间提交: 13个提交 (高活跃度)
- 主要活动: UI重构, 功能增强, 账户系统
- 发展趋势: 快速上升

### 文档状态
- README.md: 最新且完整
- CLAUDE.md: 已更新
- 配置同步: 完成

### 期间主要进展
- **UI重构** (122e90c): Tailwind CSS集成，全新组件设计
- **账户功能** (f6b65d0): 完整的用户账户管理系统
- **标签高亮** (269827f): 文章标签和高亮功能
- **中文支持** (41dc069): 中文全文搜索支持
- **架构优化** (c38a553): ID系统改为GUID

### 当前技术栈健康度
- **前端**: Next.js 15 + Tailwind CSS + TypeScript (现代化)
- **后端**: ASP.NET Core 9.0 + PostgreSQL (稳定)
- **扩展**: Chrome Extension + Vite (活跃开发)
- **服务**: 微服务架构良好分离

### 建议行动
1. 继续UI优化和用户体验改进
2. 考虑添加测试覆盖
3. 性能优化和监控
4. 文档持续维护

Git提交: 122e90c