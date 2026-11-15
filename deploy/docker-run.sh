#!/bin/bash
set -e

cd "$(dirname "$0")/.."

if command -v docker >/dev/null 2>&1; then
    container_engine=docker
elif command -v podman >/dev/null 2>&1; then
    container_engine=podman
else
    echo "Docker or Podman is required" >&2
    exit 1
fi

"$container_engine" compose -f ./deploy/docker-compose.yaml up -d --build
