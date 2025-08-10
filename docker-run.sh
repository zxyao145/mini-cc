#! /bin/bash

alias docker=podman
# pwsh
# Set-Alias docker podman

# docker compose
docker compose -f ./docker-compose.yaml up -d --build