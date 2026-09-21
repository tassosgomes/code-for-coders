FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["Directory.Packages.props", "."]
COPY ["BannedSymbols.txt", "."]
COPY ["nuget.config", "."]
COPY ["src/bff-admin/CodeForCoders.BffAdmin.slnx", "src/bff-admin/"]
COPY ["src/bff-admin/src/CodeForCoders.BffAdmin.Api/CodeForCoders.BffAdmin.Api.csproj", "src/bff-admin/src/CodeForCoders.BffAdmin.Api/"]
COPY ["src/bff-admin/src/CodeForCoders.BffAdmin.Application/CodeForCoders.BffAdmin.Application.csproj", "src/bff-admin/src/CodeForCoders.BffAdmin.Application/"]
COPY ["src/bff-admin/src/CodeForCoders.BffAdmin.Contracts/CodeForCoders.BffAdmin.Contracts.csproj", "src/bff-admin/src/CodeForCoders.BffAdmin.Contracts/"]
COPY ["src/bff-admin/src/CodeForCoders.BffAdmin.Domain/CodeForCoders.BffAdmin.Domain.csproj", "src/bff-admin/src/CodeForCoders.BffAdmin.Domain/"]
COPY ["src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data/CodeForCoders.BffAdmin.Infra.Data.csproj", "src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Data/"]
COPY ["src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Messaging/CodeForCoders.BffAdmin.Infra.Messaging.csproj", "src/bff-admin/src/CodeForCoders.BffAdmin.Infra.Messaging/"]

RUN dotnet restore "src/bff-admin/src/CodeForCoders.BffAdmin.Api/CodeForCoders.BffAdmin.Api.csproj" --configfile nuget.config

COPY . .
WORKDIR /src/src/bff-admin
RUN dotnet publish "src/CodeForCoders.BffAdmin.Api/CodeForCoders.BffAdmin.Api.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "CodeForCoders.BffAdmin.Api.dll"]
