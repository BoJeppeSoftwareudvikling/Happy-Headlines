FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DraftService/DraftService.csproj", "DraftService/"]
COPY ["Monitoring/Monitoring.csproj", "Monitoring/"]
RUN dotnet restore "DraftService/DraftService.csproj"
COPY . .
WORKDIR "/src/DraftService"
RUN dotnet build "DraftService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DraftService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DraftService.dll"]
