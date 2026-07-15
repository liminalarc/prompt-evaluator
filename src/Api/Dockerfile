# Build context is ./src so the whole solution is available for restore/publish.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
# Version is stamped from the git tag (git describe → APP_VERSION); "0.0.0-dev" locally.
ARG APP_VERSION=0.0.0-dev
RUN dotnet restore Api/Api.csproj
RUN dotnet publish Api/Api.csproj -c Release -o /app --no-restore -p:Version=$APP_VERSION

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# curl is used by the compose healthcheck.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
# Baked-in build metadata surfaced by GET /version and GET /api/version.
ARG GIT_COMMIT=dev
ENV GIT_COMMIT=$GIT_COMMIT
# Deploy channel — the reliable dev/prod discriminator (3.3). CI sets it from the git-ref
# (tag -> prod, main -> dev); "local" for a plain local build.
ARG APP_CHANNEL=local
ENV DEPLOY_CHANNEL=$APP_CHANNEL
EXPOSE 8080
ENTRYPOINT ["dotnet", "Api.dll"]
