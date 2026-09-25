FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["global.json", "."]
COPY ["Directory.Build.props", "."]
COPY ["Directory.Build.targets", "."]
COPY ["Directory.Packages.props", "."]
COPY ["BannedSymbols.txt", "."]
COPY ["nuget.config", "."]
COPY ["src/bff-student/CodeForCoders.BffStudent.slnx", "src/bff-student/"]
COPY ["src/bff-student/src/CodeForCoders.BffStudent.Api/CodeForCoders.BffStudent.Api.csproj", "src/bff-student/src/CodeForCoders.BffStudent.Api/"]
COPY ["src/bff-student/src/CodeForCoders.BffStudent.Application/CodeForCoders.BffStudent.Application.csproj", "src/bff-student/src/CodeForCoders.BffStudent.Application/"]
COPY ["src/bff-student/src/CodeForCoders.BffStudent.Contracts/CodeForCoders.BffStudent.Contracts.csproj", "src/bff-student/src/CodeForCoders.BffStudent.Contracts/"]
COPY ["src/bff-student/src/CodeForCoders.BffStudent.Domain/CodeForCoders.BffStudent.Domain.csproj", "src/bff-student/src/CodeForCoders.BffStudent.Domain/"]
COPY ["src/bff-student/src/CodeForCoders.BffStudent.Infra.Data/CodeForCoders.BffStudent.Infra.Data.csproj", "src/bff-student/src/CodeForCoders.BffStudent.Infra.Data/"]
COPY ["src/bff-student/src/CodeForCoders.BffStudent.Infra.Messaging/CodeForCoders.BffStudent.Infra.Messaging.csproj", "src/bff-student/src/CodeForCoders.BffStudent.Infra.Messaging/"]

RUN dotnet restore "src/bff-student/src/CodeForCoders.BffStudent.Api/CodeForCoders.BffStudent.Api.csproj" --configfile nuget.config

COPY . .
WORKDIR /src/src/bff-student
RUN dotnet publish "src/CodeForCoders.BffStudent.Api/CodeForCoders.BffStudent.Api.csproj" \
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
ENTRYPOINT ["dotnet", "CodeForCoders.BffStudent.Api.dll"]
