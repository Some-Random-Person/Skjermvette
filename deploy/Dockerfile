# The server, with the dashboard built into it. Built by docker-compose.yml, from the repository root.

FROM node:22-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
# Writes to ../src/Skjermvette.Server/wwwroot (vite.config.ts).
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src
COPY src/Skjermvette.Core/Skjermvette.Core.csproj src/Skjermvette.Core/
COPY src/Skjermvette.Server/Skjermvette.Server.csproj src/Skjermvette.Server/
RUN dotnet restore src/Skjermvette.Server
COPY src/Skjermvette.Core/ src/Skjermvette.Core/
COPY src/Skjermvette.Server/ src/Skjermvette.Server/
COPY --from=web /src/src/Skjermvette.Server/wwwroot/ src/Skjermvette.Server/wwwroot/
RUN dotnet publish src/Skjermvette.Server -c Release -o /app --no-restore

# 10.0 is the newest 10.0.x patch when the image is built. It must be 10.0.13 or later (#39),
# so rebuild with --pull to get security fixes.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=server /app ./
# The keys that protect sign-in cookies and email links. docker-compose.yml keeps them in a volume, so a new
# container doesn't sign everyone out. Created here so the volume belongs to the app user.
RUN mkdir -p /home/app/.aspnet/DataProtection-Keys && chown -R $APP_UID /home/app/.aspnet
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Skjermvette.Server.dll"]
