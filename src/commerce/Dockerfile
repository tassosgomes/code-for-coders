FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["Directory.Packages.props", "."]
COPY ["BannedSymbols.txt", "."]
COPY ["nuget.config", "."]
COPY ["src/commerce/CodeForCoders.Commerce.slnx", "src/commerce/"]
COPY ["src/commerce/src/CodeForCoders.Commerce.Api/CodeForCoders.Commerce.Api.csproj", "src/commerce/src/CodeForCoders.Commerce.Api/"]
COPY ["src/commerce/src/CodeForCoders.Commerce.Application/CodeForCoders.Commerce.Application.csproj", "src/commerce/src/CodeForCoders.Commerce.Application/"]
COPY ["src/commerce/src/CodeForCoders.Commerce.Contracts/CodeForCoders.Commerce.Contracts.csproj", "src/commerce/src/CodeForCoders.Commerce.Contracts/"]
COPY ["src/commerce/src/CodeForCoders.Commerce.Domain/CodeForCoders.Commerce.Domain.csproj", "src/commerce/src/CodeForCoders.Commerce.Domain/"]
COPY ["src/commerce/src/CodeForCoders.Commerce.Infra.Data/CodeForCoders.Commerce.Infra.Data.csproj", "src/commerce/src/CodeForCoders.Commerce.Infra.Data/"]
COPY ["src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/CodeForCoders.Commerce.Infra.Messaging.csproj", "src/commerce/src/CodeForCoders.Commerce.Infra.Messaging/"]

RUN dotnet restore "src/commerce/src/CodeForCoders.Commerce.Api/CodeForCoders.Commerce.Api.csproj" --configfile nuget.config

COPY . .
WORKDIR /src/src/commerce
RUN dotnet publish "src/CodeForCoders.Commerce.Api/CodeForCoders.Commerce.Api.csproj" \
    --configuration $BUILD_CONFIGURATION \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app
COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "CodeForCoders.Commerce.Api.dll"]
