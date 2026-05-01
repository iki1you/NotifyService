FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["API/API.csproj", "API/"]
COPY ["Data/Data.csproj", "Data/"]
COPY ["Infrastracture/Abstractions.csproj", "Infrastracture/"]
COPY ["Logging/Logging.csproj", "Logging/"]
COPY ["Orchestrator/Orchestrator.csproj", "Orchestrator/"]
COPY ["Queue/Queue.csproj", "Queue/"]
COPY ["Adapters/Adapters.csproj", "Adapters/"]
COPY ["RateLimiter/RateLimiter.csproj", "RateLimiter/"]

RUN dotnet restore "API/API.csproj"

COPY . .
WORKDIR "/src/API"
RUN dotnet build "API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "API.dll"]
