# syntax=docker/dockerfile:1
#
# Multi-stage production build for the ASP.NET Core API.
# Build context: backend/ (see backend/.dockerignore) - the LiteratureMillionaire.Tests
# project is deliberately excluded from that context; the runtime image never needs it.

# --- build ----------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy only the project file first so `dotnet restore` is its own cache layer, independent
# of source-code changes below.
COPY LiteratureMillionaire.API/LiteratureMillionaire.API.csproj LiteratureMillionaire.API/
RUN dotnet restore LiteratureMillionaire.API/LiteratureMillionaire.API.csproj

COPY LiteratureMillionaire.API/ LiteratureMillionaire.API/
RUN dotnet publish LiteratureMillionaire.API/LiteratureMillionaire.API.csproj \
        --no-restore \
        -c Release \
        -o /app/publish

# --- runtime ----------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl: the one HTTP utility the container's own HEALTHCHECK/Compose healthcheck needs to
# call GET /health from inside the container. No SDK, no source, no test project, no secret
# is ever copied into this stage - only the published output below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# Non-root user baked into the official aspnet image (APP_UID is defined by that base image).
USER $APP_UID

# `docker run ... --migrate-only` (see deploy/compose.yml's `migrate` service) runs the
# migration/seed step and exits instead of starting Kestrel - see Program.cs.
ENTRYPOINT ["dotnet", "LiteratureMillionaire.API.dll"]
