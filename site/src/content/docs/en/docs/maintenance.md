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
2. Update `deploy/` from the chosen release and keep your local configuration. Set `MINICC_VERSION` in the [installation command](/en/docs/installation/) to the new tag, and keep the same Compose project and database selection. When moving an existing PostgreSQL deployment to GHCR, pass `--database postgres`. If you build locally, run `up -d --build` from the new checkout with your usual base Compose file.

3. Database migrations run automatically on startup. Watch `podman logs -f minicc-api` to follow migration and image-download progress.

If the browser extension has changed, extract the ZIP from the same release into the existing extension directory, then reload it on `chrome://extensions/`.

To keep using `ImageProxy` after an upgrade, use the matching release’s proxy overlay and reuse the signing key and cache volume.

### Upgrading to Store from a release that stored signed image URLs

When you upgrade and select `Store` mode, a background job strips proxy prefixes from previously stored image URLs and queues the originals for download. It logs backfill counts and download summaries.

Hold on to the old `image-proxy-data` volume while migrating. For images that have vanished from their original websites, that volume may contain the only surviving copy.

If you need to roll back, stop the new stack, restore the pre-upgrade database backup, and start the older release with the original proxy cache volume still attached. The older release relies on the signed image URLs saved in that backup.

## Troubleshooting

### The API hangs on a database migration at startup

If a previous migration was terminated abruptly, a stale lock row can remain in `__EFMigrationsLock` and stall startup indefinitely. Stop the API, verify no migration is running, and clear the lock table:

```sql
DELETE FROM "__EFMigrationsLock";
```

### A configuration change has no effect

With Compose, rerun your deployment command after changing environment variables so the API container reads the new values. If you build the web image locally and change `NEXT_PUBLIC_API_URL`, pass `--build` to rebuild it.

### The extension fails to save

Double-check the API URL in the extension settings: it should be the base URL that serves `/api`, without `/api` itself. Also verify that the API key is still listed under **API 密钥** (API Keys) in account settings.
