# Changelog

Alle wesentlichen Änderungen an diesem Projekt werden in dieser Datei dokumentiert.
Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
die Versionierung folgt [Semantic Versioning](https://semver.org/lang/de/).

## [0.2.0] – 2026-10-20

### Hinzugefügt
- Datenmodell mit Entity Framework Core und SQLite (Mitarbeitende, Zeitbuchungen, Abwesenheiten, Anträge, Konten, Protokolle)
- Automatisches Anlegen der Datenbank und Abgleich der Abwesenheitsarten aus der `appsettings.json`
- Anmeldung per Cookie-Authentifizierung mit PBKDF2-Passwort-Hashes und Rollen (Mitarbeiter, Führungskraft, Personal, Administrator)
- Pflicht zur Passwortänderung bei Erstanmeldung, Passwortrichtlinie
- Seite „Meine Daten“ und Mitarbeiterverwaltung inkl. Führungskraft und abweichendem Genehmiger
- Sicherheits-Header (CSP, X-Frame-Options u. a.), persistente Data-Protection-Schlüssel
- Demodaten für die Entwicklungsumgebung

## [0.1.0] – 2026-10-20

### Hinzugefügt
- Projektgrundgerüst: Solution mit ASP.NET Core Razor Pages (.NET 10) und xUnit-Testprojekt
- Responsives Blau/Weiß-Layout mit Seitenleiste (mobil als ausklappbares Menü)
- Zentrale Konfiguration aller fachlichen Regeln in der `appsettings.json`
- HTTPS auf `https://localhost:5016`, Health-Check unter `/health`
- Dockerfile (Multi-Stage, unprivilegierter Benutzer) und `docker-compose.yml`
- Skripte zum Erzeugen und Vertrauen des HTTPS-Zertifikats (`scripts/create-dev-cert.*`)
