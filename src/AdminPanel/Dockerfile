FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["AdminPanel/AdminPanel.csproj", "AdminPanel/"]
COPY ["Data/Data.csproj", "Data/"]
COPY ["Infrastracture/Abstractions.csproj", "Infrastracture/"]
COPY ["Logging/Logging.csproj", "Logging/"]

RUN dotnet restore "AdminPanel/AdminPanel.csproj"

COPY . .
WORKDIR "/src/AdminPanel"
RUN dotnet build "AdminPanel.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "AdminPanel.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AdminPanel.dll"]
