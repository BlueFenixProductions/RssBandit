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
        if (_feed.Node.Items.Count == 0)
            _subs.RefreshFeed(_feed);
    }

    async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is FeedItemViewModel item)
            await Navigation.PushAsync(new ItemDetailPage(item));
        ((CollectionView)sender).SelectedItem = null;
    }
}
