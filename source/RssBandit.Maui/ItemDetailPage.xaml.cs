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
    // The engine's XSLT template references read/flag/enclosure chrome icons under an unreachable host
    // ($IMAGEDIR$ -> https://templates.invalid/...). Hide just those so the article reads clean; the
    // article's own remote images still load. Done here (not in the shared template -- the WinForms
    // head serves those icons locally and wants them).
    private const string HideChromeCss = "<style>img[src*=\"templates.invalid\"]{display:none}</style>";

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
        return head >= 0 ? html.Insert(head + "<head>".Length, HideChromeCss) : HideChromeCss + html;
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
