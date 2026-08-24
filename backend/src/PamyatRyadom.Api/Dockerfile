FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY backend/PamyatRyadom.sln ./
COPY backend/src/PamyatRyadom.Api/PamyatRyadom.Api.csproj src/PamyatRyadom.Api/
RUN dotnet restore src/PamyatRyadom.Api/PamyatRyadom.Api.csproj

COPY backend/src/PamyatRyadom.Api/. src/PamyatRyadom.Api/
RUN dotnet publish src/PamyatRyadom.Api/PamyatRyadom.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is used by the container HEALTHCHECK to probe /api/v1/health.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "PamyatRyadom.Api.dll"]
