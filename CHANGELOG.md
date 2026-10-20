# Changelog

Alle wesentlichen Änderungen an diesem Projekt werden in dieser Datei dokumentiert.
Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
die Versionierung folgt [Semantic Versioning](https://semver.org/lang/de/).

## [0.4.0] – 2026-10-20

### Hinzugefügt
- Abwesenheiten beantragen (alle Abwesenheitsarten aus der Konfiguration) über Monats- und Jahreskalender oder direkt aus den Zeitkorrekturen
- Genehmigungsworkflow: disziplinarische Führungskraft, Weiterleitung an die nächsthöhere Führungskraft bei deren Abwesenheit, abweichender Genehmiger, Fallback Personalabteilung
- Antragsübersicht mit Filter (Antragsteller/Genehmiger, Offen/Alle/Erledigt, ab Datum), Einzelgenehmigung, Begründungspflicht bei Ablehnung
- Löschen offener Anträge sowie Löschantrag (inverser Antrag) für genehmigte Abwesenheiten
- Freigaben „Arbeit > 10 Std.“, „Arbeit vor/nach Rahmen“ – sofort gutgeschrieben, bei Ablehnung wieder abgezogen
- Übertragung des Resturlaubs bis 31.05. mit zweistufiger Genehmigung (Antragszeitraum 01.01.–01.03.)
- Urlaubsberechnung: Jahresanspruch (anteilig im Eintrittsjahr), Übertrag, Verfall, Urlaubssaldo und Resturlaub (Plan zum 31.12.)
- Prüfung „Krank ohne Attest“ für max. 3 zusammenhängende Kalendertage, Hinweis auf „Krank mit eAU“
- Umbuchungen Sonderarbeitszeit/Wegezeit auf den Gleitzeitsaldo; Kontokorrekturen und Stornos für die Personalabteilung
- Benachrichtigungen im Dashboard und optional per E-Mail (SMTP in der appsettings.json)

## [0.3.0] – 2026-10-20

### Hinzugefügt
- Terminal mit Kommen/Gehen, Wechselbuchung (Ändern), Krank untertägig und Wegezeit – immer mit aktueller Uhrzeit
- Dashboard mit minutenaktuellen Konten (Saldo, Arbeitszeit, Soll, Wegezeit, Sonderarbeitszeit)
- Zeitkorrekturen in der Monatsübersicht: Intervalle hinzufügen, korrigieren und löschen mit Bestätigungsdialog und Bemerkung
- Detailfenster je Tag (Plandetails, bewerteter Dienst, Kontostände), tagesbezogene Bemerkungen und Buchungsverlauf (P10/P11/P20)
- Tagesberechnung: Rahmenarbeitszeit, gesetzlicher Pausenabzug, Kappung nach 10 Stunden, Wegezeit-Aufteilung, Krank untertägig
- Feiertagsberechnung (Osterformel) – Feiertage konfigurierbar, Standard Baden-Württemberg
- Regeln: keine Buchungen in der Zukunft, maximal 9 Tage rückwirkend (Personalabteilung ohne Grenze), keine Überschneidungen
- Unit-Tests für Tagesberechnung (inkl. Beispiele aus dem Benutzerhandbuch), Feiertage, Buchungen und Konten

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
