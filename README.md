# Jellyfin DefaultSettings Plugin

Ermöglicht Benutzern, benutzerdefinierte Standardeinstellungen für **Medienversionen (z. B. Bunt vs. Schwarz/Weiß)** sowie serienspezifische **Audio- und Untertitelspuren** in Jellyfin festzulegen:

- **Direkt auf der Serien-Detailseite im Web:**
  - Auf jeder Serie erscheint ein neuer Button **„Standard-Spuren / Version“**.
  - Ein Klick öffnet ein Dialogfenster, in dem Sie für diese Serie direkt die Wunschversion und Ton-/Untertitelspuren festlegen können.
  - Das Plugin erkennt automatisch vorhandene Fassungen (z. B. *Bunt*, *Schwarz/Weiß*, *1080p*) und vorhandene Tonspuren.

- **Serienspezifische Overrides:**
  - Bevorzugte Version / Fassung pro Serie (z. B. *Bunt* statt *Schwarz/Weiß*).
  - Übersteuern von Audiosprache und Untertitel-Modus pro Serie (z. B. Japanisch O-Ton mit Untertiteln für Animes, während normale Serien Ihre normale Kontoeinstellung nutzen).
  - Intelligenter Fallback: Hat eine Folge die Wunschversion oder Wunschsprache nicht, greift ohne Fehler der native Jellyfin-Standard.

- **Admin-Verwaltung für alle Benutzer:**
  - Im Dashboard unter **Plugins -> DefaultSettings** können Administratoren jedes beliebige Benutzerkonto aus einem Dropdown auswählen und dessen serienspezifische Einstellungen einsehen und anpassen.

- **App-übergreifende Wirkung:**
  - Durch serverseitige Interzeption der `/PlaybackInfo`-Endpunkte greifen Ihre Serien-Einstellungen **in allen Jellyfin-Clients** (Web, Android TV, Apple TV, iOS, Swiftfin, Roku, Kodi usw.).

---

## Installation & Update

1. Fügen Sie im Jellyfin Dashboard unter **Plugins -> Repositorys** folgendes Repository hinzu:
   ```
   https://raw.githubusercontent.com/toeck98/jellyfin-plugin-defaultsettings/main/manifest.json
   ```
2. Wechseln Sie auf den Reiter **Katalog**, wählen Sie **DefaultSettings** und klicken Sie auf **Installieren** (bzw. auf der Plugin-Seite auf **Aktualisieren**).
3. **Wichtig:** Starten Sie den Jellyfin-Server nach der Installation oder jedem Update neu, damit der Server die neue DLL lädt!
4. Laden Sie im Browser die Seite einmal mit `Strg + F5` neu, um den Browser-Cache für das Web-Dashboard zu aktualisieren.
