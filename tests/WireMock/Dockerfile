FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app

COPY . .
RUN dotnet restore WireMockRunner.csproj
RUN dotnet publish WireMockRunner.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=0 /out .
ENTRYPOINT ["dotnet", "WireMockRunner.dll"]
