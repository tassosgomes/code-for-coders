FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["Directory.Packages.props", "."]
COPY ["BannedSymbols.txt", "."]
COPY ["nuget.config", "."]
COPY ["src/media/CodeForCoders.Media.slnx", "src/media/"]
COPY ["src/media/src/CodeForCoders.Media.Api/CodeForCoders.Media.Api.csproj", "src/media/src/CodeForCoders.Media.Api/"]
COPY ["src/media/src/CodeForCoders.Media.Application/CodeForCoders.Media.Application.csproj", "src/media/src/CodeForCoders.Media.Application/"]
COPY ["src/media/src/CodeForCoders.Media.Contracts/CodeForCoders.Media.Contracts.csproj", "src/media/src/CodeForCoders.Media.Contracts/"]
COPY ["src/media/src/CodeForCoders.Media.Domain/CodeForCoders.Media.Domain.csproj", "src/media/src/CodeForCoders.Media.Domain/"]
COPY ["src/media/src/CodeForCoders.Media.Infra.Data/CodeForCoders.Media.Infra.Data.csproj", "src/media/src/CodeForCoders.Media.Infra.Data/"]
COPY ["src/media/src/CodeForCoders.Media.Infra.Messaging/CodeForCoders.Media.Infra.Messaging.csproj", "src/media/src/CodeForCoders.Media.Infra.Messaging/"]

RUN dotnet restore "src/media/src/CodeForCoders.Media.Api/CodeForCoders.Media.Api.csproj" --configfile nuget.config

COPY . .
WORKDIR /src/src/media
RUN dotnet publish "src/CodeForCoders.Media.Api/CodeForCoders.Media.Api.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "CodeForCoders.Media.Api.dll"]
