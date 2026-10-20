# syntax=docker/dockerfile:1

# ---------- Build-Stufe ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Erst nur Projektdateien kopieren, damit der Restore-Layer gecacht wird
COPY Directory.Build.props ./
COPY src/Gevitas.Zeiterfassung.Web/Gevitas.Zeiterfassung.Web.csproj src/Gevitas.Zeiterfassung.Web/
RUN dotnet restore src/Gevitas.Zeiterfassung.Web/Gevitas.Zeiterfassung.Web.csproj

COPY src/ src/
RUN dotnet publish src/Gevitas.Zeiterfassung.Web/Gevitas.Zeiterfassung.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# ---------- Laufzeit-Stufe ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    TZ=Europe/Berlin \
    Kestrel__Endpoints__Https__Url=https://+:5016

# Datenverzeichnis (SQLite + Data-Protection-Schlüssel) für den unprivilegierten Benutzer "app"
RUN mkdir -p /app/App_Data /https && chown -R $APP_UID:$APP_UID /app/App_Data

COPY --from=build /app/publish .

USER $APP_UID
EXPOSE 5016
VOLUME ["/app/App_Data"]

ENTRYPOINT ["dotnet", "Gevitas.Zeiterfassung.Web.dll"]
