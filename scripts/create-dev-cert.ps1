<#
.SYNOPSIS
    Erstellt bzw. vertraut das ASP.NET-Core-Entwicklerzertifikat für https://localhost
    und exportiert es als PFX für den Docker-Container.

.DESCRIPTION
    1. Vertraut dem Entwicklerzertifikat im Windows-Zertifikatsspeicher (Browser zeigt "sicher").
    2. Exportiert das Zertifikat nach .\certs\gevitas-zeiterfassung.pfx (mit Passwort).
    3. Schreibt das Passwort in die Datei .env (wird von docker compose gelesen, ist in .gitignore).

.EXAMPLE
    .\scripts\create-dev-cert.ps1
    .\scripts\create-dev-cert.ps1 -Passwort "MeinPfxPasswort"
#>
param(
    [string]$Passwort
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$certDir = Join-Path $root "certs"
$pfx = Join-Path $certDir "gevitas-zeiterfassung.pfx"
$envFile = Join-Path $root ".env"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Das .NET SDK wurde nicht gefunden. Bitte .NET 10 SDK installieren: https://dotnet.microsoft.com/download"
}

if ([string]::IsNullOrWhiteSpace($Passwort)) {
    $bytes = New-Object byte[] 24
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $Passwort = [Convert]::ToBase64String($bytes) -replace '[^A-Za-z0-9]', 'x'
}

Write-Host "1/3 Entwicklerzertifikat für localhost vertrauen ..." -ForegroundColor Cyan
dotnet dev-certs https --trust
if ($LASTEXITCODE -ne 0) { throw "dotnet dev-certs https --trust ist fehlgeschlagen." }

Write-Host "2/3 Zertifikat nach $pfx exportieren ..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $certDir | Out-Null
dotnet dev-certs https --export-path $pfx --password $Passwort
if ($LASTEXITCODE -ne 0) { throw "Export des Zertifikats ist fehlgeschlagen." }

Write-Host "3/3 Passwort in .env schreiben ..." -ForegroundColor Cyan
"CERT_PASSWORD=$Passwort" | Set-Content -Path $envFile -Encoding ascii

Write-Host ""
Write-Host "Fertig. https://localhost:5016 wird jetzt vom Browser als sicher erkannt." -ForegroundColor Green
Write-Host "Lokal starten:   dotnet run --project src/Gevitas.Zeiterfassung.Web"
Write-Host "Docker starten:  docker compose up -d --build"
