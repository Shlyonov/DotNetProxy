# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY /ProxySrv.ConsoleHost/ProxySrv.ConsoleHost.csproj ./
COPY /ProxySrv.ProxyServer/ProxySrv.ProxyServer.csproj /ProxySrv.ProxyServer/
RUN dotnet restore

# Copy everything else and build
COPY /ProxySrv.ConsoleHost/ ./
COPY /ProxySrv.ProxyServer/ /ProxySrv.ProxyServer/
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:5.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "ProxySrv.ConsoleHost.dll"]