using RssBandit.ViewModels;

namespace RssBandit.Maui;

/// <summary>
/// Reads a single article: hosts the engine-rendered HTML (NewsItemFormatter output, carried on the
/// view-model) in a WebView. Opening an item marks it read (write-through to the engine item).
/// </summary>
public partial class ItemDetailPage : ContentPage
{
    public ItemDetailPage(FeedItemViewModel item)
    {
        InitializeComponent();
        Title = item.Title;
        ArticleView.Source = new HtmlWebViewSource { Html = item.Html, BaseUrl = item.Link };
        item.IsRead = true;
    }
}
