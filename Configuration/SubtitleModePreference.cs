namespace Jellyfin.Plugin.DefaultSettings.Configuration;

/// <summary>
/// Definiert das bevorzugte Verhalten für Untertitel.
/// </summary>
public enum SubtitleModePreference
{
    /// <summary>
    /// Nur erzwungene/forced Untertitel anzeigen (z. B. bei fremdsprachigen Passagen).
    /// </summary>
    OnlyForced = 0,

    /// <summary>
    /// Untertitel in der gewünschten Sprache immer aktivieren.
    /// </summary>
    Always = 1,

    /// <summary>
    /// Untertitel nur aktivieren, wenn die gewählte Tonspur eine Fremdsprache ist.
    /// </summary>
    Smart = 2,

    /// <summary>
    /// Untertitel standardmäßig deaktivieren.
    /// </summary>
    None = 3
}
