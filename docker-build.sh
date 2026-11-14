#! /bin/bash

alias docker=podman
# pwsh
# Set-Alias docker podman

# backend
docker build -f ./src/backend/MiniCc.Api/Dockerfile  -t minicc-api:latest .

# frontend
docker build -f ./src/frontend/Dockerfile -t minicc-web:latest ./src/frontend/
