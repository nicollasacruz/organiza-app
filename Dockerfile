FROM node:24-bookworm-slim AS web
WORKDIR /web
COPY frontend/package*.json ./
RUN npm install --no-audit --no-fund
COPY frontend/ ./
ENV STATIC_EXPORT=1
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY backend/ backend/
RUN dotnet publish backend/Organiza.Api -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/* && mkdir -p /data/keys && chown -R app:app /data
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/out/ ./wwwroot/
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Organiza.Api.dll"]
