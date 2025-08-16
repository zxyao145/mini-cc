# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Quick Start

### Docker (Recommended)
```bash
./docker-build.sh  # Build all services
./docker-run.sh    # Start full stack (nginx:5000)
```

### Manual Setup
```bash
# Backend (src/backend/MiniCc.Api/)
dotnet ef database update  # Apply migrations
dotnet run                 # Start API (http://localhost:5000)

# Frontend (src/frontend/)
pnpm install
pnpm dev                   # Start Next.js (http://localhost:3000)

# Web Extension (src/web-extension/)
pnpm install
pnpm dev                   # Build extension for Chrome
```

## Architecture Overview

### Microservices Architecture
- **API**: ASP.NET Core 9.0 with Clean Architecture (CQRS + DDD)
- **Frontend**: Next.js 15 App Router with TypeScript
- **Content Service**: Node.js readability extraction
- **Database**: PostgreSQL 16 with Chinese text search
- **Proxy**: Nginx routing (localhost:5000)

### Clean Architecture Layers
```
src/backend/MiniCc.Api/Core/
├── {Domain}/Application/   # CQRS handlers, DTOs
├── {Domain}/Domain/        # Entities, value objects
└── {Domain}/Infrastructure/# Repositories, configurations
```

### Key Patterns
- **CQRS**: Commands/Queries with MediatR handlers
- **Repository**: Generic repository pattern with UoW
- **DDD**: Aggregate roots, value objects, domain events
- **Authentication**: Dual mode (Cookie + AccessKey)

### Frontend Features
- **Article Display**: Enhanced readability with syntax highlighting
- **Code Blocks**: Prism.js integration with multiple language support
- **Copy Functionality**: One-click code copying with visual feedback
- **Responsive Design**: Mobile-first approach with dark mode support
- **Performance**: Lazy loading and tree shaking optimization

### Core Entities
- **Article**: Saved content with metadata
- **Tag**: Many-to-many with articles, color-coded
- **Highlight**: Text selections with notes
- **User**: Authentication with API keys

### API Structure
```
GET    /api/articles              # List with pagination/search
POST   /api/articles              # Save from URL
GET    /api/articles/{id}         # Get single article
PUT    /api/articles/{id}         # Update metadata
DELETE /api/articles/{id}         # Delete article

POST   /api/articles/{id}/tags    # Add tag
DELETE /api/articles/{id}/tags    # Remove tag
POST   /api/articles/{id}/highlights  # Add highlight
```

## Development Commands

### Backend
```bash
# Build and run
dotnet build                    # Build solution
dotnet run                      # Start API in development
dotnet ef migrations add <name> # Add migration
dotnet ef database update       # Apply migrations

# Testing
dotnet test                     # Run all tests
dotnet test --logger "console;verbosity=detailed"  # Detailed test output

# Single test project
dotnet test test/backend/MiniCc.Api.Test/MiniCc.Api.Test.csproj
```

### Frontend  
```bash
# Development
pnpm install                   # Install dependencies
pnpm dev                       # Start development server (http://localhost:3000)
pnpm dev:debug                 # Next.js debug mode
pnpm dev:https                 # HTTPS development

# Production
pnpm build                     # Production build
pnpm start                     # Start production server
pnpm lint                      # ESLint check
```

### Web Extension
```bash
pnpm install                   # Install dependencies
pnpm dev                       # Development build with watch
pnpm build                     # Production build
pnpm lint                      # ESLint check
```

### Docker Development
```bash
# Using Podman (aliased as docker)
./docker-build.sh              # Build all service images
./docker-run.sh                # Start full stack with docker-compose

# Manual compose
podman compose -f ./docker-compose.yaml up -d --build
podman compose -f ./docker-compose.yaml down
```

## Environment Setup

### Database Configuration
- **Connection String**: `MiniCC_Db` environment variable
- **Default**: `Host=localhost;Database=mini_cc;Username=postgres;Password=postgres`
- **Docker**: Uses PostgreSQL 16 with Chinese text search (zhparser)

