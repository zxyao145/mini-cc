---
title: Backup and upgrades
description: Back up your MiniCC database and images, upgrade cleanly to new releases, and troubleshoot common issues.
---

## Backup

Always back up your database and local images together. If you restore the database without its matching image files, MiniCC will try to re-download them from the original sites, where older images may no longer exist.

- **PostgreSQL**: back up the `pgdata` volume, or export a SQL dump:

  ```bash
  podman exec db pg_dump -U postgres mini_cc > mini_cc.sql
  ```

- **SQLite**: stop the API first, then copy the database file along with any lingering WAL files. If the SQLite CLI is installed on the host, you can also take a live backup without stopping the API:

  ```bash
  sqlite3 /path/to/mini_cc.db ".backup /path/to/backup.db"
  ```

- **Local images**: under Compose, images live in `/app/data/images` inside `api-data` (PostgreSQL) or `sqlite-data` (SQLite). If you use Cloudflare R2, rely on R2's own retention or replication policies.

## Upgrade

1. Back up both the database and your local image directory. For PostgreSQL, dump the database and back up `api-data`; for SQLite, stop the API and back up `sqlite-data`.
2. Pull the latest code and rebuild with your usual Compose command:

   ```bash
   git pull
   podman compose -f deploy/docker-compose.yaml up -d --build
   ```

3. Database migrations run automatically on startup. Watch `podman logs -f minicc-api` to follow migration and image-download progress.

The browser extension may also have updates. Run `pnpm build:ext` from the repository root and reload the extension on `chrome://extensions/`.

### Upgrading from a release that used the image proxy

Newer releases download and store images directly in the API by default. On first startup, a background job strips old proxy URL prefixes and queues images for download, logging backfill counts and download summaries as it goes.

Hold on to the old `image-proxy-data` volume while migrating. For images that have vanished from their original websites, that volume may contain the only surviving copy.

If you need to roll back, stop the new stack, restore the pre-upgrade database backup, and start the older release with the original proxy cache volume still attached. The older release relies on the signed image URLs saved in that backup.

## Troubleshooting

### The API hangs on a database migration at startup

If a previous migration was terminated abruptly, a stale lock row can remain in `__EFMigrationsLock` and stall startup indefinitely. Stop the API, verify no migration is running, and clear the lock table:

```sql
DELETE FROM "__EFMigrationsLock";
```

### A configuration change has no effect

With Compose, run `up -d` after changing environment variables so the API container is recreated with the new values. `NEXT_PUBLIC_API_URL` is a build-time argument, so pass `--build` to rebuild the web image.

### The extension fails to save

Double-check the API URL in the extension settings: it should be the base URL that serves `/api`, without `/api` itself. Also verify that the API key is still listed under **API 密钥** (API Keys) in account settings.
