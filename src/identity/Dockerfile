FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["Directory.Packages.props", "."]
COPY ["BannedSymbols.txt", "."]
COPY ["nuget.config", "."]
COPY ["src/identity/CodeForCoders.Identity.slnx", "src/identity/"]
COPY ["src/identity/src/CodeForCoders.Identity.Api/CodeForCoders.Identity.Api.csproj", "src/identity/src/CodeForCoders.Identity.Api/"]
COPY ["src/identity/src/CodeForCoders.Identity.Application/CodeForCoders.Identity.Application.csproj", "src/identity/src/CodeForCoders.Identity.Application/"]
COPY ["src/identity/src/CodeForCoders.Identity.Contracts/CodeForCoders.Identity.Contracts.csproj", "src/identity/src/CodeForCoders.Identity.Contracts/"]
COPY ["src/identity/src/CodeForCoders.Identity.Domain/CodeForCoders.Identity.Domain.csproj", "src/identity/src/CodeForCoders.Identity.Domain/"]
COPY ["src/identity/src/CodeForCoders.Identity.Infra.Data/CodeForCoders.Identity.Infra.Data.csproj", "src/identity/src/CodeForCoders.Identity.Infra.Data/"]
COPY ["src/identity/src/CodeForCoders.Identity.Infra.Messaging/CodeForCoders.Identity.Infra.Messaging.csproj", "src/identity/src/CodeForCoders.Identity.Infra.Messaging/"]

RUN dotnet restore "src/identity/src/CodeForCoders.Identity.Api/CodeForCoders.Identity.Api.csproj" --configfile nuget.config

COPY . .
WORKDIR /src/src/identity
RUN dotnet publish "src/CodeForCoders.Identity.Api/CodeForCoders.Identity.Api.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "CodeForCoders.Identity.Api.dll"]
