using System.Linq;

namespace RssBandit.Maui;

/// <summary>The subscription tree: feeds grouped by their OPML category. Tapping a feed opens its
/// item list (which refreshes that feed lazily).</summary>
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
}
