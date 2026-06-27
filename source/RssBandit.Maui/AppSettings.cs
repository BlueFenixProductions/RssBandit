using Microsoft.Maui.Storage;

namespace RssBandit.Maui;

/// <summary>
/// App settings persisted via MAUI <see cref="Preferences"/> -- the cross-platform key/value store.
/// (The engine's registry-backed IPersistedSettings no-ops off-Windows, so the MAUI head keeps its
/// own user settings here.)
/// </summary>
public static class AppSettings
{
    /// <summary>Base font size (px) for the article reader WebView.</summary>
    public static int ReaderFontSize
    {
        get => Preferences.Default.Get(nameof(ReaderFontSize), 16);
        set => Preferences.Default.Set(nameof(ReaderFontSize), value);
    }

    /// <summary>Refresh every feed when the app launches (otherwise feeds refresh lazily on open).</summary>
    public static bool RefreshOnLaunch
    {
        get => Preferences.Default.Get(nameof(RefreshOnLaunch), false);
        set => Preferences.Default.Set(nameof(RefreshOnLaunch), value);
    }

    /// <summary>Mark an article read when it's opened.</summary>
    public static bool MarkReadOnOpen
    {
        get => Preferences.Default.Get(nameof(MarkReadOnOpen), true);
        set => Preferences.Default.Set(nameof(MarkReadOnOpen), value);
    }
}
