# Lokales Pack-Skript für DefaultSettings
param (
    [string]$Version = "1.1.0.0"
)

$ErrorActionPreference = "Stop"

Write-Host "Baue Plugin DefaultSettings in Version $Version..." -ForegroundColor Cyan

# 1. Kompilieren
dotnet build "$PSScriptRoot\DefaultSettings.csproj" -c Release /p:Version=$Version

$binDir = "$PSScriptRoot\bin\Release\net8.0"
$artifactsDir = "$PSScriptRoot\artifacts"

if (!(Test-Path $artifactsDir)) {
    New-Item -ItemType Directory -Path $artifactsDir | Out-Null
}

$zipPath = "$artifactsDir\DefaultSettings.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# 2. ZIP packen (ausschließlich die DLL)
Write-Host "Erstelle ZIP-Archiv unter $zipPath..." -ForegroundColor Cyan
$dllPath = "$binDir\Jellyfin.Plugin.DefaultSettings.dll"

# Temporären Ordner für sauberes ZIP verwenden
$tempZipFolder = "$artifactsDir\temp_zip"
if (Test-Path $tempZipFolder) { Remove-Item $tempZipFolder -Recurse -Force }
New-Item -ItemType Directory -Path $tempZipFolder | Out-Null
Copy-Item $dllPath -Destination $tempZipFolder

Compress-Archive -Path "$tempZipFolder\*" -DestinationPath $zipPath -Force
Remove-Item $tempZipFolder -Recurse -Force

# 3. MD5-Prüfsumme ermitteln
$md5 = (Get-FileHash -Path $zipPath -Algorithm MD5).Hash.ToLower()

Write-Host "`nErfolgreich gepackt!" -ForegroundColor Green
Write-Host "ZIP-Pfad: $zipPath"
Write-Host "MD5 Hash: $md5" -ForegroundColor Yellow
Write-Host "Dateigröße: $((Get-Item $zipPath).Length) Bytes`n"
