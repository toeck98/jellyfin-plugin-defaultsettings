# Jellyfin DefaultSettings Plugin

Ermöglicht Benutzern, benutzerdefinierte Standardeinstellungen für Audio-, Untertitel- und Medienversionen in Jellyfin festzulegen:

- **Globale Einstellungen pro Benutzer:**
  - Bevorzugte Audiosprache (z. B. Deutsch, Englisch, Japanisch)
  - Bevorzugte Untertitelsprache
  - Untertitel-Modus:
    - *Nur Forced*: Nur erzwungene Untertitel anzeigen
    - *Immer*: Untertitel immer einblenden
    - *Smart*: Untertitel nur aktivieren, wenn die Tonspur fremdsprachig ist
    - *Keine*: Untertitel standardmäßig deaktivieren
  - Bevorzugte Version / Edition

- **Serienspezifische Overrides:**
  - Übersteuern von Audiosprache, Untertitelsprache und Untertitel-Modus pro Serie (z. B. Japanisch O-Ton mit Untertiteln für Animes, Deutsch für Standard-Serien)
  - Bevorzugte Medienversion pro Serie (z. B. *Bunt* vs. *Schwarz/Weiß*)
  - Automatische Erkennung der vorhandenen Versionen und Tonspuren im Web-Dashboard

- **App-übergreifende Wirkung:**
  - Durch serverseitige Interzeption der `/PlaybackInfo`- und `/Items`-Endpunkte greifen die Einstellungen **in allen Jellyfin-Clients** (Web, Android TV, Apple TV, iOS, Swiftfin, Roku, Kodi usw.).

---

## Installation

1. Plugin als ZIP kompilieren oder über ein Plugin-Repository installieren.
2. In das Jellyfin `plugins/DefaultSettings`-Verzeichnis entpacken.
3. Jellyfin-Server neustarten.
4. Im Jellyfin Dashboard unter **Plugins -> DefaultSettings** konfigurieren.
