FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ArticleService/ArticleService.csproj", "ArticleService/"]
RUN dotnet restore "ArticleService/ArticleService.csproj"
COPY . .
WORKDIR "/src/ArticleService"
RUN dotnet build "ArticleService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ArticleService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ArticleService.dll"]