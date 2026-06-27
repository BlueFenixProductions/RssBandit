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
    // A <head> blob injected into the article: the Tokyo Night Dark reading theme in the Hack font,
    // plus highlight.js for code-block syntax highlighting (same as PostXING). The .js/.css/.ttf are
    // bundled raw assets reachable on Android at /android_asset; highlightAll() runs once on load.
    private const string ThemeCss =
        "<link rel=\"stylesheet\" href=\"file:///android_asset/tokyo-night-dark.min.css\">" +
        "<style>" +
        "@font-face{font-family:'Hack';src:url('file:///android_asset/Hack-Regular.ttf');}" +
        "@font-face{font-family:'Hack';font-weight:bold;src:url('file:///android_asset/Hack-Bold.ttf');}" +
        "html,body{background:#1a1b26!important;color:#c0caf5!important;line-height:1.6;padding:4px 10px;" +
        "overflow-x:hidden!important;overflow-wrap:break-word!important;word-break:break-word!important;}" +
        // Every container transparent over the dark body; max-width:100% defeats the template's
        // desktop-era div.PostContent{max-width:70%}. Colour is forced on text elements only -- NOT
        // span/pre/code -- so highlight.js's syntax colours survive.
        "body *{background-color:transparent!important;max-width:100%!important;box-sizing:border-box!important;}" +
        "body,body *{font-family:'Hack','Roboto Mono',monospace!important;}" +
        "p,div,li,td,th,blockquote,strong,em,b,i,small{color:#c0caf5!important;}" +
        "a{color:#7aa2f7!important;}" +
        "h1,h2,h3,h4{color:#bb9af7!important;}" +
        // width:auto overrides a width=\"...\" attribute so a big WordPress image scales to fit instead
        // of forcing a horizontal scroll; table-layout:fixed keeps wide tables inside the viewport too.
        "img{width:auto!important;max-width:100%!important;height:auto!important;}" +
        "table{max-width:100%!important;table-layout:fixed!important;}" +
        "img[src*=\"templates.invalid\"]{display:none!important;}" +
        "pre{background-color:#16161e!important;padding:12px!important;overflow-x:auto!important;border-radius:6px!important;}" +
        ".hljs{background:#16161e!important;}" +
        "hr{border-color:#414868!important;}" +
        "blockquote{border-left:3px solid #7aa2f7!important;color:#a9b1d6!important;}" +
        "</style>" +
        "<script src=\"file:///android_asset/highlight.min.js\"></script>" +
        "<script>window.addEventListener('load',function(){try{if(window.hljs){" +
        "document.querySelectorAll('pre').forEach(function(p){" +
        "var code=p.querySelector('code')||p;" +
        // Skip blocks that already carry their own markup (a feed that ships pre-highlighted code with
        // styled spans) -- re-highlighting those looks wrong. Only auto-highlight plain-text blocks.
        "if(code.children.length>0)return;" +
        "if(code===p){var c=document.createElement('code');c.textContent=p.textContent;p.textContent='';p.appendChild(c);code=c;}" +
        "hljs.highlightElement(code);});" +
        "}}catch(e){}});</script>";

    private bool _loaded;

    public ItemDetailPage(FeedItemViewModel item)
    {
        InitializeComponent();
        Title = item.Title;

        ArticleView.Source = new HtmlWebViewSource { Html = InjectCss(item.Html), BaseUrl = item.Link };
        ArticleView.Navigated += (s, e) => _loaded = true;
        ArticleView.Navigating += OnNavigating;

        if (AppSettings.MarkReadOnOpen)
            item.IsRead = true;
    }

    private static string InjectCss(string html)
    {
        html ??= string.Empty;
        // ThemeCss is constant; the reader font size is a user setting, appended after so it wins.
        var blob = ThemeCss + $"<style>html,body{{font-size:{AppSettings.ReaderFontSize}px!important;}}</style>";
        int head = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
        return head >= 0 ? html.Insert(head + "<head>".Length, blob) : blob + html;
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
