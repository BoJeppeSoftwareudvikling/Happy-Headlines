FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["PublisherService/PublisherService.csproj", "PublisherService/"]
COPY ["Messaging/Messaging.csproj", "Messaging/"]
COPY ["Monitoring/Monitoring.csproj", "Monitoring/"]
RUN dotnet restore "PublisherService/PublisherService.csproj"
COPY . .
WORKDIR "/src/PublisherService"
RUN dotnet build "PublisherService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PublisherService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PublisherService.dll"]
