# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/Bookit.Api/Bookit.Api.csproj
RUN dotnet publish src/Bookit.Api/Bookit.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

# Time zone data: the slot calculator resolves IANA zones such as America/Guayaquil.
# Slim images may not include it, so it is installed explicitly.
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

# Never run as root inside the container.
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "Bookit.Api.dll"]