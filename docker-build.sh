#! /bin/bash

alias docker=podman
# pwsh
# Set-Alias docker podman

# backend
docker build -f ./src/backend/MiniCc.Api/Dockerfile  -t minicc-api:latest .
docker build -f ./src/backend/readability-api/Dockerfile  -t minicc-readability-api:latest ./src/backend/readability-api/

# frontend
docker build -f ./src/frontend/Dockerfile -t minicc-web:latest ./src/frontend/


# docker compose
docker compose -f ./docker-compose.yml up -d