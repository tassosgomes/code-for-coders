FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["Directory.Packages.props", "."]
COPY ["BannedSymbols.txt", "."]
COPY ["nuget.config", "."]
COPY ["src/learning/CodeForCoders.Learning.slnx", "src/learning/"]
COPY ["src/learning/src/CodeForCoders.Learning.Api/CodeForCoders.Learning.Api.csproj", "src/learning/src/CodeForCoders.Learning.Api/"]
COPY ["src/learning/src/CodeForCoders.Learning.Application/CodeForCoders.Learning.Application.csproj", "src/learning/src/CodeForCoders.Learning.Application/"]
COPY ["src/learning/src/CodeForCoders.Learning.Contracts/CodeForCoders.Learning.Contracts.csproj", "src/learning/src/CodeForCoders.Learning.Contracts/"]
COPY ["src/learning/src/CodeForCoders.Learning.Domain/CodeForCoders.Learning.Domain.csproj", "src/learning/src/CodeForCoders.Learning.Domain/"]
COPY ["src/learning/src/CodeForCoders.Learning.Infra.Data/CodeForCoders.Learning.Infra.Data.csproj", "src/learning/src/CodeForCoders.Learning.Infra.Data/"]
COPY ["src/learning/src/CodeForCoders.Learning.Infra.Messaging/CodeForCoders.Learning.Infra.Messaging.csproj", "src/learning/src/CodeForCoders.Learning.Infra.Messaging/"]

RUN dotnet restore "src/learning/src/CodeForCoders.Learning.Api/CodeForCoders.Learning.Api.csproj" --configfile nuget.config

COPY . .
WORKDIR /src/src/learning
RUN dotnet publish "src/CodeForCoders.Learning.Api/CodeForCoders.Learning.Api.csproj" \
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
ENTRYPOINT ["dotnet", "CodeForCoders.Learning.Api.dll"]
