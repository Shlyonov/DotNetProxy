# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY /ConsoleHost/ConsoleHost.csproj ./
COPY /ProxyServer/ProxyServer.csproj /ProxyServer/
RUN dotnet restore

# Copy everything else and build
COPY /ConsoleHost/ ./
COPY /ProxyServer/ /ProxyServer/
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "ConsoleHost.dll"]