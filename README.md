# Gevitas Zeiterfassung

Webanwendung zur Arbeitszeiterfassung für Gevitas – geschrieben in **C# / ASP.NET Core Razor Pages (.NET 10)** mit **SQLite**.
Läuft lokal über **https://localhost:5016** oder als **Docker-Container**.

> Dieses Projekt befindet sich im Aufbau. Die vollständige Anleitung folgt mit Version 1.0.0.

## Schnellstart

```powershell
# 1. Zertifikat für https://localhost vertrauen und für Docker exportieren
.\scripts\create-dev-cert.ps1

# 2a. Lokal starten
dotnet run --project src/Gevitas.Zeiterfassung.Web

# 2b. oder im Docker-Container
docker compose up -d --build
```

Anschließend im Browser **https://localhost:5016** öffnen.
