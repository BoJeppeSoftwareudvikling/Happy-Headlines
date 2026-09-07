FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ProfanityService/ProfanityService.csproj", "ProfanityService/"]
RUN dotnet restore "ProfanityService/ProfanityService.csproj"
COPY . .
WORKDIR "/src/ProfanityService"
RUN dotnet build "ProfanityService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ProfanityService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ProfanityService.dll"]
