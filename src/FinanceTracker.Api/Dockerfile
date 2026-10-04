# syntax=docker/dockerfile:1

# Multi-stage build: see docs/adr/0007-dockerfile-multistage-build-and-base-images.md.

# ---- Stage 1: runtime base (no SDK) ------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# ---- Stage 2: build -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Project files first, so the restore layer stays cached until one changes.
COPY ["src/FinanceTracker.Api/FinanceTracker.Api.csproj", "src/FinanceTracker.Api/"]
COPY ["src/FinanceTracker.Application/FinanceTracker.Application.csproj", "src/FinanceTracker.Application/"]
COPY ["src/FinanceTracker.Domain/FinanceTracker.Domain.csproj", "src/FinanceTracker.Domain/"]
COPY ["src/FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj", "src/FinanceTracker.Infrastructure/"]
RUN dotnet restore "src/FinanceTracker.Api/FinanceTracker.Api.csproj"

COPY src/ src/
RUN dotnet publish "src/FinanceTracker.Api/FinanceTracker.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---- Stage 3: final image ----------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Non-root user provided by the .NET base images.
USER $APP_UID

ENTRYPOINT ["dotnet", "FinanceTracker.Api.dll"]
