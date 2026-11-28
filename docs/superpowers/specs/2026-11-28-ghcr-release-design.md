# MiniCC 的 GitHub Release 镜像发布设计

GitHub Release 发布后，GitHub Actions 从该 Release 指向的提交构建 API、Web 和 Nginx，将通过验证的镜像推送到 GitHub Container Registry（GHCR）。正式版等三个镜像推送并校验通过后部署官网，使用本次 Release 的标签生成中英文安装命令。自部署使用同一个标签拉取三个镜像，默认使用 SQLite，可通过 `--database postgres` 选择 PostgreSQL。

## 镜像与版本

| Compose 服务 | Dockerfile | 构建上下文 | `v1.0.1` 对应的镜像 |
| --- | --- | --- | --- |
| `minicc-api` | `src/server/Dockerfile` | 仓库根目录 | `ghcr.io/zxyao145/minicc-api:v1.0.1` |
| `minicc-web` | `src/web/Dockerfile` | 仓库根目录 | `ghcr.io/zxyao145/minicc-web:v1.0.1` |
| `lb` | `src/ng/Dockerfile` | `src/ng` | `ghcr.io/zxyao145/minicc-lb:v1.0.1` |

首版发布 `linux/amd64`。SQLite 运行在 API 容器内，PostgreSQL 部署直接拉取 `zhparser/zhparser:bookworm-16`，该上游标签目前仅提供 `linux/amd64`。历史图片代理由可选 Compose 扩展提供。[zhparser 标签信息](https://hub.docker.com/v2/repositories/zhparser/zhparser/tags/bookworm-16)

正式版使用 `v1.0.1` 这样的标签，预发行版使用 `v1.0.1-rc.1` 这样的标签。流水线校验标签格式及 Docker tag 长度，直接使用合法的 Release 标签作为镜像标签。每个镜像记录 `org.opencontainers.image.source`、`org.opencontainers.image.revision` 和 `org.opencontainers.image.version`，便于从镜像追溯仓库、提交和版本。[GHCR 镜像标签与元数据](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry#labelling-container-images)

## 发布工作流

在现有 `.github/workflows/deploy-site.yml` 中编排镜像构建、镜像发布、扩展打包和官网部署。工作流只监听 `release` 的 `published` 事件，取代原来的 `push v*` 和 `workflow_dispatch` 部署入口。正式版和预发行版发布后都会启动镜像构建和扩展打包。镜像任务检出 `github.sha`，从 `github.event.release.tag_name` 取得版本。同一标签的推送任务串行运行，进行中的推送继续完成。[Release 事件](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#release) · [工作流并发控制](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency)

### 构建和验证

1. 使用 `contents: read` 检出 Release 提交。在 Ubuntu amd64 runner 安装仓库指定的 .NET、Node.js 和 pnpm 版本，运行 `dotnet test --configuration Release` 与 `pnpm install --frozen-lockfile`、`pnpm test`。
2. 用 Docker Buildx 构建并加载三个带目标版本标签的镜像。构建 Web 时传入 `NEXT_PUBLIC_API_URL=/api`。Next.js 会把该值写入客户端代码，现有 Nginx 会把 `/api/` 转发到 `minicc-api:8080`。因此同一个 Web 镜像可以服务本机、局域网地址和自定义域名。[Next.js 环境变量](https://nextjs.org/docs/app/guides/environment-variables#bundling-environment-variables-for-the-browser)
3. 使用 GHCR Compose 覆盖文件和已经加载的本地镜像，通过 `compose up -d --no-build` 依次启动两套数据库配置。用临时卷验证数据库初始化、页面加载和登录。至少用 `http://127.0.0.1:5000` 验证浏览器发往 `/api/` 的同源请求。测试完整结束后，用 `docker save` 保存三个镜像，作为同一次工作流的 Actions artifact 保留 7 天。构建任务将产物名称传给推送任务。[Actions 产物](https://docs.github.com/en/actions/tutorials/store-and-share-data)

### 推送和恢复

`publish-images` 任务依赖镜像构建任务成功，下载归档后用 `docker load` 加载已验证的镜像。通过内置 `GITHUB_TOKEN` 登录 `ghcr.io`，在该任务授予 `packages: write`。已有同名 GHCR 包时，确认包的 `Manage Actions access` 允许这个仓库写入。[GitHub 推送镜像](https://docs.github.com/en/actions/tutorials/publish-packages/publish-docker-images) · [GHCR 包权限](https://docs.github.com/en/packages/learn-github-packages/configuring-a-packages-access-control-and-visibility#ensuring-workflow-access-to-your-package)

三个镜像分别推送。`publish-images` 读取 GHCR 上的三个标签，核对各自的 manifest digest 与推送记录。全部通过后该任务才成功，并在 Actions 摘要列出镜像地址、digest 和提交 SHA。推送中断可能留下部分镜像，通过 **Re-run failed jobs** 复用同一份构建产物并重试推送。归档过期后使用新 Release 标签重新发布整套镜像。[重跑失败任务](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/re-run-workflows-and-jobs)

从 GitHub 页面或维护者身份的 `gh release create` 发布。若以后改由另一个工作流创建 Release，应使用能触发下游工作流的凭证，例如 GitHub App 安装令牌。内置 `GITHUB_TOKEN` 产生的 `release` 事件会被 GitHub 抑制。[工作流触发限制](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow#triggering-a-workflow-from-a-workflow)

### 官网部署和教程版本

`deploy` 声明 `needs: [publish-images, publish-extension]`，以 `success() && !github.event.release.prerelease` 为运行条件。正式 Release 的三个镜像都推送并校验通过、扩展 ZIP 也打包成功后，`deploy` 才构建并发布官网。镜像发布失败会跳过官网任务。修复后重跑同一次 Release 的失败任务，依赖满足后继续部署。预发行版的流程在镜像和扩展发布完成后结束。[GitHub Actions 任务依赖](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idneeds)

官网 `pnpm build` 将本次 `github.event.release.tag_name` 作为 `PUBLIC_MINICC_RELEASE_TAG` 传给 Astro。`site/src/content/docs/docs/installation.mdx` 和 `site/src/content/docs/en/docs/installation.mdx` 共用安装命令组件，以该值渲染 `MINICC_VERSION`。例如正式 Release `v1.0.1` 的两页都生成 `MINICC_VERSION=v1.0.1`，涵盖默认 SQLite 和显式 PostgreSQL 的命令。官网发布任务直接使用事件标签，缺少标签时终止构建。本地预览可使用 `vX.Y.Z` 占位符。[Astro 构建时环境变量](https://docs.astro.build/en/guides/environment-variables/)

## GHCR 部署

新增 `deploy/docker-compose.ghcr.yaml`，作为 SQLite 和 PostgreSQL 基础 Compose 共用的覆盖文件。它设置三个应用服务的 `image`，用必填的 `MINICC_VERSION` 统一指定版本。[Compose 文件合并](https://docs.docker.com/compose/how-tos/multiple-compose-files/merge/) · [变量插值](https://docs.docker.com/reference/compose-file/interpolation/)

```yaml
services:
  minicc-api:
    image: "ghcr.io/zxyao145/minicc-api:${MINICC_VERSION:?Set MINICC_VERSION to the Release tag}"
  minicc-web:
    image: "ghcr.io/zxyao145/minicc-web:${MINICC_VERSION:?Set MINICC_VERSION to the Release tag}"
  lb:
    image: "ghcr.io/zxyao145/minicc-lb:${MINICC_VERSION:?Set MINICC_VERSION to the Release tag}"
```

新增 `deploy/docker-run.ghcr.sh` 作为部署入口。它用 `--database` 选择基础文件，再把 `deploy/docker-compose.ghcr.yaml` 作为第二个 `-f` 参数传给 Compose。

| `--database` | 基础 Compose | 持久化与启动行为 |
| --- | --- | --- |
| 缺省或 `sqlite` | `deploy/docker-compose.sqlite.yaml` | API 使用 `/app/data/mini_cc.db`，由 `sqlite-data` 卷持久化。Compose 启动 API、Web、Nginx 三个服务。 |
| `postgres` | `deploy/docker-compose.yaml` | Compose 拉取并启动 `db`，等待它健康后启动 API。`pgdata` 保存数据库，`api-data` 保存 API 本地数据。 |

脚本先校验参数并定位仓库的 `deploy` 目录，再按现有脚本的顺序选择 Docker 或 Podman。`--database` 缺少值或值超出允许范围时，返回非零退出码并提示 `sqlite`、`postgres` 两个合法值。`MINICC_VERSION` 缺失或为空时，直接报错。选定配置后运行 `compose pull`，拉取成功再运行 `compose up -d --no-build`。Podman 使用支持这些命令和参数的 Compose 提供程序。[Compose 启动参数](https://docs.docker.com/reference/cli/docker/compose/up/) · [Podman Compose](https://docs.podman.io/en/latest/markdown/podman-compose.1.html)

实现后，从含有部署文件的仓库根目录运行以下命令。

```sh
# 默认 SQLite
MINICC_VERSION=v1.0.1 bash deploy/docker-run.ghcr.sh

# 显式选择 PostgreSQL
MINICC_VERSION=v1.0.1 bash deploy/docker-run.ghcr.sh --database postgres
```

同一种数据库升级时使用原数据库参数，更新 `MINICC_VERSION` 即可继续使用现有数据卷。已有 PostgreSQL 部署迁入 GHCR 安装入口时应显式传 `--database postgres`。切换数据库前用原配置执行 `compose down` 并保留卷，再用新参数启动。需要保留原库业务数据时，先安排跨库迁移。PostgreSQL 基础配置会映射宿主机 `15432`，开放部署前按安装文档配置数据库端口和凭证。

## 实施与验收

1. 增加 GHCR Compose 覆盖文件和部署入口。用 `compose config` 校验两个参数分支的服务列表、数据库配置、数据卷与三个应用镜像的统一标签。缺省和 `--database sqlite` 生成相同配置，缺少版本和非法数据库参数在拉取前退出。
2. 在发布工作流中增加镜像构建、推送和冒烟检查。两个数据库分支都能完成初始化并登录，Web 请求与部署地址同源。模拟正式版其中一个镜像推送失败，确认 `deploy` 被跳过。重跑失败任务后确认复用原构建产物，三个 digest 和扩展包验证通过后才开始官网部署。
3. 用包含这些文件的提交发布 `v1.0.1-rc.1` 之类的预发行版。确认推送 Git tag 和保存草稿均未启动发布工作流，发布预发行版后镜像和扩展任务完成，官网部署被跳过。检查 Actions 摘要里的提交 SHA 和三个 digest，再从一台干净的 amd64 主机拉取并依次启动两套部署。如果面向公开自部署，首推后将三个 GHCR 包设为 Public，再验证匿名拉取。新 GHCR 包默认 Private。[GHCR 初始可见性](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry#pushing-container-images)
4. 发布正式 Release，核对 `deploy` 的开始时间晚于三个镜像和扩展任务的成功时间。从构建产物检查中英文安装页，确认两种数据库的安装命令均使用本次 Release 的标签。README 写明如何选择发布版本和升级。
