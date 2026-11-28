---
title: Image storage
description: How MiniCC downloads and persists article images, how to set up local disk or S3-compatible storage such as Cloudflare R2, and how to migrate between them.
---

Once an article is saved, the API downloads its embedded images in the background and saves them on disk or in object storage rather than inside the database. By default, files go into `data/images` (relative to the API content root), which maps to `/app/data/images` in the API data volume under Compose.

Dedicate each image directory or bucket to a single MiniCC database. The cleanup job treats any file not referenced by that database as orphaned and removes it.

## Settings

| Variable | Default | Description |
| --- | --- | --- |
| `Images__Mode` | `Store` | `Store` downloads and persists images via the API; `ImageProxy` switches to the legacy image proxy. |
| `Images__Delivery` | `Authenticated` | `Authenticated` requires sign-in or an API key to view images; `Public` allows anonymous access. |
| `Images__ApiUrl` | `/api/images` | Base URL the API uses when building image links. Set to `http://localhost:5001/api/images` during local development. |
| `Images__PublicBaseUrl` | Empty | Public bucket domain, such as `https://images.example.com`. Requires `Delivery=Public` with S3 storage. |
| `Images__AllowPrivateNetworks` | `false` | Allow downloading images from private, loopback, and link-local addresses |
| `Images__Storage__Provider` | `Local` | Storage backend: `Local` or `S3` |
| `Images__Storage__LocalPath` | `data/images` | Local image directory, relative to the API content root |
| `Images__Storage__S3__ServiceUrl` | Empty | S3 endpoint URL. For Cloudflare R2, use `https://<account_id>.r2.cloudflarestorage.com`. |
| `Images__Storage__S3__Bucket` | Empty | Bucket name |
| `Images__Storage__S3__Region` | `auto` | Region name. Cloudflare R2 requires `auto`. |
| `Images__Storage__S3__AccessKeyId` | Empty | Access key ID (pass via environment variable) |
| `Images__Storage__S3__SecretAccessKey` | Empty | Secret access key (pass via environment variable) |

## Who can see images

| Configuration | Image URL | Access |
| --- | --- | --- |
| `Images__Delivery=Authenticated` (default) | Served via the MiniCC API, e.g. `/api/images/<hash>.png` | Requires sign-in or an API key |
| `Images__Delivery=Public` | Served via the MiniCC API, e.g. `/api/images/<hash>.png` | Accessible to anyone with the link |
| `Images__Delivery=Public` with `Images__PublicBaseUrl` and S3 storage | Your bucket domain, e.g. `https://images.example.com/<hash-prefix>/<hash>.png` | Served directly from the bucket to anyone with the link |

## Local storage

Local storage works out of the box with zero extra configuration. If you prefer to declare it explicitly, add these lines to `minicc-api.environment`:

```yaml
Images__Storage__Provider: Local
Images__Storage__LocalPath: data/images
Images__Delivery: Authenticated
```

## Cloudflare R2

1. Create an R2 bucket in your Cloudflare dashboard.
2. Create an R2 API token with **Object Read & Write** permissions scoped to that bucket. See [R2 S3 API setup](https://developers.cloudflare.com/r2/get-started/s3/) and [R2 API token permissions](https://developers.cloudflare.com/r2/api/tokens/).
3. Export the credentials on your Compose host:

   ```bash
   export R2_ACCESS_KEY_ID="your-r2-access-key-id"
   export R2_SECRET_ACCESS_KEY="your-r2-secret-access-key"
   ```

4. Add the S3 settings to `minicc-api.environment`. Compose passes the exported variables into the container automatically:

   ```yaml
   Images__Storage__Provider: S3
   Images__Storage__S3__ServiceUrl: https://<account_id>.r2.cloudflarestorage.com
   Images__Storage__S3__Bucket: your-bucket
   Images__Storage__S3__Region: auto
   Images__Storage__S3__AccessKeyId: ${R2_ACCESS_KEY_ID:?Set R2_ACCESS_KEY_ID}
   Images__Storage__S3__SecretAccessKey: ${R2_SECRET_ACCESS_KEY:?Set R2_SECRET_ACCESS_KEY}
   Images__Delivery: Authenticated
   ```

To serve images directly from R2, enable an `r2.dev` subdomain for testing or attach a custom domain in your bucket settings (see [R2 public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/)), then set `Images__Delivery=Public` and `Images__PublicBaseUrl=https://images.example.com`. Keep in mind that anyone with the image URL will be able to load it directly.

If you want public image links without exposing the bucket itself, set `Images__Delivery: Public` and leave `Images__PublicBaseUrl` empty so the MiniCC API continues serving images on your behalf.

## Download rules

- Every 30 seconds, the worker picks up images from at most 10 articles, running up to 4 concurrent downloads globally and 2 per host.
- Each download has a 30-second timeout from the moment it acquires a slot until the response body finishes reading, following up to 5 redirects and capping files at 20 MB.
- Accepted formats are JPEG, PNG, GIF, WebP, AVIF, and SVG, detected from file contents rather than extensions.
- Transient failures retry after 1 minute, 10 minutes, 1 hour, and 6 hours, then every 24 hours; after 10 failures the image is marked as failed.
- Unreferenced local files or S3 objects are held for a 24-hour grace period before cleanup removes them.
- When upgrading from an older release, existing articles are backfilled in batches before image downloads begin.

## Network requirements

The image downloader needs direct outbound HTTP(S) connectivity. Because it connects directly to the IP address it validated, it bypasses system proxies as well as `HTTP_PROXY` and `HTTPS_PROXY`. If your server can only reach the internet through an outbound proxy, downloads in `Store` mode will fail.

By default, the downloader blocks loopback, private, and link-local addresses. Only enable `Images__AllowPrivateNetworks=true` if you genuinely need to clip pages from internal sites; with it enabled, saving a page containing an internal image URL can cause the API server to send requests to services on your private network.

## Move between local storage and S3

1. Stop the API (or pause image processing) and take a backup first.
2. Copy the entire image directory tree, preserving the two-character hash subdirectories. Using [rclone](https://rclone.org/s3/), do a dry run before syncing:

   ```bash
   rclone sync --dry-run /path/to/images r2:your-bucket
   rclone sync /path/to/images r2:your-bucket
   ```

3. Once the copy finishes, update your storage settings to point to the new backend.

To move back from R2 to local disk, simply swap the source and destination.

## Legacy image proxy

The legacy proxy requires `Images__Mode=ImageProxy` and `ImageProxy__SignatureKey` on the API. Under Compose, export `IMAGE_PROXY_SECRET` and include `deploy/docker-compose.image-proxy.yaml` when starting the stack. That overlay sets both variables and wires up the proxy container and its nginx route:

```bash
export IMAGE_PROXY_SECRET="your-long-random-secret"
podman compose -f deploy/docker-compose.yaml -f deploy/docker-compose.image-proxy.yaml up -d --build
```

For SQLite deployments, replace the first file with `deploy/docker-compose.sqlite.yaml`.
