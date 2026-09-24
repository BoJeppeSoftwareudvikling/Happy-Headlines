FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["NewsletterService/NewsletterService.csproj", "NewsletterService/"]
COPY ["Messaging/Messaging.csproj", "Messaging/"]
COPY ["Monitoring/Monitoring.csproj", "Monitoring/"]
RUN dotnet restore "NewsletterService/NewsletterService.csproj"
COPY . .
WORKDIR "/src/NewsletterService"
RUN dotnet build "NewsletterService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NewsletterService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NewsletterService.dll"]
