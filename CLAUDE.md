# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Root Project Commands (from `/src/`)
- `pnpm dev` - Run both backend and frontend concurrently in development mode
- `pnpm dev:backend` - Run only the ASP.NET Core API (`cd backend/MiniCc.Api && dotnet run`)
- `pnpm dev:frontend` - Run only the Next.js frontend (`cd frontend && pnpm dev`)
- `pnpm build` - Build the frontend for production
- `pnpm build:backend` - Build the backend (`cd backend/MiniCc.Api && dotnet build`)
- `pnpm install:all` - Install dependencies for both backend and frontend
- `pnpm lint` - Run ESLint on frontend code
- `pnpm db:migrate` - Apply database migrations (`cd backend/MiniCc.Api && dotnet ef database update`)
- `pnpm db:migration` - Create new migration (`cd backend/MiniCc.Api && dotnet ef migrations add`)

### Backend Commands (from `/src/backend/MiniCc.Api/`)
- `dotnet run` - Start the API server (https://localhost:5001, http://localhost:5000)
- `dotnet build` - Build the backend
- `dotnet ef database update` - Apply database migrations
- `dotnet ef migrations add <MigrationName>` - Create new migration
- `dotnet tool install --global dotnet-ef` - Install EF Core tools globally (one-time setup)

### Frontend Commands (from `/src/frontend/`)
- `pnpm dev` - Start Next.js development server with Turbopack (http://localhost:3000)
- `pnpm build` - Build for production
- `pnpm start` - Start production server
- `pnpm lint` - Run ESLint

## Architecture Overview

### Project Structure
This is a full-stack read-later application with:
- **Backend**: ASP.NET Core 9.0 Web API with PostgreSQL database
- **Frontend**: Next.js 15 with TypeScript, SCSS, and App Router
- **Monorepo**: Uses pnpm workspaces for dependency management

### Backend Architecture (`/src/backend/MiniCc.Api/`)
- **Controllers**: API endpoints for articles, tags, and highlights
- **Models**: Entity models (Article, Tag, Highlight) with EF Core annotations
- **Data**: Entity Framework DbContext with PostgreSQL provider
- **Services**: Business logic services (ArticleService, WebScraperService)
- **Database**: PostgreSQL with Entity Framework Core 9.0

### Frontend Architecture (`/src/frontend/src/`)
- **App Router**: Next.js 15 app directory structure
- **Components**: React functional components with SCSS modules
- **API Client**: Centralized axios-based API client in `lib/api.ts`
- **Types**: TypeScript definitions in `types/index.ts`

### Key Dependencies
- **Backend**: Entity Framework Core, HtmlAgilityPack (web scraping), Npgsql (PostgreSQL)
- **Frontend**: React 19, Next.js 15, TypeScript, Axios, SASS

### Database Schema
Three main entities with relationships:
- **Articles**: Core content with title, URL, content, metadata (author, summary, etc.)
- **Tags**: Many-to-many relationship with Articles via junction table
- **Highlights**: One-to-many relationship with Articles, stores text selections with notes

### API Endpoints
Base URL: `https://localhost:5001/api`
- Articles CRUD: `/articles` (GET, POST, PUT, DELETE)
- Article actions: `/articles/{id}/favorite`, `/articles/{id}/archive`
- Tags: `/articles/{id}/tags` (POST, DELETE)
- Highlights: `/articles/{id}/highlights` (POST), `/highlights/{id}` (DELETE)

### Environment Configuration
- Backend connection string in `appsettings.json`
- Frontend API URL via `NEXT_PUBLIC_API_URL` environment variable
- CORS configured for `http://localhost:3000` in development

### Development Prerequisites
- .NET 9.0 SDK
- Node.js 18+ and pnpm
- PostgreSQL database
- Entity Framework Core CLI tools (`dotnet tool install --global dotnet-ef`)