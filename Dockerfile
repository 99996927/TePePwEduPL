# Obraz aplikacji (Hr.Api + zbudowany Angular). Budowanie: docker compose build (patrz compose.yaml).

# 1. Angular
FROM node:24 AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npm run build

# 2. Publikacja .NET z Angularem w wwwroot
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS publish
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/Hr.Core/Hr.Core.csproj src/Hr.Core/
COPY src/Hr.Rules/Hr.Rules.csproj src/Hr.Rules/
COPY src/Hr.Agent/Hr.Agent.csproj src/Hr.Agent/
COPY src/Hr.Api/Hr.Api.csproj src/Hr.Api/
RUN dotnet restore src/Hr.Api/Hr.Api.csproj
COPY src/ src/
COPY --from=web /src/web/dist/hr-app/browser/ src/Hr.Api/wwwroot/
RUN dotnet publish src/Hr.Api/Hr.Api.csproj -c Release -o /out --no-restore

# 3. Środowisko uruchomieniowe (użytkownik nie-root)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=publish /out ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Hr.Api.dll"]
