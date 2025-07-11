# MiniCC (Mini Cut Collection) - Read Later Application

Omnivore minimum implementation, built with ASP.NET Core and Next.js.

## Features

- **Save Articles**: Save web pages from URLs with automatic content extraction
- **Search**: Full-text search across saved articles
- **Tagging**: Organize articles with custom tags and colors
- **Highlighting**: Add highlights and notes to article content
- **Favorites & Archive**: Mark articles as favorites or archive them
- **Responsive UI**: Modern, responsive interface built with React and SCSS

## Tech Stack

### Backend
- **ASP.NET Core 9.0** - Web API framework
- **Entity Framework Core** - ORM for database operations
- **PostgreSQL** - Primary database
- **HtmlAgilityPack** - Web scraping and content extraction

### Frontend
- **Next.js 15** - React framework with App Router
- **TypeScript** - Type-safe JavaScript
- **SCSS** - CSS preprocessor for styling
- **Axios** - HTTP client for API requests

## Project Structure

```
src/
├── backend/
│   └── MiniCc.Api/
│       ├── Controllers/     # API controllers
│       ├── Data/           # Entity Framework context
│       ├── Models/         # Domain models
│       ├── Services/       # Business logic services
│       └── Migrations/     # Database migrations
├── frontend/
│   ├── src/
│   │   ├── app/           # Next.js app directory
│   │   ├── components/    # React components
│   │   ├── lib/           # Utilities and API client
│   │   └── types/         # TypeScript type definitions
│   └── public/            # Static assets
│
└── web-extension/
    └── src/               # source file
```

## Getting Started

### Prerequisites
- .NET 9.0 SDK
- Node.js 18+ and pnpm (install pnpm: `npm install -g pnpm`)
- PostgreSQL database 

### Backend Setup

1. Navigate to the backend directory:
   ```bash
   cd backend/MiniCc.Api
   ```

2. Install Entity Framework tools (if not already installed):
   ```bash
   dotnet tool install --global dotnet-ef
   ```

3. Update the connection string in `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Database=ome_reader;Username=your_username;Password=your_password"
     }
   }
   ```

4. Create and run database migrations:
   ```bash
   dotnet ef database update
   ```

5. Run the backend API:
   ```bash
   dotnet run
   ```

The API will be available at `http://localhost:5000`.

### Frontend Setup

1. Navigate to the frontend directory:
   ```bash
   cd frontend
   ```

2. Install dependencies:
   ```bash
   pnpm install
   ```

3. Update the API URL in `.env.local` if needed:
   ```
   NEXT_PUBLIC_API_URL=http://localhost:5000/api
   ```

4. Run the development server:
   ```bash
   pnpm dev
   ```

The frontend will be available at `http://localhost:3000`.

### Web Extension

1. Navigate to the web-extension directory:

   ```bash
   cd web-extension
   ```

2. Install dependencies:

   ```bash
   pnpm install
   ```

3. Run the development server:

   ```bash
   pnpm dev
   ```

The web extension will be available at Chrome web extension.

## API Endpoints

### Articles
- `GET /api/articles` - Get all articles (with pagination and search)
- `GET /api/articles/{id}` - Get specific article
- `POST /api/articles` - Save new article from URL
- `PUT /api/articles/{id}` - Update article
- `DELETE /api/articles/{id}` - Delete article
- `POST /api/articles/{id}/favorite` - Toggle favorite status
- `POST /api/articles/{id}/archive` - Toggle archive status

### Tags
- `POST /api/articles/{id}/tags` - Add tag to article
- `DELETE /api/articles/{id}/tags/{tagId}` - Remove tag from article

### Highlights
- `POST /api/articles/{id}/highlights` - Add highlight to article
- `DELETE /api/highlights/{id}` - Delete highlight

## Development

### Backend Commands
```bash
# Run the API in development mode
dotnet run

# Run tests (if any)
dotnet test

# Create new migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update
```

### Frontend Commands
```bash
# Install all dependencies
pnpm install:all

# Development server
pnpm dev

# Production build
pnpm build

# Start production server
pnpm start

# Linting
pnpm lint
```

### Web Extension Commands

```bash
# Install all dependencies
pnpm install:all

# Development server
pnpm dev

# Production build
pnpm build

# Linting
pnpm lint
```



## Database Schema

The application uses three main entities:

- **Articles**: Store saved web content with metadata
- **Tags**: Organize articles with custom labels and colors
- **Highlights**: Text selections and notes within articles

All entities include created/updated timestamps and proper relationships.

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Run tests and ensure builds pass
5. Submit a pull request

## License

This project is licensed under the MIT License.
