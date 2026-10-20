# Changelog

Alle wesentlichen Änderungen an diesem Projekt werden in dieser Datei dokumentiert.
Das Format basiert auf [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
die Versionierung folgt [Semantic Versioning](https://semver.org/lang/de/).

## [1.1.0] – 2026-10-20

### Hinzugefügt
- **Darkmode**: Hell, Dunkel oder „Wie System“ – umschaltbar über das Mond-/Sonnensymbol in der Kopfzeile oder unter Einstellungen; alle Seiten, Tabellen und Kalender angepasst
- **Englische Oberfläche**: Sprache Deutsch/Englisch unter Einstellungen bzw. bereits auf der Anmeldeseite wählbar; Navigation, Seiten, Meldungen, Regelhinweise, Berichte, Abwesenheitsarten, Feiertage sowie Datums- und Monatsangaben werden übersetzt
- Darstellung und Sprache werden pro Benutzer gespeichert und nach der Anmeldung automatisch übernommen
- Anmeldung wahlweise mit Benutzername oder Personalnummer
- Automatische Ergänzung neuer Spalten in bestehenden Datenbanken (kein manueller Eingriff beim Update nötig)
- Tests: Vollständigkeit der Übersetzungen, Platzhalter-Prüfung, Sprachumschaltung, Darkmode, Personalnummer-Login, Datenbank-Update

### Geändert
- Anmeldeformular ist nun mittig ausgerichtet
- „Abmelden“ erscheint als weiß umrandeter Knopf mit weißer Schrift am Ende der Navigation

## [1.0.0] – 2026-10-20

Erste vollständige Version.

### Hinzugefügt
- Dashboard pro Person konfigurierbar (Seite „Einstellungen“): Terminal, Krank untertägig, Wegezeit, Konten, Benachrichtigungen
- Integrationstests mit `WebApplicationFactory` (Anmeldung, Rechte, Sicherheits-Header, Terminal, CSV-Export, Aufruf aller Seiten)
- GitHub-Actions-Pipeline: Build, Tests mit Abdeckung, Docker-Image
- Skripte zum Erzeugen der Release-Tags aus den `release/*`-Branches
- Vollständige README (HTTPS-Zertifikat, lokaler Start, Docker, Konfiguration, Tests, Versionierung, Fehlerbehebung)

## [0.5.0] – 2026-10-20

### Hinzugefügt
- Regelverletzungen: fehlende Buchungen, fehlende Gehen-Buchung, Arbeitszeit > 10 Std., Arbeit vor/nach Rahmen, Arbeit an Abwesenheitstagen, Saldo-Grenzen und Ruhezeitunterschreitungen
- Quittieren von Regelverletzungen durch die Mitarbeitenden; Führungskräfte sehen die Unregelmäßigkeiten ihres Teams
- Berichte 500 Monatsjournal, 539 Saldo-Ampel, 540 Pausenabzug, 542 Urlaubsübersicht, 544 Arbeitszeit größer 10, 546 Ruhezeitverletzungen – mit Druckansicht und CSV-Export
- Führungskräfte sehen bei Berichten ihrer Mitarbeitenden nur Kontensalden, keine Einzelbuchungen
- Gruppenkalender für Führungskräfte (Abwesenheiten des Teams ohne Gründe)

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
