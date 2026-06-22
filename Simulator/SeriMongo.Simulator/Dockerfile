# syntax=docker/dockerfile:1

FROM node:24-alpine AS ui-build
WORKDIR /src

COPY Simulator/SeriMongo.Simulator/package.json Simulator/SeriMongo.Simulator/package-lock.json ./
RUN npm ci

COPY Simulator/SeriMongo.Simulator/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src

COPY Simulator/SeriMongo.Simulator/SeriMongo.Simulator.csproj Simulator/SeriMongo.Simulator/
RUN dotnet restore Simulator/SeriMongo.Simulator/SeriMongo.Simulator.csproj

COPY Simulator/SeriMongo.Simulator/ Simulator/SeriMongo.Simulator/
RUN dotnet publish Simulator/SeriMongo.Simulator/SeriMongo.Simulator.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV SimulatorOptions__TargetBaseUrl=http://serimongo:8080

EXPOSE 8080

COPY --from=backend-build /app/publish ./
COPY --from=ui-build /src/dist/SeriMongoSimulator ./dist/SeriMongoSimulator

ENTRYPOINT ["dotnet", "SeriMongo.Simulator.dll"]
