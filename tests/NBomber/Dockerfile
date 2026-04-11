FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app

COPY . .
RUN dotnet restore NBomberRunner.csproj
RUN dotnet publish NBomberRunner.csproj -c Release -o /out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=0 /out .
ENTRYPOINT ["dotnet", "NBomberRunner.dll"]
