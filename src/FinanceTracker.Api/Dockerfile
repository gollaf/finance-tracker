# syntax=docker/dockerfile:1

# Multi-stage build, cross-compiled for amd64 and arm64: see
# docs/adr/0024-multi-architecture-images.md.

# ---- Stage 1: runtime base (no SDK) ------------------------------------------
# This stage and the final one are the target architecture. They must not RUN
# anything: on a foreign architecture that would need emulation, and .NET
# isn't supported under QEMU.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# ---- Stage 2: build -----------------------------------------------------------
# Always the build machine's own architecture; -a $TARGETARCH makes the SDK
# produce output for the target instead.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
# Set by BuildKit from --platform: amd64 or arm64.
ARG TARGETARCH
WORKDIR /src

# Project files first, so the restore layer stays cached until one changes.
COPY ["src/FinanceTracker.Api/FinanceTracker.Api.csproj", "src/FinanceTracker.Api/"]
COPY ["src/FinanceTracker.Application/FinanceTracker.Application.csproj", "src/FinanceTracker.Application/"]
COPY ["src/FinanceTracker.Domain/FinanceTracker.Domain.csproj", "src/FinanceTracker.Domain/"]
COPY ["src/FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj", "src/FinanceTracker.Infrastructure/"]
RUN dotnet restore "src/FinanceTracker.Api/FinanceTracker.Api.csproj" -a $TARGETARCH

COPY src/ src/
RUN dotnet publish "src/FinanceTracker.Api/FinanceTracker.Api.csproj" \
    -c Release \
    -a $TARGETARCH \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

# ---- Stage 3: final image ----------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Non-root user provided by the .NET base images.
USER $APP_UID

ENTRYPOINT ["dotnet", "FinanceTracker.Api.dll"]
