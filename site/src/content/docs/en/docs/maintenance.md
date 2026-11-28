---
title: Backup and upgrades
description: Back up MiniCC's database and images, upgrade to a new version, and recover from a stuck database migration.
---

## Backup

Back up the database and the local images together. If you restore the database without its images, stored images are downloaded again from their original sites, where some may no longer exist.

**PostgreSQL**: back up the `pgdata` volume, or dump the database:

```bash
podman exec db pg_dump -U postgres mini_cc > mini_cc.sql
```

**SQLite**: stop the API, then copy the database file together with any remaining WAL files. On a host with the SQLite CLI you can also back up while the API runs:

```bash
sqlite3 /path/to/mini_cc.db ".backup /path/to/backup.db"
```

**Local images**: in Compose they are in `/app/data/images`, inside the `api-data` volume for PostgreSQL or the `sqlite-data` volume for SQLite. With R2, image files stay in the bucket and are covered by your R2 retention or replication setup.

## Upgrade

1. Back up the database and the local image directory. For PostgreSQL, dump the database and back up `api-data`. For SQLite, stop the API and back up `sqlite-data`.
2. Pull the new version and rebuild with the same Compose command you already use:

   ```bash
   git pull
   podman compose -f deploy/docker-compose.yaml up -d --build
   ```

3. The API applies migrations on startup. Follow migrations and image downloads in the API log with `podman logs -f minicc-api`.

The browser extension may have changed too. Run `pnpm build:ext` from the repository root and reload the extension on `chrome://extensions/`.

### Upgrading from a version that used the image proxy

New versions download and store images in the API by default. On first start, a background job removes the old proxy prefixes and then downloads the images. The log shows the backfill counts and download summaries.

Keep the old `image-proxy-data` volume during the migration. It may hold the only remaining copies of images that have been removed from their original sites.

To roll back, stop the new version, restore the database backup taken before the upgrade, and start the older version with the saved proxy cache volume. The older version relies on the signed image URLs stored in that backup.

## Troubleshooting

### The API hangs on a database migration at startup

If a migration was killed, the API can wait on `__EFMigrationsLock` forever. Stop the API, make sure no migration is running, and clear the table:

```sql
DELETE FROM "__EFMigrationsLock";
```

### A configuration change has no effect

With Compose, run `up -d` again after you change environment variables so the API container is recreated. `NEXT_PUBLIC_API_URL` is a build argument, so add `--build` to rebuild the web image.

### The extension fails to save

Check the API URL in the extension settings. It is the address that serves `/api`, without `/api` itself. Then make sure the API key still appears in the **API 密钥** (API Keys) list in account settings.
