# syntax=docker/dockerfile:1
# Single-container image for AWS App Runner (spec 3.2) — the `litmus-ai` service.
# The .NET API serves the built Angular SPA from wwwroot: one origin, no nginx.
# Build context = repo root:  docker build -f deploy/Dockerfile -t <tag> .
# (eval-runner keeps its own image — see eval-runner/Dockerfile.)

# --- Angular build ------------------------------------------------------------
FROM node:20-alpine AS client
WORKDIR /web
# git resolves the brand-tokens git dependency during npm ci.
RUN apk add --no-cache git
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build
# Output: /web/dist/web/browser

# --- .NET publish -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src
# Whole solution so Api's project references (Domain/Application/Infrastructure) restore.
COPY src/ ./
# Version is stamped from the git tag (git describe -> APP_VERSION); "0.0.0-dev" locally.
ARG APP_VERSION=0.0.0-dev
RUN dotnet restore Api/Api.csproj
RUN dotnet publish Api/Api.csproj -c Release -o /app/publish --no-restore -p:Version=$APP_VERSION

# --- Runtime ------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# curl backs the container / App Runner health probe.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=server /app/publish ./
# The built SPA lands in wwwroot; the API serves it outside Development (UseStaticFiles +
# MapFallbackToFile in Program.cs).
COPY --from=client /web/dist/web/browser ./wwwroot
# Baked-in build metadata surfaced by GET /version and GET /api/version (3.3).
ARG GIT_COMMIT=dev
ENV GIT_COMMIT=$GIT_COMMIT
# Deploy channel — the reliable dev/prod discriminator (3.3). CI sets it from the git-ref.
ARG APP_CHANNEL=local
ENV DEPLOY_CHANNEL=$APP_CHANNEL
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Api.dll"]
