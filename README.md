# Gevitas Zeiterfassung

Webanwendung zur Arbeitszeiterfassung für **Gevitas** – fachlich angelehnt an das Benutzerhandbuch „Interflex 6040“,
umgesetzt mit **C# / ASP.NET Core Razor Pages (.NET 10)** und **SQLite**, in einem responsiven **Blau/Weiß-Design**.
Die Anwendung läuft lokal unter **https://localhost:5016** oder als **Docker-Container**.

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![SQLite](https://img.shields.io/badge/DB-SQLite-0b4f9c) ![Docker](https://img.shields.io/badge/Docker-ready-1e7be0)

---

## Inhalt

1. [Funktionen](#funktionen)
2. [Voraussetzungen](#voraussetzungen)
3. [Schnellstart](#schnellstart)
4. [HTTPS-Zertifikat (Browser zeigt „sicher“)](#https-zertifikat-browser-zeigt-sicher)
5. [Lokal starten (https://localhost:5016)](#lokal-starten-httpslocalhost5016)
6. [Mit Docker starten](#mit-docker-starten)
7. [Erste Anmeldung & Benutzer](#erste-anmeldung--benutzer)
8. [Benachrichtigungen](#benachrichtigungen)
9. [Mitarbeitende anlegen](#mitarbeitende-anlegen)
10. [Darkmode & Sprache](#darkmode--sprache)
11. [Konfiguration (appsettings.json)](#konfiguration-appsettingsjson)
12. [Tests](#tests)
13. [Versionierung](#versionierung)
14. [Projektstruktur](#projektstruktur)
15. [Fehlerbehebung](#fehlerbehebung)

---

## Funktionen

| Bereich | Umfang |
|---|---|
| **Dashboard** | Terminal (Kommen/Gehen), minutenaktuelle Konten (Saldo, Arbeitszeit, Soll, Wegezeit, Sonderarbeitszeit, Urlaubssaldo, Resturlaub), Benachrichtigungen – pro Person konfigurierbar |
| **Terminal** | Kommen, Gehen, Ändern (Wechselbuchung P11), Krank untertägig, Wegezeit – immer mit aktueller Uhrzeit |
| **Zeitkorrekturen** | Monatsübersicht, Intervalle hinzufügen/korrigieren/löschen mit Bestätigung und Bemerkung, Detailfenster (Plandetails, bewerteter Dienst), tagesbezogene Bemerkungen, Buchungsverlauf (P10/P11/P20) |
| **Regeln** | Rahmenarbeitszeit 06–20 Uhr, gesetzlicher Pausenabzug (30/45 Min., 15-Min.-Regel, 6-Std.-Block), Kappung nach 10 Std., Wegezeit füllt zuerst das Soll auf, Krank untertägig füllt das Soll auf, keine Buchungen in der Zukunft, max. 9 Tage rückwirkend |
| **Anträge** | Abwesenheiten (Urlaub, Gleitzeitabbau, Krank o. A./mit eAU, Sonderurlaube …), Löschung offener/genehmigter Abwesenheiten, Arbeit > 10 Std., Arbeit vor/nach Rahmen, Übertragung des Resturlaubs (zweistufig) |
| **Genehmigung** | Disziplinarische Führungskraft, automatische Weiterleitung bei deren Abwesenheit, abweichender Genehmiger, Fallback Personalabteilung, Begründungspflicht bei Ablehnung |
| **Kalender** | Monats- und Jahreskalender (Eintrag hinzufügen/löschen), Gruppenkalender für Führungskräfte (ohne Abwesenheitsgründe) |
| **Konten** | Gleitzeit, Wegezeit, Sonderarbeitszeit, Umbuchungen auf den Saldo, Kontokorrekturen/Stornos durch die Personalabteilung |
| **Urlaub** | Jahresanspruch (anteilig im Eintrittsjahr), Übertrag mit Verfall 31.03. bzw. 31.05., Urlaubssaldo (tagesaktuell), Resturlaub (Plan zum 31.12.) |
| **Regelverletzungen** | Fehlende Buchungen, > 10 Std., Rahmen, Saldo-Grenzen, Ruhezeit (11 Std.) – Quittierung durch Mitarbeitende |
| **Berichte** | 500 Monatsjournal, 539 Saldo-Ampel, 540 Pausenabzug, 542 Urlaubsübersicht, 544 Arbeitszeit > 10, 546 Ruhezeitverletzungen – Druck/PDF und CSV-Export |
| **Benachrichtigungen** | Glocke oben rechts mit Zähler der ungelesenen Nachrichten, ungelesene fett, Klick markiert als gelesen und öffnet das Ziel, „Alle als gelesen markieren“, Übersichtsseite – optional zusätzlich per E-Mail |
| **Verwaltung** | Mitarbeitende, Rollen, Führungskraft (nur Personen mit Führungsrolle wählbar), abweichender Genehmiger, Beginn der Zeiterfassung, Sollzeit, Urlaubsanspruch, Passwort-Reset |
| **Darstellung & Sprache** | Darkmode (Hell/Dunkel/Wie System, Umschalter in der Kopfzeile), Oberfläche auf Deutsch oder Englisch – pro Benutzer gespeichert, auf der Anmeldeseite wählbar |
| **Sicherheit** | Cookie-Anmeldung, PBKDF2-Passwort-Hashes, Rollen, Passwortänderung bei Erstanmeldung, Anti-Forgery, CSP und weitere Sicherheits-Header, HTTPS/HSTS |

---

## Voraussetzungen

| Für | Benötigt |
|---|---|
| Lokal (HTTPS) | [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Docker | [Docker Desktop](https://www.docker.com/products/docker-desktop/) (oder Docker Engine + Compose) **und** das .NET 10 SDK zum Erzeugen des Zertifikats |
| Repository | [Git](https://git-scm.com/) |

Prüfen:

```powershell
dotnet --version   # 10.0.x
docker --version
```

---

## Schnellstart

```powershell
git clone https://github.com/Gymbollek2/Gevitas_Zeiterfassung.git
cd Gevitas_Zeiterfassung

# 1. Zertifikat für https://localhost erstellen, vertrauen und für Docker exportieren
.\scripts\create-dev-cert.ps1

# 2a. Lokal starten …
dotnet run --project src/Gevitas.Zeiterfassung.Web

# 2b. … oder im Docker-Container
docker compose up -d --build
```

Danach **https://localhost:5016** im Browser öffnen.

---

## HTTPS-Zertifikat (Browser zeigt „sicher“)

Die Anwendung nutzt das **ASP.NET-Core-Entwicklerzertifikat** für `localhost`. Damit der Browser die Verbindung als sicher
anzeigt, muss das Zertifikat im Betriebssystem als vertrauenswürdig eingetragen sein. Das erledigt das Skript:

**Windows (PowerShell):**

```powershell
.\scripts\create-dev-cert.ps1
```

> Falls PowerShell Skripte blockiert: `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` und erneut ausführen.

**macOS / Linux:**

```bash
chmod +x scripts/create-dev-cert.sh
./scripts/create-dev-cert.sh
```

Das Skript

1. führt `dotnet dev-certs https --trust` aus (Windows zeigt einen Sicherheitsdialog – mit **Ja** bestätigen),
2. exportiert das Zertifikat nach `certs/gevitas-zeiterfassung.pfx` (für Docker),
3. schreibt das PFX-Passwort in die Datei `.env` (wird von `docker compose` gelesen).

`certs/` und `.env` stehen in der `.gitignore` und werden **nie** eingecheckt.

**Manuell (falls gewünscht):**

```powershell
dotnet dev-certs https --clean      # optional: altes Zertifikat entfernen
dotnet dev-certs https --trust
dotnet dev-certs https -ep .\certs\gevitas-zeiterfassung.pfx -p "MeinPasswort"
Set-Content .env "CERT_PASSWORD=MeinPasswort"
```

**Firefox:** Firefox nutzt einen eigenen Zertifikatsspeicher. Unter `about:config` den Wert
`security.enterprise_roots.enabled` auf `true` setzen (Windows/macOS) und Firefox neu starten.

**Zertifikat unter Linux:** `--trust` wird je nach Distribution nur teilweise unterstützt. Alternativ mit
[mkcert](https://github.com/FiloSottile/mkcert) arbeiten: `mkcert -install` und
`mkcert -pkcs12 -p12-file certs/gevitas-zeiterfassung.pfx localhost` (Standardpasswort `changeit` in `.env` eintragen).

---

## Lokal starten (https://localhost:5016)

```powershell
dotnet run --project src/Gevitas.Zeiterfassung.Web
```

* Die URL steht in der `appsettings.json` (`Kestrel:Endpoints:Https:Url`) und in `Properties/launchSettings.json`.
* Beim ersten Start wird die SQLite-Datenbank unter `src/Gevitas.Zeiterfassung.Web/App_Data/zeiterfassung.db` angelegt.
* `dotnet run` startet in der Umgebung **Development** – dort werden zusätzlich Demo-Benutzer angelegt (siehe unten).
* In Visual Studio / Rider einfach das Profil **https** starten.

Release-Build ohne Demodaten:

```powershell
dotnet run --project src/Gevitas.Zeiterfassung.Web --configuration Release -- --environment Production
```

---

## Mit Docker starten

Voraussetzung: Das Zertifikat wurde mit `scripts/create-dev-cert.*` erzeugt (`certs/gevitas-zeiterfassung.pfx` und `.env` existieren).

```powershell
docker compose up -d --build     # bauen und im Hintergrund starten
docker compose logs -f           # Logs ansehen
docker compose down              # stoppen (Daten bleiben erhalten)
```

Die Anwendung ist dann ebenfalls unter **https://localhost:5016** erreichbar – mit demselben, vertrauenswürdigen Zertifikat.

| Thema | Umsetzung |
|---|---|
| Image | Multi-Stage-Build (`mcr.microsoft.com/dotnet/sdk:10.0` → `aspnet:10.0`), läuft als unprivilegierter Benutzer |
| Port | Container lauscht auf `https://+:5016`, veröffentlicht als `5016:5016` |
| Zertifikat | `./certs` wird schreibgeschützt nach `/https` gemountet, Passwort aus `.env` (`CERT_PASSWORD`) |
| Daten | Docker-Volume `gevitas-daten` → `/app/App_Data` (SQLite-Datenbank und Data-Protection-Schlüssel) |
| Zeitzone | `TZ=Europe/Berlin`; zusätzlich rechnet die Anwendung immer in der konfigurierten Zeitzone |
| Health-Check | `https://localhost:5016/health` |

Ohne Compose:

```powershell
docker build -t gevitas-zeiterfassung .
docker run -d --name gevitas-zeiterfassung -p 5016:5016 `
  -e Kestrel__Certificates__Default__Path=/https/gevitas-zeiterfassung.pfx `
  -e Kestrel__Certificates__Default__Password=<Passwort aus .env> `
  -v ${PWD}/certs:/https:ro -v gevitas-daten:/app/App_Data `
  gevitas-zeiterfassung
```

Datenbank sichern:

```powershell
docker compose cp zeiterfassung:/app/App_Data/zeiterfassung.db ./backup-zeiterfassung.db
```

---

## Erste Anmeldung & Benutzer

| Benutzer | Passwort | Rolle | Hinweis |
|---|---|---|---|
| `admin` (Pers.-Nr. `0001`) | `Gevitas#Start2026` | Administrator | Wird beim ersten Start angelegt, **Passwort muss bei der ersten Anmeldung geändert werden** |
| `fuehrung` (`1001`) | `Demo1234!` | Führungskraft | nur in Development |
| `mitarbeiter` (`1003`) | `Demo1234!` | Mitarbeiter (unterstellt `fuehrung`) | nur in Development |
| `kollegin` (`1004`) | `Demo1234!` | Mitarbeiterin (unterstellt `fuehrung`) | nur in Development |
| `personal` (`1002`) | `Demo1234!` | Personalabteilung | nur in Development |

Angemeldet wird wahlweise mit **Benutzername oder Personalnummer**.

Das Start-Passwort des Administrators sollte für den produktiven Betrieb über eine Umgebungsvariable gesetzt werden,
z. B. in der `docker-compose.yml`: `Seed__AdminPasswort: "EinSicheresPasswort1"`.

**Rollen:** Mitarbeiter/in → Führungskraft (Genehmigungen, Gruppenkalender, Team-Salden) → Personalabteilung
(Verwaltung, Kontokorrekturen, Zeitkorrekturen ohne 9-Tage-Grenze, alle Berichte) → Administrator.

---

## Benachrichtigungen

Oben rechts in der Kopfzeile befindet sich eine **Glocke**. Die rote Zahl zeigt die Anzahl der ungelesenen
Benachrichtigungen. Ein Klick auf die Glocke öffnet die neuesten Benachrichtigungen – ungelesene sind **fett** und
blau markiert. Ein Klick auf eine Benachrichtigung markiert sie als gelesen (die Zahl verringert sich) und öffnet die
passende Seite. Mit **„Alle als gelesen markieren“** werden alle auf einmal erledigt; „Alle Benachrichtigungen anzeigen“
öffnet die vollständige Liste.

Benachrichtigt wird bei:

| Ereignis | Empfänger |
|---|---|
| Antrag gestellt | Antragsteller/in (Bestätigung) und Genehmiger/in (bzw. Personalabteilung) |
| Antrag genehmigt / abgelehnt | Antragsteller/in (inkl. Bemerkung) |
| Zweistufiger Antrag weitergeleitet | Antragsteller/in und nächste Genehmigungsstufe |
| Antrag zurückgezogen bzw. gelöscht | Genehmiger/in |
| Führungskraft oder Vertretung geändert | Person, neue Führungskraft bzw. Vertretung; offene Anträge gehen automatisch an die neue Genehmigerin bzw. den neuen Genehmiger |
| Zugang eingerichtet | neue Person (inkl. Beginn der Zeiterfassung) |
| Zeitbuchungen oder Konten durch die Personalabteilung geändert | betroffene Person |

Der Text wird in der Sprache der Empfängerin bzw. des Empfängers erstellt. Ist `Smtp:Aktiv` gesetzt, geht jede
Benachrichtigung zusätzlich per E-Mail raus. Gelesene Benachrichtigungen werden nach
`Zeiterfassung:BenachrichtigungenAufbewahrenTage` (Standard 180) beim Start gelöscht.

## Mitarbeitende anlegen

* **Disziplinarische Führungskraft / abweichender Genehmiger:** Wählbar sind nur aktive Personen mit der Rolle
  Führungskraft, Personal oder Administrator – nicht die Person selbst und keine ihr unterstellten Personen.
* **Zeiterfassung ab:** Standard ist **„Am Folgetag der Accounterstellung“**, alternativ ein beliebiger anderer Tag.
  Sollzeit und Kontostände werden erst ab diesem Tag berechnet – niemand startet mit Minusstunden, auch wenn das
  Eintrittsdatum weit zurückliegt. Der Tag kann später unter „Bearbeiten“ geändert werden.
* Eine Führungskraft kann erst herabgestuft oder deaktiviert werden, wenn ihr niemand mehr zugeordnet ist und keine
  Anträge mehr offen sind.
* Die eigene Rolle und der eigene Zugang können nicht geändert bzw. deaktiviert werden.
* Administratoren können nur von Administratoren angelegt oder bearbeitet werden.
* Beim Update auf 1.2.0 wird der Erfassungsbeginn bestehender Personen einmalig auf den Tag ihrer ersten Zeitbuchung
  gesetzt (ohne Buchungen auf den Tag des Updates).

---

## Darkmode & Sprache

* **Darkmode:** Über das Mond-/Sonnensymbol oben rechts oder unter **Einstellungen → Darstellung** (Hell, Dunkel, Wie System).
* **Sprache:** Unter **Einstellungen → Sprache** (Deutsch/English) oder vor der Anmeldung über „Deutsch | English“ unter dem Anmeldeformular.
* Beide Einstellungen werden pro Benutzer in der Datenbank gespeichert und nach jeder Anmeldung übernommen.
* Übersetzungen stehen in `Services/Uebersetzungen.cs` (Schlüssel = deutscher Text). Neue Texte im Code mit `T("…")` bzw.
  `F("…", wert)` kennzeichnen – ein Test schlägt fehl, solange eine englische Übersetzung fehlt.
* Gespeicherte Inhalte (z. B. Bemerkungen, Buchungsverlauf) bleiben in der Sprache, in der sie erfasst wurden.
  Benachrichtigungen werden in der Sprache der Empfängerin bzw. des Empfängers erstellt.

## Konfiguration (appsettings.json)

Alle fachlichen Regeln stehen in `src/Gevitas.Zeiterfassung.Web/appsettings.json`. Jeder Wert lässt sich per
Umgebungsvariable überschreiben (`:` wird zu `__`, z. B. `Zeiterfassung__MaxArbeitszeitMinuten=600`).

| Schlüssel | Bedeutung | Standard |
|---|---|---|
| `Kestrel:Endpoints:Https:Url` | Adresse der Anwendung | `https://localhost:5016` |
| `ConnectionStrings:Zeiterfassung` | SQLite-Datei | `Data Source=App_Data/zeiterfassung.db` |
| `DataProtection:SchluesselPfad` | Schlüssel für Anmelde-Cookies | `App_Data/keys` |
| `Authentifizierung:*` | Cookie-Name, Sitzungsdauer, Mindestlänge Passwort | 480 Min., 8 Zeichen |
| `Seed:*` | Administrator anlegen, Demodaten | `admin`, Demodaten nur in Development |
| `Smtp:*` | E-Mail-Benachrichtigungen (Aktiv, Host, Port, SSL, Benutzer, Passwort, Absender) | deaktiviert |
| `Zeiterfassung:Zeitzone` | Zeitzone für Buchungen | `Europe/Berlin` |
| `Zeiterfassung:Sollarbeitszeit` | Standard-Soll pro Tag (Minuten) und Arbeitstage | 480, Mo–Fr |
| `Zeiterfassung:Rahmenarbeitszeit` | Beginn/Ende der Rahmenarbeitszeit | 06:00–20:00 |
| `Zeiterfassung:MaxArbeitszeitMinuten` | Kappung der täglichen Arbeitszeit | 600 (10 Std.) |
| `Zeiterfassung:Pausen` | Mindestpause, Schwellen 6/9 Std., 30/45 Min., Blockregel | gesetzlich |
| `Zeiterfassung:MaxRueckwirkendeTage` | Rückwirkende Erfassung | 9 |
| `Zeiterfassung:BenachrichtigungenAufbewahrenTage` | Gelesene Benachrichtigungen werden danach gelöscht | 180 |
| `Zeiterfassung:Saldo` | Unter-/Obergrenze, Ampelgrenzen (Minuten) | −40 / +80 Std., 20 / 40 Std. |
| `Zeiterfassung:Urlaub` | Jahresurlaub, Verfall, verlängerter Verfall, Antragszeitraum Übertrag | 30, 31.03., 31.05., 01.01.–01.03. |
| `Zeiterfassung:KrankOhneAttestMaxTage` | Max. zusammenhängende Tage „Krank o. A.“ | 3 |
| `Zeiterfassung:MinRuhezeitMinuten` | Mindestruhezeit | 660 (11 Std.) |
| `Zeiterfassung:Feiertage` | Gültige Feiertage (Standard Baden-Württemberg) | siehe Datei |
| `Zeiterfassung:Abwesenheitsarten` | Code, Name, Farbe, Wirkung, zählt als Urlaub, max. Tage, Nachweis | 17 Arten |
| `Zeiterfassung:Support` | Kontakt Personalabteilung, IT-Hinweis | – |

Mögliche Werte für `Feiertage`: `Neujahr, HeiligeDreiKoenige, Frauentag, Gruendonnerstag, Karfreitag, Ostersonntag,
Ostermontag, TagDerArbeit, ChristiHimmelfahrt, Pfingstsonntag, Pfingstmontag, Fronleichnam, MariaeHimmelfahrt,
Weltkindertag, TagDerDeutschenEinheit, Reformationstag, Allerheiligen, BussUndBettag, Heiligabend,
ErsterWeihnachtstag, ZweiterWeihnachtstag, Silvester`.

Mögliche `Wirkung` einer Abwesenheitsart: `SollGutschrift` (Tag mit Soll gutgeschrieben), `Gleitzeitabbau`
(keine Gutschrift), `AbbauSonderarbeitszeit`, `AbbauWegezeit`. Änderungen werden beim nächsten Start übernommen.

---

## Tests

```powershell
dotnet test
```

Die Tests (xUnit) decken u. a. ab:

* **Tagesberechnung** – Pausenabzug inkl. der Beispiele aus dem Benutzerhandbuch, Kappung, Rahmen, Wegezeit, Krank untertägig, Abwesenheiten
* **Buchungen** – Terminal, Zukunft/9-Tage-Grenze, Überschneidungen, Protokoll
* **Konten** – Saldo-Fortschreibung, Wochenenden/Feiertage
* **Urlaub** – Übertrag, Verfall, Verlängerung, anteiliger Anspruch
* **Anträge** – Genehmigerermittlung, Vertretung, Krank o. A., Ablehnung, Löschantrag, zweistufiger Übertrag
* **Regelverletzungen, Ruhezeit, CSV-Export, Feiertage, Kontobuchungen**
* **Integrationstests** – komplette Anwendung (WebApplicationFactory): Anmeldung, Rechte, Sicherheits-Header und Aufruf aller Seiten

Mit Testabdeckung: `dotnet test --collect:"XPlat Code Coverage"`.
Die GitHub-Actions-Pipeline (`.github/workflows/ci.yml`) baut, testet und erstellt das Docker-Image bei jedem Push.

---

## Versionierung

Das Projekt folgt [Semantic Versioning](https://semver.org/lang/de/). Die aktuelle Version steht in
`Directory.Build.props` (`<Version>`) und wird in der Fußzeile der Anwendung angezeigt. Alle Änderungen stehen in der
[CHANGELOG.md](CHANGELOG.md).

| Version | Inhalt |
|---|---|
| 0.1.0 | Projektgrundgerüst, Layout, Konfiguration, Docker |
| 0.2.0 | Datenmodell, SQLite, Anmeldung, Rollen, Mitarbeiterverwaltung |
| 0.3.0 | Terminal, Zeitkorrekturen, Tagesberechnung, Konten |
| 0.4.0 | Anträge, Genehmigung, Kalender, Urlaub, Umbuchungen |
| 0.5.0 | Regelverletzungen, Berichte, Gruppenkalender |
| 1.0.0 | Dashboard-Einstellungen, Integrationstests, CI, Dokumentation |
| 1.1.0 | Darkmode, Englisch, Login per Personalnummer, zentrierte Anmeldung |
| 1.2.0 | Benachrichtigungs-Glocke, Prüfungen in der Mitarbeiterverwaltung, Beginn der Zeiterfassung |

Jede Version liegt als Branch `release/vX.Y.Z` vor. Daraus werden die Git-Tags erzeugt mit:

```powershell
.\scripts\create-release-tags.ps1      # bzw. ./scripts/create-release-tags.sh
```

Neue Version erstellen: Version in `Directory.Build.props` erhöhen, `CHANGELOG.md` ergänzen, committen und taggen
(`git tag -a v1.2.0 -m "Version 1.2.0" && git push origin v1.2.0`).

---

## Projektstruktur

```
Gevitas_Zeiterfassung/
├── src/Gevitas.Zeiterfassung.Web/      Razor-Pages-Anwendung
│   ├── Data/                           EF-Core-DbContext, Datenbank-Initialisierung
│   ├── Domain/                         Entitäten und Enums
│   ├── Infrastructure/                 DI-Registrierung, Middleware, Basisklasse für Seiten
│   ├── Options/                        Typisierte Konfiguration (appsettings.json)
│   ├── Pages/                          Oberflächen (Dashboard, Terminal, Zeitwirtschaft, Anträge, Berichte, Verwaltung …)
│   ├── Services/                       Fachlogik (Tagesberechnung, Konten, Urlaub, Anträge, Berichte …)
│   └── wwwroot/                        CSS (Blau/Weiß, responsiv) und JavaScript
├── tests/Gevitas.Zeiterfassung.Tests/  Unit- und Integrationstests (xUnit)
├── scripts/                            Zertifikat- und Release-Skripte
├── Dockerfile, docker-compose.yml
├── Directory.Build.props               Gemeinsame Build-Einstellungen und Version
└── CHANGELOG.md
```

**Datenbank:** Das Schema wird beim Start automatisch angelegt (`EnsureCreated`); in späteren Versionen hinzugekommene
Spalten werden bei bestehenden Datenbanken automatisch ergänzt (`DatenbankInitialisierung.SchemaAktualisierenAsync`).
Ein Update erfordert also keinen manuellen Eingriff – die Daten bleiben erhalten.

---

## Fehlerbehebung

| Problem | Lösung |
|---|---|
| Browser meldet „nicht sicher“ | `scripts/create-dev-cert.ps1` erneut ausführen, Browser neu starten; Firefox siehe oben |
| `Unable to configure HTTPS endpoint` | Kein Entwicklerzertifikat vorhanden → `dotnet dev-certs https --trust` |
| Docker: `CERT_PASSWORD` fehlt | Zertifikatsskript ausführen (legt `.env` an) |
| Docker: Zertifikat nicht lesbar (Linux) | `chmod 644 certs/gevitas-zeiterfassung.pfx` |
| Port 5016 belegt | Anderen Prozess beenden oder Port in `appsettings.json` / `docker-compose.yml` ändern |
| Datenbank zurücksetzen (lokal) | Anwendung stoppen, Ordner `src/Gevitas.Zeiterfassung.Web/App_Data` löschen |
| Datenbank zurücksetzen (Docker) | `docker compose down -v` (**löscht alle Daten!**) |
| Admin-Passwort vergessen | Datenbank zurücksetzen oder als Personal/Admin über die Mitarbeiterverwaltung neu setzen |

Fragen zur Anwendung: Personalabteilung (siehe Seite **Information** in der Anwendung).
