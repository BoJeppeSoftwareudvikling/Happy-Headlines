FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["CommentService/CommentService.csproj", "CommentService/"]
COPY ["Monitoring/Monitoring.csproj", "Monitoring/"]
RUN dotnet restore "CommentService/CommentService.csproj"
COPY . .
WORKDIR "/src/CommentService"
RUN dotnet build "CommentService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CommentService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CommentService.dll"]
