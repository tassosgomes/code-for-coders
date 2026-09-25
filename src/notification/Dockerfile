FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["Directory.Packages.props", "."]
COPY ["BannedSymbols.txt", "."]
COPY ["nuget.config", "."]
COPY ["src/notification/CodeForCoders.Notification.slnx", "src/notification/"]
COPY ["src/notification/src/CodeForCoders.Notification.Api/CodeForCoders.Notification.Api.csproj", "src/notification/src/CodeForCoders.Notification.Api/"]
COPY ["src/notification/src/CodeForCoders.Notification.Application/CodeForCoders.Notification.Application.csproj", "src/notification/src/CodeForCoders.Notification.Application/"]
COPY ["src/notification/src/CodeForCoders.Notification.Contracts/CodeForCoders.Notification.Contracts.csproj", "src/notification/src/CodeForCoders.Notification.Contracts/"]
COPY ["src/notification/src/CodeForCoders.Notification.Domain/CodeForCoders.Notification.Domain.csproj", "src/notification/src/CodeForCoders.Notification.Domain/"]
COPY ["src/notification/src/CodeForCoders.Notification.Infra.Data/CodeForCoders.Notification.Infra.Data.csproj", "src/notification/src/CodeForCoders.Notification.Infra.Data/"]
COPY ["src/notification/src/CodeForCoders.Notification.Infra.Messaging/CodeForCoders.Notification.Infra.Messaging.csproj", "src/notification/src/CodeForCoders.Notification.Infra.Messaging/"]

RUN dotnet restore "src/notification/src/CodeForCoders.Notification.Api/CodeForCoders.Notification.Api.csproj" --configfile nuget.config

COPY . .
WORKDIR /src/src/notification
RUN dotnet publish "src/CodeForCoders.Notification.Api/CodeForCoders.Notification.Api.csproj" \
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
ENTRYPOINT ["dotnet", "CodeForCoders.Notification.Api.dll"]
