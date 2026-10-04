# syntax=docker/dockerfile:1

# Same layout as src/FinanceTracker.Api/Dockerfile; only the differences are
# commented here.

# ---- Stage 1: runtime base --------------------------------------------------
# dotnet/runtime, not dotnet/aspnet, and no EXPOSE: the Worker serves no HTTP.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

# ---- Stage 2: build ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The Worker and every project it references (not the Api).
COPY ["src/FinanceTracker.Worker/FinanceTracker.Worker.csproj", "src/FinanceTracker.Worker/"]
COPY ["src/FinanceTracker.Application/FinanceTracker.Application.csproj", "src/FinanceTracker.Application/"]
COPY ["src/FinanceTracker.Domain/FinanceTracker.Domain.csproj", "src/FinanceTracker.Domain/"]
COPY ["src/FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj", "src/FinanceTracker.Infrastructure/"]
RUN dotnet restore "src/FinanceTracker.Worker/FinanceTracker.Worker.csproj"

COPY src/ src/
RUN dotnet publish "src/FinanceTracker.Worker/FinanceTracker.Worker.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---- Stage 3: final image ----------------------------------------------------
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

USER $APP_UID

ENTRYPOINT ["dotnet", "FinanceTracker.Worker.dll"]
