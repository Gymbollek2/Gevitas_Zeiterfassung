# Changelog

Alle wesentlichen Änderungen an diesem Projekt werden in dieser Datei dokumentiert.
Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
die Versionierung folgt [Semantic Versioning](https://semver.org/lang/de/).

## [0.1.0] – 2026-10-20

### Hinzugefügt
- Projektgrundgerüst: Solution mit ASP.NET Core Razor Pages (.NET 10) und xUnit-Testprojekt
- Responsives Blau/Weiß-Layout mit Seitenleiste (mobil als ausklappbares Menü)
- Zentrale Konfiguration aller fachlichen Regeln in der `appsettings.json`
- HTTPS auf `https://localhost:5016`, Health-Check unter `/health`
- Dockerfile (Multi-Stage, unprivilegierter Benutzer) und `docker-compose.yml`
- Skripte zum Erzeugen und Vertrauen des HTTPS-Zertifikats (`scripts/create-dev-cert.*`)
