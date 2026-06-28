using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using RssBandit.ViewModels;

namespace RssBandit.Maui;

/// <summary>
/// The article reader. Hosts a single WebView into which the reader "shell" (Tokyo Night theme +
/// inlined highlight.js + Hack fonts) is loaded <em>once</em>; each article's body is then swapped in
/// via a <c>renderArticle()</c> call (<see cref="ReaderHtml.RenderScript"/>) without reloading the
/// shell. This Flyweight reuse holds for the whole reading session: paging Prev/Next through the feed
/// never re-parses the ~1 MB of assets. The shell is torn down only when the page is popped (MAUI
/// disposes the WebView on pop), so the next visit rebuilds it.
/// </summary>
public partial class ItemDetailPage : ContentPage
{
    private readonly IReadOnlyList<FeedItemViewModel> _items;
    private int _index;
    private bool _shellLoaded;

    public ItemDetailPage(IReadOnlyList<FeedItemViewModel> items, int index)
    {
        InitializeComponent();
        _items = items;
        _index = index;
        ArticleView.Navigated += OnNavigated;
        ArticleView.Navigating += OnNavigating;
        UpdateChrome();
    }

    private FeedItemViewModel Current => _items[_index];

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_shellLoaded) return; // OnAppearing also fires on back-nav into the reader; load the shell once.
        try
        {
            var (css, js, hackRegular, hackBold) = await ReaderAssets.LoadAsync();
            var shell = ReaderHtml.BuildShell(css, js, hackRegular, hackBold, AppSettings.ReaderFontSize);
            // The body is injected once the shell document is ready (see OnNavigated).
            ArticleView.Source = new HtmlWebViewSource { Html = shell };
        }
        catch
        {
            // Degrade gracefully: if the assets can't be read, show the raw article without the skin.
            _shellLoaded = true;
            ArticleView.Source = new HtmlWebViewSource { Html = Current.Html, BaseUrl = Current.Link };
            MarkRead();
        }
    }

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (_shellLoaded) return; // only the first navigation is the shell load; renderArticle is not a navigation.
        _shellLoaded = true;
        await ShowCurrentAsync();
    }

    private async Task ShowCurrentAsync()
    {
        MarkRead();
        UpdateChrome();
        await ArticleView.EvaluateJavaScriptAsync(ReaderHtml.RenderScript(Current.Html));
    }

    private void MarkRead()
    {
        if (AppSettings.MarkReadOnOpen)
            Current.IsRead = true;
    }

    private void UpdateChrome()
    {
        Title = Current.Title;
        PrevButton.IsEnabled = _index > 0;
        NextButton.IsEnabled = _index < _items.Count - 1;
    }

    private async void OnPrev(object? sender, EventArgs e)
    {
        if (_index <= 0) return;
        _index--;
        await ShowCurrentAsync(); // Flyweight: shell stays, only the body swaps.
    }

    private async void OnNext(object? sender, EventArgs e)
    {
        if (_index >= _items.Count - 1) return;
        _index++;
        await ShowCurrentAsync();
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        var url = e.Url ?? string.Empty;

        // The engine's internal command links (fdaction:?action=...) are inert in a plain WebView.
        if (url.StartsWith("fdaction:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            return;
        }

        // Once the shell has loaded, real links open in the system browser rather than turning this
        // reader into a browser. (The shell's own initial load is allowed through — _shellLoaded is
        // still false then — and renderArticle body swaps don't raise Navigating at all.)
        if (_shellLoaded && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                          || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            e.Cancel = true;
            _ = Launcher.Default.OpenAsync(url);
        }
    }
}
