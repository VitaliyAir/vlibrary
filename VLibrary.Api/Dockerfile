# VLibrary API. Build context: the solution folder (D:\Family\Developer\VLibrary), e.g.
#   docker build -f VLibrary.Api/Dockerfile -t vlibrary-api .
# Started by D:\Family\Developer\VDocker\docker-compose.yml.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first: the restore layer is reused while only code changes.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY VLibrary.Domain/VLibrary.Domain.csproj VLibrary.Domain/
COPY VLibrary.Application/VLibrary.Application.csproj VLibrary.Application/
COPY VLibrary.Infrastructure/VLibrary.Infrastructure.csproj VLibrary.Infrastructure/
COPY VLibrary.Api/VLibrary.Api.csproj VLibrary.Api/
RUN dotnet restore VLibrary.Api/VLibrary.Api.csproj

COPY VLibrary.Domain/ VLibrary.Domain/
COPY VLibrary.Application/ VLibrary.Application/
COPY VLibrary.Infrastructure/ VLibrary.Infrastructure/
COPY VLibrary.Api/ VLibrary.Api/
RUN dotnet publish VLibrary.Api/VLibrary.Api.csproj -c Release --no-restore -o /app/publish -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# DjVuLibre: djvused (DjVu metadata) and ddjvu (DjVu covers).
RUN apt-get update \
    && apt-get install -y --no-install-recommends djvulibre-bin \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# The log file sink writes to /app/logs; the app runs as the image's non-root user.
RUN mkdir -p /app/logs && chown "$APP_UID" /app/logs
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Docker
EXPOSE 8080
ENTRYPOINT ["dotnet", "VLibrary.Api.dll"]
