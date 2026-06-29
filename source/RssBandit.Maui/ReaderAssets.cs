using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace RssBandit.Maui;

/// <summary>
/// Loads the reader's bundled assets — the highlight.js theme + script and the two Hack fonts — from
/// the app package <em>once</em> and caches them. Together these form the Flyweight "shell" shared
/// across every article (assembled by <see cref="RssBandit.ViewModels.ReaderHtml.BuildShell"/>); the
/// fonts are returned base64-encoded so they can be inlined as <c>@font-face</c> data URIs (the bundled
/// <c>file:///android_asset</c> assets are otherwise blocked by the article document's origin).
/// </summary>
static class ReaderAssets
{
    private static readonly Lazy<Task<(string css, string js, string hackRegularB64, string hackBoldB64)>>
        _load = new(LoadCoreAsync); // Lazy<Task> -> read once, thread-safe, awaited by every reader.

    /// <summary>Returns the cached assets, loading them on first call.</summary>
    public static Task<(string css, string js, string hackRegularB64, string hackBoldB64)> LoadAsync()
        => _load.Value;

    private static async Task<(string, string, string, string)> LoadCoreAsync()
    {
        var css = await ReadTextAsync("tokyo-night-dark.min.css");
        var js = await ReadTextAsync("highlight.min.js");
        var hackRegular = await ReadBase64Async("Hack-Regular.ttf");
        var hackBold = await ReadBase64Async("Hack-Bold.ttf");
        return (css, js, hackRegular, hackBold);
    }

    private static async Task<string> ReadTextAsync(string name)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(name);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static async Task<string> ReadBase64Async(string name)
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync(name);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms); // app-package streams aren't seekable; copy then encode.
        return Convert.ToBase64String(ms.ToArray());
    }
}
