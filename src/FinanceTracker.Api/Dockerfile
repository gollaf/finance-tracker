# syntax=docker/dockerfile:1

# ---- Stage 1: runtime base --------------------------------------------------
# The ASP.NET Core runtime image: the .NET runtime plus the ASP.NET Core
# shared framework needed to *run* an already-built app. No SDK, no
# compilers, no MSBuild -- there is nothing in this image that could
# restore or build anything. Small, and nothing for an attacker to abuse
# as build tooling if the running container is ever compromised.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# ---- Stage 2: build -- has the full SDK -------------------------------------
# The SDK image (compilers, NuGet client, MSBuild) is several times the
# size of the aspnet image above. That's fine: this stage only exists to
# produce build output. It is never copied into the final image, so its
# size never reaches anything that ships.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy *only* the four .csproj files first, before any .cs source. Docker
# caches each instruction as its own layer, keyed by exactly what it
# received (COPY) or ran (RUN). As long as none of these four files
# change, this COPY and the `dotnet restore` below it are both served from
# cache on every later build -- so editing a .cs file doesn't re-download
# every NuGet package on the next `docker build`, only recompiles.
COPY ["src/FinanceTracker.Api/FinanceTracker.Api.csproj", "src/FinanceTracker.Api/"]
COPY ["src/FinanceTracker.Application/FinanceTracker.Application.csproj", "src/FinanceTracker.Application/"]
COPY ["src/FinanceTracker.Domain/FinanceTracker.Domain.csproj", "src/FinanceTracker.Domain/"]
COPY ["src/FinanceTracker.Infrastructure/FinanceTracker.Infrastructure.csproj", "src/FinanceTracker.Infrastructure/"]
RUN dotnet restore "src/FinanceTracker.Api/FinanceTracker.Api.csproj"

# Now copy the actual source tree and publish. This layer invalidates on
# every code change -- expected, since restore (the expensive network
# step) is the one we cached above, not compilation.
COPY src/ src/
RUN dotnet publish "src/FinanceTracker.Api/FinanceTracker.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# ---- Stage 3: final image ----------------------------------------------------
# Start again from the small runtime base, not from the SDK stage, so none
# of the SDK's compilers/tooling weight ends up in the image that actually
# ships and runs in production.
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# $APP_UID is defined by the base aspnet image itself -- a non-root "app"
# user it creates for exactly this purpose. Running as non-root is a
# defense-in-depth measure: if the app is ever compromised, the attacker
# doesn't automatically get root inside the container.
USER $APP_UID

ENTRYPOINT ["dotnet", "FinanceTracker.Api.dll"]
