using CommunityToolkit.Mvvm.ComponentModel;

namespace RssBandit.Maui;

/// <summary>Binds the Settings screen to <see cref="AppSettings"/>; each change persists immediately.</summary>
public partial class SettingsViewModel : ObservableObject
{
    public SettingsViewModel()
    {
        _readerFontSize = AppSettings.ReaderFontSize;
        _refreshOnLaunch = AppSettings.RefreshOnLaunch;
        _markReadOnOpen = AppSettings.MarkReadOnOpen;
    }

    [ObservableProperty]
    private int _readerFontSize;
    partial void OnReaderFontSizeChanged(int value) => AppSettings.ReaderFontSize = value;

    [ObservableProperty]
    private bool _refreshOnLaunch;
    partial void OnRefreshOnLaunchChanged(bool value) => AppSettings.RefreshOnLaunch = value;

    [ObservableProperty]
    private bool _markReadOnOpen;
    partial void OnMarkReadOnOpenChanged(bool value) => AppSettings.MarkReadOnOpen = value;
}
