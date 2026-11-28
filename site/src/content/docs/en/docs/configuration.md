---
title: Configuration reference
description: The MiniCC API's configuration file and environment variables, plus database, library, sign-in and web app settings.
---

## How configuration works

The API reads its defaults from `src/server/appsettings.yaml`, where every setting has a comment in Chinese and English. The development environment also loads `appsettings.Development.yaml`.

Deployments usually override settings with environment variables. Join the levels with two underscores: `Library:TrashRetentionDays` in YAML becomes the environment variable `Library__TrashRetentionDays`. With Compose, add variables to the `environment` section of `minicc-api`:

```yaml
minicc-api:
  environment:
    Library__TrashRetentionDays: 60
    Library__UndoWindowSeconds: 15
```

Then run `podman compose -f deploy/docker-compose.yaml up -d` so the API container restarts with the new settings.

## Environment variables

| Variable | Default | Description |
| --- | --- | --- |
| `Database__Provider` | `postgres` | `postgres` or `sqlite`, case-insensitive |
| `MiniCC_Db` | See below | Database connection string. Relative SQLite paths resolve from the API content root. |
| `Library__TrashRetentionDays` | `30` | Days to keep articles in the trash (1–365) |
| `Library__UndoWindowSeconds` | `10` | Seconds the undo button stays visible after a deletion (3–60) |
| `DataProtection__KeysPath` | Empty | Directory for the keys that encrypt sign-in cookies |
| `Images__*` | | Image downloads, access and storage. See [Image storage](/en/docs/images/). |
| `ImageProxy__SignatureKey` | Empty | Signing key for the legacy image proxy, needed only in `ImageProxy` mode |

## Database

`Database:Provider` selects the database. The connection string is read in this order:

1. The `MiniCC_Db` environment variable
2. `ConnectionStrings:DefaultConnection` for PostgreSQL, or `ConnectionStrings:Sqlite` for SQLite

Defaults:

| Database | Default connection string |
| --- | --- |
| PostgreSQL | `Host=127.0.0.1;Port=15432;Database=mini_cc;Username=postgres;Password=postgres` |
| SQLite | `Data Source=data/mini_cc.db` |

The default SQLite path is relative to the API content root, and the API creates its directory on startup. Both Compose files already set `MiniCC_Db`, so you usually don't need to change it.

The API applies database migrations every time it starts.

## Library

| Setting | Default | Range | Effect |
| --- | --- | --- | --- |
| `Library:TrashRetentionDays` | 30 | 1–365 | Days an article stays in the trash before a background job deletes it permanently |
| `Library:UndoWindowSeconds` | 10 | 3–60 | How long the undo button stays visible after you delete articles |

The API checks both settings at startup and does not start when either is out of range.

## Sign-in and security

`DataProtection:KeysPath` is the directory for the keys that encrypt sign-in cookies. Both Compose files set it to `/app/data/data-protection-keys` in the API data volume. For other deployments, set it to a persistent directory. Otherwise the keys are lost when the API restarts, and everyone has to sign in again.

`AllowedHosts` limits the host names accepted in the request `Host` header. Separate names with semicolons and leave out ports. The default `*` allows any host.

`AesKeys` holds the AES keys that encrypt API keys in the database. Each entry has an `Id`, a `Key` and an `IV`; the last two are Base64 encoded. Stored API keys can only be decrypted with the entry that encrypted them, so don't remove or change an entry that is in use.

## Web app

The web app has one setting: `NEXT_PUBLIC_API_URL`, the address of the API. It is built into the pages, so you must rebuild after changing it.

- Compose: change the value under `minicc-web` → `build.args`. The default is `http://localhost:5000/api`.
- From source: set it in `src/web/.env.local`. The default is `http://localhost:5001/api`.

## Logs

The API logs with Serilog. It writes to the console and to hourly files under `logs/` in its working directory, with errors also written to a separate file. With Compose, `podman logs minicc-api` shows the console log.
