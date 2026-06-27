using System;
using Microsoft.Maui.ApplicationModel;
using RssBandit.ViewModels;

namespace RssBandit.Maui;

/// <summary>
/// Reads a single article: hosts the engine-rendered HTML (NewsItemFormatter output, carried on the
/// view-model) in a WebView. Opening an item marks it read (write-through to the engine item).
/// </summary>
public partial class ItemDetailPage : ContentPage
{
    // TokyoNight reading theme for the article, in the Hack font (bundled -> reachable on Android at
    // /android_asset). Also hides the engine template's read/flag/enclosure chrome icons, which point
    // at an unreachable host ($IMAGEDIR$ -> templates.invalid), and fits article images to the screen.
    // Done here in the host layer, not the shared XSLT (the WinForms head serves those icons locally).
    private const string ThemeCss =
        "<style>" +
        "@font-face{font-family:'Hack';src:url('file:///android_asset/Hack-Regular.ttf');}" +
        "@font-face{font-family:'Hack';font-weight:bold;src:url('file:///android_asset/Hack-Bold.ttf');}" +
        "html,body{background:#1a1b26!important;color:#c0caf5!important;line-height:1.6;padding:4px 10px;}" +
        // The engine template wraps content in containers with their own (light) backgrounds; make every
        // descendant transparent so the dark body shows through, and force the text colour to TokyoNight.
        "body *{background-color:transparent!important;color:#c0caf5!important;}" +
        "body,body *{font-family:'Hack','Roboto Mono',monospace!important;}" +
        "a{color:#7aa2f7!important;}" +
        "h1,h2,h3,h4{color:#bb9af7!important;}" +
        "img{max-width:100%!important;height:auto!important;}" +
        "img[src*=\"templates.invalid\"]{display:none!important;}" +
        "pre,code{background:#16161e!important;color:#9ece6a!important;}" +
        "hr{border-color:#414868!important;}" +
        "blockquote{border-left:3px solid #7aa2f7!important;color:#a9b1d6!important;}" +
        "</style>";

    private bool _loaded;

    public ItemDetailPage(FeedItemViewModel item)
    {
        InitializeComponent();
        Title = item.Title;

        ArticleView.Source = new HtmlWebViewSource { Html = InjectCss(item.Html), BaseUrl = item.Link };
        ArticleView.Navigated += (s, e) => _loaded = true;
        ArticleView.Navigating += OnNavigating;

        item.IsRead = true;
    }

    private static string InjectCss(string html)
    {
        html ??= string.Empty;
        int head = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
        return head >= 0 ? html.Insert(head + "<head>".Length, ThemeCss) : ThemeCss + html;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        var url = e.Url ?? string.Empty;

        // The template's internal command links (fdaction:?action=...) are inert in a plain WebView.
        if (url.StartsWith("fdaction:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            return;
        }

        // Once the article itself has loaded, open real links in the system browser rather than
        // turning this reader pane into a browser. (The initial HtmlWebViewSource load is allowed
        // through because _loaded is still false until Navigated fires.)
        if (_loaded && (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                     || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            e.Cancel = true;
            _ = Launcher.Default.OpenAsync(url);
        }
    }
}
