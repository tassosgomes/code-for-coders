FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["Directory.Packages.props", "."]
COPY ["BannedSymbols.txt", "."]
COPY ["nuget.config", "."]
COPY ["src/audit/CodeForCoders.Audit.slnx", "src/audit/"]
COPY ["src/audit/src/CodeForCoders.Audit.Api/CodeForCoders.Audit.Api.csproj", "src/audit/src/CodeForCoders.Audit.Api/"]
COPY ["src/audit/src/CodeForCoders.Audit.Application/CodeForCoders.Audit.Application.csproj", "src/audit/src/CodeForCoders.Audit.Application/"]
COPY ["src/audit/src/CodeForCoders.Audit.Contracts/CodeForCoders.Audit.Contracts.csproj", "src/audit/src/CodeForCoders.Audit.Contracts/"]
COPY ["src/audit/src/CodeForCoders.Audit.Domain/CodeForCoders.Audit.Domain.csproj", "src/audit/src/CodeForCoders.Audit.Domain/"]
COPY ["src/audit/src/CodeForCoders.Audit.Infra.Data/CodeForCoders.Audit.Infra.Data.csproj", "src/audit/src/CodeForCoders.Audit.Infra.Data/"]
COPY ["src/audit/src/CodeForCoders.Audit.Infra.Messaging/CodeForCoders.Audit.Infra.Messaging.csproj", "src/audit/src/CodeForCoders.Audit.Infra.Messaging/"]

RUN dotnet restore "src/audit/src/CodeForCoders.Audit.Api/CodeForCoders.Audit.Api.csproj" --configfile nuget.config

COPY . .
WORKDIR /src/src/audit
RUN dotnet publish "src/CodeForCoders.Audit.Api/CodeForCoders.Audit.Api.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

RUN apt-get update \
    && apt-get install --no-install-recommends --yes libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "CodeForCoders.Audit.Api.dll"]
