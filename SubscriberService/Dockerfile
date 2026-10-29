FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["SubscriberService/SubscriberService.csproj", "SubscriberService/"]
COPY ["Messaging/Messaging.csproj", "Messaging/"]
COPY ["Monitoring/Monitoring.csproj", "Monitoring/"]
RUN dotnet restore "SubscriberService/SubscriberService.csproj"
COPY . .
WORKDIR "/src/SubscriberService"
RUN dotnet build "SubscriberService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SubscriberService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SubscriberService.dll"]
