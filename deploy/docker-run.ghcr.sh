#!/bin/bash
set -e

case "$#:${1-}:${2-}" in
    0::|2:--database:sqlite)
        base_compose=./deploy/docker-compose.sqlite.yaml
        ;;
    2:--database:postgres)
        base_compose=./deploy/docker-compose.yaml
        ;;
    *)
        echo "Usage: $0 [--database sqlite|postgres]" >&2
        exit 1
        ;;
esac

: "${MINICC_VERSION:?Set MINICC_VERSION to the Release tag}"
cd "$(dirname "$0")/.."

if command -v docker >/dev/null 2>&1; then
    container_engine=docker
elif command -v podman >/dev/null 2>&1; then
    container_engine=podman
else
    echo "Docker or Podman is required" >&2
    exit 1
fi

compose=("$container_engine" compose -f "$base_compose" -f ./deploy/docker-compose.ghcr.yaml)
"${compose[@]}" pull
"${compose[@]}" up -d --no-build
