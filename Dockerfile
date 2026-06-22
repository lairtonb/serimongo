# syntax=docker/dockerfile:1

FROM node:24-alpine AS ui-build
WORKDIR /src/SeriMongo

COPY SeriMongo/package.json SeriMongo/package-lock.json ./
RUN npm ci

COPY SeriMongo/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

COPY SeriMongo/SeriMongo.csproj SeriMongo/
RUN dotnet restore SeriMongo/SeriMongo.csproj

COPY SeriMongo/ SeriMongo/
RUN dotnet publish SeriMongo/SeriMongo.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ApplicationOptions__Database__ConnectionString="Data Source=/data/serimongo.db"

EXPOSE 8080
VOLUME ["/data"]

COPY --from=backend-build /app/publish ./
COPY --from=ui-build /src/SeriMongo/dist/SeriMongo ./dist/SeriMongo
COPY seed.sql ./seed.sql

ENTRYPOINT ["dotnet", "SeriMongo.dll"]
