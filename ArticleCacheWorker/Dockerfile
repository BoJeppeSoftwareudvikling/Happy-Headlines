FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ArticleCacheWorker/ArticleCacheWorker.csproj", "ArticleCacheWorker/"]
COPY ["ArticleCache.Contracts/ArticleCache.Contracts.csproj", "ArticleCache.Contracts/"]
COPY ["Monitoring/Monitoring.csproj", "Monitoring/"]

RUN dotnet restore "ArticleCacheWorker/ArticleCacheWorker.csproj"

COPY . .
WORKDIR "/src/ArticleCacheWorker"

RUN dotnet publish "ArticleCacheWorker.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ArticleCacheWorker.dll"]