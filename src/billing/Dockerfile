FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["Directory.Packages.props", "."]
COPY ["BannedSymbols.txt", "."]
COPY ["nuget.config", "."]
COPY ["src/billing/CodeForCoders.Billing.slnx", "src/billing/"]
COPY ["src/billing/src/CodeForCoders.Billing.Api/CodeForCoders.Billing.Api.csproj", "src/billing/src/CodeForCoders.Billing.Api/"]
COPY ["src/billing/src/CodeForCoders.Billing.Application/CodeForCoders.Billing.Application.csproj", "src/billing/src/CodeForCoders.Billing.Application/"]
COPY ["src/billing/src/CodeForCoders.Billing.Contracts/CodeForCoders.Billing.Contracts.csproj", "src/billing/src/CodeForCoders.Billing.Contracts/"]
COPY ["src/billing/src/CodeForCoders.Billing.Domain/CodeForCoders.Billing.Domain.csproj", "src/billing/src/CodeForCoders.Billing.Domain/"]
COPY ["src/billing/src/CodeForCoders.Billing.Infra.Data/CodeForCoders.Billing.Infra.Data.csproj", "src/billing/src/CodeForCoders.Billing.Infra.Data/"]
COPY ["src/billing/src/CodeForCoders.Billing.Infra.Gateway/CodeForCoders.Billing.Infra.Gateway.csproj", "src/billing/src/CodeForCoders.Billing.Infra.Gateway/"]
COPY ["src/billing/src/CodeForCoders.Billing.Infra.Messaging/CodeForCoders.Billing.Infra.Messaging.csproj", "src/billing/src/CodeForCoders.Billing.Infra.Messaging/"]

RUN dotnet restore "src/billing/src/CodeForCoders.Billing.Api/CodeForCoders.Billing.Api.csproj" --configfile nuget.config

COPY . .
WORKDIR /src/src/billing
RUN dotnet publish "src/CodeForCoders.Billing.Api/CodeForCoders.Billing.Api.csproj" \
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
ENTRYPOINT ["dotnet", "CodeForCoders.Billing.Api.dll"]
