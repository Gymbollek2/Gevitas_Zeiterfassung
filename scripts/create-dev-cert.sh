#!/usr/bin/env bash
# Erstellt bzw. vertraut das ASP.NET-Core-Entwicklerzertifikat für https://localhost
# und exportiert es als PFX für den Docker-Container (macOS/Linux).
#
#   ./scripts/create-dev-cert.sh [PASSWORT]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CERT_DIR="$ROOT/certs"
PFX="$CERT_DIR/gevitas-zeiterfassung.pfx"
ENV_FILE="$ROOT/.env"
PASSWORT="${1:-$(head -c 24 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 32)}"

command -v dotnet >/dev/null 2>&1 || { echo "Das .NET 10 SDK wurde nicht gefunden: https://dotnet.microsoft.com/download" >&2; exit 1; }

echo "1/3 Entwicklerzertifikat für localhost vertrauen ..."
if ! dotnet dev-certs https --trust; then
    echo "Hinweis: Automatisches Vertrauen wird auf dieser Plattform evtl. nicht vollständig unterstützt."
    echo "         Siehe README, Abschnitt 'Zertifikat unter Linux'."
fi

echo "2/3 Zertifikat nach $PFX exportieren ..."
mkdir -p "$CERT_DIR"
dotnet dev-certs https --export-path "$PFX" --password "$PASSWORT"
# Der Container läuft als unprivilegierter Benutzer und muss die Datei lesen können
chmod 644 "$PFX"

echo "3/3 Passwort in .env schreiben ..."
printf 'CERT_PASSWORD=%s\n' "$PASSWORT" > "$ENV_FILE"
chmod 600 "$ENV_FILE"

echo
echo "Fertig. https://localhost:5016 wird jetzt vom Browser als sicher erkannt."
echo "Lokal starten:   dotnet run --project src/Gevitas.Zeiterfassung.Web"
echo "Docker starten:  docker compose up -d --build"
