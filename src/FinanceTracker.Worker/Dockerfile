# syntax=docker/dockerfile:1

# Same three-stage layout as src/FinanceTracker.Api/Dockerfile -- see
# docs/adr/0007-dockerfile-multistage-build-and-base-images.md for the
# reasoning behind each stage. Only the differences are commented here.

# ---- Stage 1: runtime base --------------------------------------------------
# dotnet/runtime, not dotnet/aspnet: this process serves no HTTP, so it
# doesn't need the ASP.NET Core shared framework the aspnet image adds on
# top of this one. Smaller image, less in it that could ever be exploited.
# No EXPOSE either -- there is no port for anything to connect to.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

# ---- Stage 2: build ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# The Worker's own .csproj plus every project it references, directly or
# transitively (Worker -> Application + Infrastructure -> Domain), copied
# before any source so the restore layer below stays cached until a
# .csproj actually changes. The Api's .csproj is not needed here.
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

# Non-root, same as the Api image -- $APP_UID is defined by every official
# .NET base image, runtime included, not just aspnet.
USER $APP_UID

ENTRYPOINT ["dotnet", "FinanceTracker.Worker.dll"]
