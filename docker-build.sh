#!/bin/bash
set -e

container_engine=docker
if ! command -v docker >/dev/null 2>&1; then
    container_engine=podman
fi

"$container_engine" build -f ./src/server/Dockerfile -t minicc-api:latest .
"$container_engine" build -f ./src/web/Dockerfile -t minicc-web:latest .
