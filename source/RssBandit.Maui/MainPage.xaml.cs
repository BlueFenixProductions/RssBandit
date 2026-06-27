using System.Linq;

namespace RssBandit.Maui;

/// <summary>The subscription tree: feeds grouped by their OPML category. Tapping a feed opens its
/// item list (which refreshes that feed lazily). Add via the toolbar; swipe a feed to remove it.</summary>
public partial class MainPage : ContentPage
{
    private readonly SubscriptionsViewModel _vm = new();

    public MainPage()
    {
        InitializeComponent();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.InitializeAsync(); // imports the blogroll OPML on first run, loads the feedlist after
    }

    async void OnFeedSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is SubscriptionItemViewModel feed)
            await Navigation.PushAsync(new ItemsPage(_vm, feed));
        ((CollectionView)sender).SelectedItem = null;
    }

    void OnRefreshAll(object sender, System.EventArgs e) => _vm.RefreshAll();

    async void OnAddFeed(object sender, System.EventArgs e)
    {
        var url = await DisplayPromptAsync(
            "Add feed", "Feed URL:", accept: "Add", cancel: "Cancel",
            placeholder: "https://example.com/feed", keyboard: Keyboard.Url);

        var error = _vm.AddFeed(url); // null on success or cancel
        if (error != null)
            await DisplayAlert("Add feed", error, "OK");
    }

    async void OnDeleteFeed(object sender, System.EventArgs e)
    {
        if ((sender as SwipeItem)?.BindingContext is not SubscriptionItemViewModel sub)
            return;
        if (await DisplayAlert("Unsubscribe", $"Remove “{sub.Title}”?", "Remove", "Cancel"))
            _vm.RemoveFeed(sub);
    }
}
