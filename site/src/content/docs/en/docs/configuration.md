---
title: Configuration reference
description: Configuration files, environment variables, and settings for the MiniCC API, database, library, authentication, and web app.
---

## How configuration works

The API loads its defaults from `src/server/appsettings.yaml`, where every option is documented in both Chinese and English. In development, it also reads `appsettings.Development.yaml`.

In production, overriding values with environment variables is usually the cleanest approach. Join nested YAML keys with double underscores: `Library:TrashRetentionDays` maps to `Library__TrashRetentionDays`. With Compose, add them under `minicc-api`'s `environment` block:

```yaml
minicc-api:
  environment:
    Library__TrashRetentionDays: 60
    Library__UndoWindowSeconds: 15
```

Then run `podman compose -f deploy/docker-compose.yaml up -d` to recreate the API container with the updated settings.

## Environment variables

| Variable | Default | Description |
| --- | --- | --- |
| `Database__Provider` | `postgres` | Database engine: `postgres` or `sqlite` (case-insensitive) |
| `MiniCC_Db` | See below | Database connection string. Relative SQLite paths resolve against the API content root. |
| `Library__TrashRetentionDays` | `30` | Days to retain trashed articles (1–365) |
| `Library__UndoWindowSeconds` | `10` | Seconds the undo toast stays visible after deletion (3–60) |
| `DataProtection__KeysPath` | Empty | Directory storing the keys that encrypt sign-in cookies |
| `Images__*` | | Image downloads, access control, and storage. See [Image storage](/en/docs/images/). |
| `ImageProxy__SignatureKey` | Empty | Signing key required in `ImageProxy` mode |

## Database

`Database:Provider` chooses which database engine to use. The connection string is resolved in order:

1. The `MiniCC_Db` environment variable
2. `ConnectionStrings:DefaultConnection` (PostgreSQL) or `ConnectionStrings:Sqlite` (SQLite)

Defaults:

| Database | Default connection string |
| --- | --- |
| PostgreSQL | `Host=127.0.0.1;Port=15432;Database=mini_cc;Username=postgres;Password=postgres` |
| SQLite | `Data Source=data/mini_cc.db` |

The default SQLite path is relative to the API content root, and the API creates the parent directory automatically if needed. Both Compose files preconfigure `MiniCC_Db`, so you rarely need to touch it.

Database migrations run automatically on every API startup.

## Library

| Setting | Default | Range | Effect |
| --- | --- | --- | --- |
| `Library:TrashRetentionDays` | 30 | 1–365 | Days an article remains in the trash before background cleanup purges it permanently |
| `Library:UndoWindowSeconds` | 10 | 3–60 | Duration the undo button remains visible after deleting articles |

Both values are validated at startup; the API refuses to start if either is out of range.

## Sign-in and security

`DataProtection:KeysPath` sets where the API stores encryption keys for sign-in cookies. Both Compose files point it to `/app/data/data-protection-keys` inside the API data volume. If you deploy outside Compose, point it to a persistent directory; otherwise the keys are lost on restart and every user is signed out.

`AllowedHosts` restricts which hostnames are accepted in the incoming `Host` header. Separate multiple hostnames with semicolons and omit port numbers. The default `*` allows any host.

`AesKeys` stores the AES keys used to encrypt API keys in the database. Each entry has an `Id`, `Key`, and `IV` (the latter two Base64-encoded). Existing API keys can only be decrypted by the entry that encrypted them, so never delete or alter an entry that is still in use.

## Web app

The web app has only one setting: `NEXT_PUBLIC_API_URL`, which points to the API. Because Next.js bakes it in at build time, you must rebuild whenever you change it:

- **GHCR images** use the same-origin `/api`. For a local Compose build, set `minicc-web` → `build.args` (defaults to `http://localhost:5000/api`).
- **From source**: set it in `src/web/.env.local` (defaults to `http://localhost:5001/api`).

## Logs

The API logs through Serilog to both the console and hourly rolling files under `logs/` in its working directory, with errors written to a separate file. With Compose, run `podman logs minicc-api` to inspect console output.