### External Services
- **Readability API**: `MiniCC_ReadabilityApi` (default: http://127.0.0.1:5002)
- **Image Proxy**: Runs on port 5050 with secret key authentication
- **CORS**: Configured for localhost:3000, localhost:5000

### Authentication
- **Cookie Authentication**: 30-minute sessions with sliding expiration
- **API Key Authentication**: Header-based for programmatic access
- **Encryption**: AES encryption for sensitive data

## Clean Architecture Implementation

### Domain Layer
- **Aggregate Roots**: Article, Tag, Highlight, User
- **Value Objects**: Url, Content, TagColor
- **Domain Events**: ArticleEvents, HighlightEvents, TagEvents
- **Domain Services**: IArticleDomainService, IContentExtractionService

### Application Layer
- **Commands**: SaveArticle, DeleteArticle, AddHighlight, etc.
- **Queries**: GetArticleById, GetHighlights, etc.
- **DTOs**: ArticleDto, HighlightDto, TagDto
- **Validation**: FluentValidation integration

### Infrastructure Layer
- **Repositories**: Generic repository with EF Core
- **Entity Configurations**: Fluent API configurations
- **External Services**: Readability API integration

### API Layer
- **Controllers**: RESTful endpoints with attribute routing
- **Authentication**: Dual authentication schemes
- **Documentation**: OpenAPI/Swagger and Scalar UI

## Testing

### Test Structure
```
test/
├── backend/
│   ├── ContentHandler.Test/    # Content extraction tests
│   └── MiniCc.Api.Test/        # API unit/integration tests
```

### Testing Framework
- **xUnit**: Test framework
- **Moq**: Mocking library
- **FluentAssertions**: Assertion library
- **EF Core InMemory**: Database testing

### Running Tests
```bash
# All tests
dotnet test

# Specific project
dotnet test test/backend/MiniCc.Api.Test/

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Docker Services

### Service Architecture
- **db**: PostgreSQL 16 with zhparser for Chinese search
- **readability-api**: Node.js content extraction service
- **minicc-api**: ASP.NET Core backend
- **minicc-web**: Next.js frontend
- **image-proxy**: Image proxy service for secure image loading
- **lb**: Nginx load balancer (port 5000)

### Service Communication
- **Internal**: Services communicate via Docker network
- **External**: All external traffic through nginx (port 5000)
- **Database**: Persistent volume for data storage

## Code Quality Standards

### Backend Standards
- **C#**: Modern C# with nullable reference types
- **Clean Architecture**: Strict separation of concerns
- **Dependency Injection**: Constructor injection pattern
- **Logging**: Serilog with structured logging
- **Validation**: FluentValidation for request validation

### Frontend Standards
- **TypeScript**: Strict type checking
- **React**: Functional components with hooks
- **Styling**: Tailwind CSS with dark mode support
- **Performance**: Code splitting and lazy loading
- **Accessibility**: WCAG compliant components

### Security Practices
- **Input Validation**: All user input validated
- **XSS Protection**: Content sanitization with DOMPurify
- **CORS**: Restricted to trusted origins
- **Authentication**: Secure cookie handling
- **Secrets**: Environment variable configuration

## Performance Considerations

### Database Optimization
- **Full-Text Search**: PostgreSQL tsvector for content search
- **Chinese Support**: zhparser for Chinese text segmentation
- **Indexing**: Proper indexes on frequently queried fields
- **Connection Pooling**: EF Core connection management

### Frontend Optimization
- **Code Splitting**: Next.js automatic code splitting
- **Image Optimization**: Lazy loading and proxy service
- **Caching**: Browser and CDN caching strategies
- **Bundle Analysis**: Regular bundle size monitoring

## Troubleshooting

### Common Issues
- **Database Migrations**: Run `dotnet ef database update` after schema changes
- **Port Conflicts**: Ensure ports 3000, 5000, 5050 are available
- **Docker Issues**: Use `podman` instead of `docker` if configured
- **CORS Problems**: Verify frontend URL in CORS configuration

### Development Tips
- **Hot Reload**: Both backend and frontend support hot reload
- **Debugging**: Use `pnpm dev:debug` for frontend debugging
- **Database**: Use pgAdmin or DBeaver for database management
- **API Testing**: Use Scalar UI at http://localhost:5000/scalar