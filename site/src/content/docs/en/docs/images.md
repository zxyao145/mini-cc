---
title: Image storage
description: How MiniCC downloads and stores article images, how to configure local storage or S3-compatible storage such as Cloudflare R2, and how to move between them.
---

After an article is saved, the API downloads its images in the background and stores them outside the database. The default is a local directory, `data/images`, relative to the API content root. In Compose this is `/app/data/images` in the API data volume.

Use a directory or bucket for one MiniCC database only. Cleanup treats images that the database does not reference as unused and deletes them.

## Settings

| Variable | Default | Description |
| --- | --- | --- |
| `Images__Mode` | `Store` | `Store` downloads and keeps images in the API. `ImageProxy` uses the legacy image proxy. |
| `Images__Delivery` | `Authenticated` | `Authenticated` requires sign-in or an API key to view images. `Public` allows anonymous access. |
| `Images__ApiUrl` | `/api/images` | Address the API uses to build image links. Set it to `http://localhost:5001/api/images` for local development. |
| `Images__PublicBaseUrl` | Empty | Public bucket domain, for example `https://images.example.com`. Requires `Delivery=Public` and S3 storage. |
| `Images__AllowPrivateNetworks` | `false` | Allows downloads from private, loopback and link-local addresses |
| `Images__Storage__Provider` | `Local` | `Local` or `S3` |
| `Images__Storage__LocalPath` | `data/images` | Local image directory, relative to the API content root |
| `Images__Storage__S3__ServiceUrl` | Empty | S3 endpoint. For R2, use `https://<account_id>.r2.cloudflarestorage.com`. |
| `Images__Storage__S3__Bucket` | Empty | Bucket name |
| `Images__Storage__S3__Region` | `auto` | Region. R2 requires `auto`. |
| `Images__Storage__S3__AccessKeyId` | Empty | Access key ID. Provide it through the environment. |
| `Images__Storage__S3__SecretAccessKey` | Empty | Secret access key. Provide it through the environment. |

## Who can see images

| Configuration | Image URL | Access |
| --- | --- | --- |
| `Images__Delivery=Authenticated` (default) | MiniCC API, such as `/api/images/<hash>.png` | Requires sign-in or an API key |
| `Images__Delivery=Public` | MiniCC API, such as `/api/images/<hash>.png` | Anyone with the URL |
| `Images__Delivery=Public` with `Images__PublicBaseUrl` and S3 storage | Your bucket domain, such as `https://images.example.com/<hash-prefix>/<hash>.png` | Anyone with the URL, served directly from the bucket |

## Local storage

Local storage is the default and needs no changes. To set it explicitly, add this to `minicc-api.environment`:

```yaml
Images__Storage__Provider: Local
Images__Storage__LocalPath: data/images
Images__Delivery: Authenticated
```

## Cloudflare R2

1. Create an R2 bucket in the Cloudflare dashboard.
2. Create an R2 API token with **Object Read & Write** permission, scoped to that bucket. See [R2 S3 API setup](https://developers.cloudflare.com/r2/get-started/s3/) and [R2 API token permissions](https://developers.cloudflare.com/r2/api/tokens/).
3. Export the credentials on the host that runs Compose:

   ```bash
   export R2_ACCESS_KEY_ID="your-r2-access-key-id"
   export R2_SECRET_ACCESS_KEY="your-r2-secret-access-key"
   ```

4. Add the storage settings to `minicc-api.environment`. Compose substitutes the exported variables into the container.

   ```yaml
   Images__Storage__Provider: S3
   Images__Storage__S3__ServiceUrl: https://<account_id>.r2.cloudflarestorage.com
   Images__Storage__S3__Bucket: your-bucket
   Images__Storage__S3__Region: auto
   Images__Storage__S3__AccessKeyId: ${R2_ACCESS_KEY_ID:?Set R2_ACCESS_KEY_ID}
   Images__Storage__S3__SecretAccessKey: ${R2_SECRET_ACCESS_KEY:?Set R2_SECRET_ACCESS_KEY}
   Images__Delivery: Authenticated
   ```

To serve images straight from R2, enable an `r2.dev` address in the bucket settings for testing, or attach a dedicated image domain. See [R2 public buckets](https://developers.cloudflare.com/r2/buckets/public-buckets/). Then set `Images__Delivery=Public` and `Images__PublicBaseUrl=https://images.example.com`. Anyone who knows an image URL can then load it.

To make images public without opening the bucket, set `Images__Delivery: Public` and leave `Images__PublicBaseUrl` empty. The MiniCC API keeps serving the images.

## Download rules

- Every 30 seconds, the API processes images for up to 10 articles. It runs at most 4 downloads at once overall and 2 per host.
- Each image has 30 seconds, counted from when it gets a download slot and including reading the response body. Downloads follow at most 5 redirects and accept files up to 20 MB.
- JPEG, PNG, GIF, WebP, AVIF and SVG are accepted. The format is detected from the file content.
- Transient errors retry after 1 minute, 10 minutes, 1 hour, 6 hours, then every 24 hours. After the tenth failure the image is marked failed.
- Unreferenced local files or S3 objects are cleaned up only after a 24-hour grace period.
- After an upgrade from an older version, existing articles are backfilled in batches before downloads begin.

## Network requirements

Image downloads need direct outbound HTTP(S) access. To connect to the IP address it validated, the downloader bypasses system proxies and environment proxies such as `HTTP_PROXY` and `HTTPS_PROXY`. On a server that reaches the web only through a proxy, image downloads in `Store` mode fail.

By default the downloader blocks loopback, private and link-local addresses. Set `Images__AllowPrivateNetworks=true` only if you need to save articles from sites on your private network. With it enabled, saving a page that contains a private image URL can make the API server connect to internal services.

## Move between local storage and S3

1. Stop the API, or at least image processing, and take a backup.
2. Copy the whole image tree. Object keys include a two-character hash directory, which must be kept. This example uses [rclone](https://rclone.org/s3/): preview first, then synchronize.

   ```bash
   rclone sync --dry-run /path/to/images r2:your-bucket
   rclone sync /path/to/images r2:your-bucket
   ```

3. After the copy completes, switch the storage settings to the destination.

To move from R2 back to local storage, swap the source and destination.

## Legacy image proxy

The API needs `Images__Mode=ImageProxy` and the signing key `ImageProxy__SignatureKey`. With Compose, export `IMAGE_PROXY_SECRET` and add `deploy/docker-compose.image-proxy.yaml` when you start the stack. That file sets both values and adds the proxy service and its nginx route:

```bash
export IMAGE_PROXY_SECRET="your-long-random-secret"
podman compose -f deploy/docker-compose.yaml -f deploy/docker-compose.image-proxy.yaml up -d --build
```

For SQLite, use `deploy/docker-compose.sqlite.yaml` as the first file.
