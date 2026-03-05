FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["Workers/Workers.csproj", "Workers/"]
COPY ["Data/Data.csproj", "Data/"]
COPY ["Infrastracture/Abstractions.csproj", "Infrastracture/"]
COPY ["Queue/Queue.csproj", "Queue/"]

RUN dotnet restore "Workers/Workers.csproj"

COPY . .
WORKDIR "/src/Workers"
RUN dotnet build "Workers.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Workers.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Workers.dll"]
