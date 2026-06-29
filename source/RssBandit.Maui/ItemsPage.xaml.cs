using System.Linq;
using RssBandit.ViewModels;

namespace RssBandit.Maui;

/// <summary>A single feed's item list. Refreshes the feed on first open (lazy), then taps through to
/// the article detail. Binds the feed's <see cref="SubscriptionItemViewModel"/>.</summary>
public partial class ItemsPage : ContentPage
{
    private readonly SubscriptionsViewModel _subs;
    private readonly SubscriptionItemViewModel _feed;

    public ItemsPage(SubscriptionsViewModel subs, SubscriptionItemViewModel feed)
    {
        InitializeComponent();
        _subs = subs;
        _feed = feed;
        Title = feed.Title;
        BindingContext = feed;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _subs.SetActiveFeed(_feed); // so a refresh formats this feed's items (not just its count)
        if (_feed.Node.Items.Count == 0)
            _subs.RefreshFeed(_feed);
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _subs.SetActiveFeed(null);
        if (_feed.Node.Items.Count > 0)
            _feed.UnreadCount = _feed.Node.UnreadCount; // reflect what was read back onto the tree
    }

    async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is FeedItemViewModel item)
        {
            // Hand the reader the whole feed + the tapped index so it can page Prev/Next through the
            // session, reusing the loaded shell (the Flyweight) across articles.
            var items = _feed.Node.Items;
            int idx = items.IndexOf(item);
            if (idx >= 0)
                await Navigation.PushAsync(new ItemDetailPage(items, idx));
        }
        ((CollectionView)sender).SelectedItem = null;
    }
}
